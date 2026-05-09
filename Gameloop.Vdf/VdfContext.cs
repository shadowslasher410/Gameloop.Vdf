global using State = Gameloop.Vdf.VdfState;
using Gameloop.Vdf.Linq;
using System.Globalization;
using System.Text;

namespace Gameloop.Vdf;

/// <summary>
/// Specifies the current state of the VDF reader or writer.
/// </summary>
public enum VdfState
{
    /// <summary>The initial state before reading or writing begins.</summary>
    Start, 
    /// <summary>Currently processing a property key.</summary>
    Key, 
    /// <summary>Currently processing a property value.</summary>
    Value, 
    /// <summary>Currently processing a full VProperty.</summary>
    Property, 
    /// <summary>Inside a VObject context.</summary>
    Object, 
    /// <summary>Processing the start of a VObject.</summary>
    ObjectStart, 
    /// <summary>Processing the end of a VObject.</summary>
    ObjectEnd,
    /// <summary>Processing the start of a VArray.</summary>
    ArrayStart, 
    /// <summary>Processing the end of a VArray.</summary>
    ArrayEnd, 
    /// <summary>Currently reading or writing a comment.</summary>
    Comment, 
    /// <summary>Processing a conditional block (e.g., [$WIN32]).</summary>
    Conditional, 
    /// <summary>The parsing process has finished successfully.</summary>
    Finished, 
    /// <summary>The reader or writer is closed and disposed.</summary>
    Closed
}

/// <summary>
/// Initializes a new instance of the <see cref="VdfBinaryReader"/> class using the specified <see cref="Stream"/>.
/// </summary>
/// <param name="stream">The binary stream to read Valve Data Format data from.</param>
/// <remarks>
/// The reader handles Valve's binary KeyValues format, supporting common primitive types like Int32, Single, and UInt64 
/// along with standard null-terminated strings and nested objects.
/// </remarks>
public abstract class VdfBinaryReader(Stream stream) : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// The underlying <see cref="Stream"/> from which the binary VDF data is read.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if the provided stream is null.</exception>
    private readonly Stream _stream = stream ?? throw new ArgumentNullException(nameof(stream));

    /// <summary>
    /// A fixed-size buffer used to store bytes for primitive type conversion (e.g., Int32, Single, UInt64).
    /// </summary>
    private readonly byte[] _buffer = new byte[8];

    /// <summary>
    /// Reads the next <see cref="VProperty"/> from the binary stream.
    /// </summary>
    /// <returns>The deserialized property.</returns>
    public VProperty Read()
    {
        byte type = (byte)_stream.ReadByte();
        string name = ReadNullTerminatedString();
        return new VProperty(name, ReadValue(type));
    }

    /// <summary>
    /// Asynchronously reads the next <see cref="VProperty"/> from the binary stream.
    /// </summary>
    /// <returns>A task representing the asynchronous read operation, containing the deserialized property.</returns>
    public async Task<VProperty> ReadAsync()
    {
        await FillBufferAsync(1);
        byte type = _buffer[0];
        string name = await ReadNullTerminatedStringAsync();
        return new VProperty(name, await ReadValueAsync(type));
    }

    /// <summary>
    /// Reads a value from the stream based on the provided Valve binary type header.
    /// </summary>
    /// <param name="type">The byte representing the VDF data type.</param>
    /// <returns>A <see cref="VToken"/> containing the deserialized value.</returns>
    /// <exception cref="VdfException">Thrown when an unsupported type byte is encountered.</exception>
    private VToken ReadValue(byte type) => type switch
    {
        0x00 => ReadObject(),
        0x01 => new VValue(ReadNullTerminatedString()),
        0x02 => new VValue(ReadInt32()),
        0x03 => new VValue(ReadSingle()),
        0x07 => new VValue(ReadUInt64()),
        _ => throw new VdfException($"Unknown binary type: {type}")
    };

    /// <summary>
    /// Reads a nested <see cref="VObject"/> from the stream by iteratively parsing properties until a terminator is reached.
    /// </summary>
    /// <returns>The deserialized <see cref="VObject"/>.</returns>
    private VObject ReadObject()
    {
        VObject obj = [];
        while (true)
        {
            byte type = (byte)_stream.ReadByte();
            if (type == 0x08 || type == 0x0B) break;
            // 0x08 is the standard terminator, 0x0B is sometimes used for EOF/Section end

            string name = ReadNullTerminatedString();
            obj.Add(new VProperty(name, ReadValue(type)));
        }
        return obj;
    }

    /// <summary>
    /// Asynchronously reads a value from the stream based on the provided Valve binary type header.
    /// </summary>
    private async Task<VToken> ReadValueAsync(byte type) => type switch
    {
        0x00 => await ReadObjectAsync(),
        0x01 => new VValue(await ReadNullTerminatedStringAsync()),
        0x02 => new VValue(await ReadInt32Async()),
        0x03 => new VValue(await ReadSingleAsync()),
        0x07 => new VValue(await ReadUInt64Async()),
        _ => throw new VdfException($"Unknown binary type: {type}")
    };

    /// <summary>
    /// Asynchronously reads a nested <see cref="VObject"/> from the stream.
    /// </summary>
    private async Task<VObject> ReadObjectAsync()
    {
        VObject obj = [];
        while (true)
        {
            await FillBufferAsync(1);
            byte type = _buffer[0];
            if (type == 0x08 || type == 0x0B) break;

            string name = await ReadNullTerminatedStringAsync();
            obj.Add(new VProperty(name, await ReadValueAsync(type)));
        }
        return obj;
    }

    /// <summary>
    /// Reads bytes from the stream until a null terminator (0x00) is reached and decodes them as a UTF-8 string.
    /// </summary>
    private string ReadNullTerminatedString()
    {
        using MemoryStream ms = new();
        int b;
        while ((b = _stream.ReadByte()) != 0x00 && b != -1) ms.WriteByte((byte)b);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// Asynchronously reads bytes until a null terminator is reached and decodes them as a UTF-8 string.
    /// </summary>
    private async Task<string> ReadNullTerminatedStringAsync()
    {
        using MemoryStream ms = new();
        byte[] b = new byte[1];
        while (await _stream.ReadAsync(b.AsMemory(0, 1)) > 0 && b[0] != 0x00)
            ms.WriteByte(b[0]);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>Reads a 4-byte signed integer from the current stream and advances the current position by four bytes.</summary>
    /// <returns>A 4-byte signed integer read from the current stream.</returns>
    private int ReadInt32() { FillBuffer(4); return BitConverter.ToInt32(_buffer, 0); }

    /// <summary>Reads a 4-byte floating point value from the current stream and advances the current position by four bytes.</summary>
    /// <returns>A 4-byte floating point value read from the current stream.</returns>
    private float ReadSingle() { FillBuffer(4); return BitConverter.ToSingle(_buffer, 0); }

    /// <summary>Reads an 8-byte unsigned integer from the current stream and advances the current position by eight bytes.</summary>
    /// <returns>An 8-byte unsigned integer read from the current stream.</returns>
    private ulong ReadUInt64() { FillBuffer(8); return BitConverter.ToUInt64(_buffer, 0); }

    /// <summary>Asynchronously reads a 4-byte signed integer from the current stream.</summary>
    /// <returns>A task that represents the asynchronous read operation, containing the 4-byte signed integer.</returns>
    private async Task<int> ReadInt32Async() { await FillBufferAsync(4); return BitConverter.ToInt32(_buffer, 0); }

    /// <summary>Asynchronously reads a 4-byte floating point value from the current stream.</summary>
    /// <returns>A task that represents the asynchronous read operation, containing the 4-byte floating point value.</returns>
    private async Task<float> ReadSingleAsync() { await FillBufferAsync(4); return BitConverter.ToSingle(_buffer, 0); }

    /// <summary>Asynchronously reads an 8-byte unsigned integer from the current stream.</summary>
    /// <returns>A task that represents the asynchronous read operation, containing the 8-byte unsigned integer.</returns>
    private async Task<ulong> ReadUInt64Async() { await FillBufferAsync(8); return BitConverter.ToUInt64(_buffer, 0); }

    /// <summary>
    /// Synchronously fills the internal buffer with the specified number of bytes.
    /// </summary>
    /// <exception cref="EndOfStreamException">Thrown if the stream ends before the count is reached.</exception>
    private void FillBuffer(int count)
    {
        int read = _stream.Read(_buffer, 0, count);
        if (read < count) throw new EndOfStreamException();
    }

    /// <summary>
    /// Asynchronously fills the internal buffer, handling partial reads from the underlying stream.
    /// </summary>
    /// <exception cref="EndOfStreamException">Thrown if the stream ends before the count is reached.</exception>
    private async Task FillBufferAsync(int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await _stream.ReadAsync(_buffer.AsMemory(totalRead, count - totalRead));
            if (read == 0) throw new EndOfStreamException();
            totalRead += read;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Initializes a new instance of the <see cref="VdfReader"/> class using the specified <see cref="VdfSerializerSettings"/>.
/// </summary>
/// <param name="settings">The settings used to configure the reader's behavior, such as parsing rules or format detection.</param>
public abstract class VdfReader(VdfSerializerSettings settings) : IDisposable, IAsyncDisposable
{
    /// <summary>Gets the settings used to configure the reader.</summary>
    public VdfSerializerSettings Settings { get; } = settings;

    /// <summary>Gets or sets a value indicating whether the underlying stream/reader should be closed when the VdfReader is closed.</summary>
    public bool CloseInput { get; set; } = true;

    /// <summary>Gets the text value of the last token read.</summary>
    public string? Value { get; set; } = null;

    /// <summary>Gets the current state of the reader.</summary>
    public VdfState CurrentState { get; protected set; } = VdfState.Start;

    protected VdfReader() : this(VdfSerializerSettings.Default) { }

    /// <summary>Reads the next VDF token from the source.</summary>
    /// <returns><c>true</c> if the next token was read successfully; <c>false</c> if there are no more tokens.</returns>
    public abstract bool ReadToken();

    /// <summary>Asynchronously reads the next VDF token from the source.</summary>
    /// <returns>A task representing the asynchronous read operation, containing <c>true</c> if the next token was read successfully.</returns>
    public abstract Task<bool> ReadTokenAsync();

    /// <summary>Closes the reader and sets the state to <see cref="VdfState.Closed"/>.</summary>
    public virtual void Close()
    {
        CurrentState = VdfState.Closed;
        Value = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (CurrentState == VdfState.Closed) return;
        Close();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (CurrentState == VdfState.Closed) return;
        await DisposeAsyncCore().ConfigureAwait(false);
        Close();
        GC.SuppressFinalize(this);
    }

    /// <summary>Performs asynchronous tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;
}

/// <summary>
/// Initializes a new instance of the <see cref="VdfWriter"/> class with the specified <see cref="VdfSerializerSettings"/>.
/// </summary>
/// <param name="settings">The settings used to configure the writer's behavior, such as formatting and escape sequence rules.</param>
public abstract class VdfWriter(VdfSerializerSettings settings) : IDisposable, IAsyncDisposable
{
    /// <summary>Gets the settings used to configure the writer.</summary>
    public VdfSerializerSettings Settings { get; } = settings;

    /// <summary>Gets or sets a value indicating whether the underlying stream/writer should be closed when the VdfWriter is closed.</summary>
    public bool CloseOutput { get; set; } = true;

    /// <summary>Gets the current state of the writer.</summary>
    public VdfState CurrentState { get; protected set; } = VdfState.Start;

    protected VdfWriter() : this(VdfSerializerSettings.Default) { }

    /// <summary>Writes the start of a VDF array.</summary>
    public abstract void WriteArrayStart();

    /// <summary>Writes the end of a VDF array.</summary>
    public abstract void WriteArrayEnd();

    /// <summary>Writes the start of a VDF object (curly brace).</summary>
    public abstract void WriteObjectStart();

    /// <summary>Writes the end of a VDF object (curly brace).</summary>
    public abstract void WriteObjectEnd();

    /// <summary>Writes a property key.</summary>
    /// <param name="key">The key name.</param>
    public abstract void WriteKey(string key);

    /// <summary>Writes a VDF comment.</summary>
    /// <param name="text">The comment content.</param>
    public abstract void WriteComment(string text);

    /// <summary>Writes a conditional block.</summary>
    /// <param name="tokens">The list of tokens forming the condition.</param>
    public abstract void WriteConditional(IReadOnlyList<VConditional.Token> tokens);

    /// <summary>Writes a <see cref="VValue"/>.</summary>
    /// <param name="value">The value token to write.</param>
    public virtual void WriteValue(VValue value) => WriteValue(value.ToString(), value.TypeHint);

    /// <summary>Writes a raw string value with an optional type hint.</summary>
    /// <param name="value">The string value.</param>
    /// <param name="typeHint">Optional hint for binary serialization types.</param>
    public abstract void WriteValue(string value, string? typeHint = null);

    /// <summary>Closes the writer and its underlying stream.</summary>
    public virtual void Close() => CurrentState = VdfState.Closed;

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases unmanaged and optionally managed resources.</summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (CurrentState == VdfState.Closed) return;
        if (disposing) Close();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>Performs asynchronous tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    protected virtual ValueTask DisposeAsyncCore()
    {
        if (CurrentState != VdfState.Closed) Close();
        return ValueTask.CompletedTask;
    }

    /// <summary>Asynchronously writes a property key.</summary>
    public abstract Task WriteKeyAsync(string key);

    /// <summary>Asynchronously writes a raw string value.</summary>
    public abstract Task WriteValueAsync(string value, string? typeHint = null);

    /// <summary>Asynchronously writes a <see cref="VValue"/>.</summary>
    public virtual Task WriteValueAsync(VValue value) => WriteValueAsync(value.ToString(), value.TypeHint);

    /// <summary>Asynchronously writes the start of a VDF object.</summary>
    public abstract Task WriteObjectStartAsync();

    /// <summary>Asynchronously writes the end of a VDF object.</summary>
    public abstract Task WriteObjectEndAsync();

    /// <summary>Asynchronously writes the start of a VDF array.</summary>
    public abstract Task WriteArrayStartAsync();

    /// <summary>Asynchronously writes the end of a VDF array.</summary>
    public abstract Task WriteArrayEndAsync();

    /// <summary>Asynchronously writes a comment.</summary>
    public abstract Task WriteCommentAsync(string text);

    /// <summary>Asynchronously writes a conditional block.</summary>
    public abstract Task WriteConditionalAsync(IReadOnlyList<VConditional.Token> tokens);
}

/// <summary>
/// Provides a base class for writing VDF data in Valve's binary format.
/// </summary>
public abstract class VdfBinaryWriter(Stream stream) : IDisposable, IAsyncDisposable
{
    private readonly Stream _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    private readonly byte[] _buffer = new byte[8];


    /// <summary>
    /// Writes a <see cref="VProperty"/> to the binary stream.
    /// </summary>
    /// <param name="property">The property to serialize.</param>
    public void Write(VProperty property)
    {
        WriteToken(property.Value, property.Key);
        _stream.WriteByte(0x0B);
    }

    /// <summary>
    /// Recursively writes a <see cref="VToken"/> (either an object or a value) to the stream.
    /// </summary>
    /// <param name="token">The token to write.</param>
    /// <param name="name">The key name associated with this token.</param>
    private void WriteToken(VToken token, string name)
    {
        if (token is VObject obj)
        {
            _stream.WriteByte(0x00);
            WriteNullTerminatedString(name);
            foreach (VProperty prop in obj.Properties())
                WriteToken(prop.Value, prop.Key);
            _stream.WriteByte(0x08);
        }
        else if (token is VValue val)
        {
            WriteValue(val, name);
        }
    }

    /// <summary>
    /// Writes a primitive <see cref="VValue"/> and its type header to the stream.
    /// </summary>
    /// <param name="val">The value to write.</param>
    /// <param name="name">The key name associated with this value.</param>
    private void WriteValue(VValue val, string name)
    {
        byte type = GetTypeByte(val);
        _stream.WriteByte(type);
        WriteNullTerminatedString(name);

        switch (type)
        {
            case 0x01: // String
                WriteNullTerminatedString(val.ToString());
                break;
            case 0x02: // Int32
                BitConverter.TryWriteBytes(_buffer, Convert.ToInt32(val.Value, CultureInfo.InvariantCulture));
                _stream.Write(_buffer, 0, 4);
                break;
            case 0x03: // Single (float)
                BitConverter.TryWriteBytes(_buffer, Convert.ToSingle(val.Value, CultureInfo.InvariantCulture));
                _stream.Write(_buffer, 0, 4);
                break;
            case 0x07: // UInt64
                BitConverter.TryWriteBytes(_buffer, Convert.ToUInt64(val.Value, CultureInfo.InvariantCulture));
                _stream.Write(_buffer, 0, 8);
                break;
        }
    }

    /// <summary>
    /// Asynchronously writes a <see cref="VProperty"/> to the binary stream.
    /// </summary>
    /// <param name="property">The property to serialize.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    public async Task WriteAsync(VProperty property)
    {
        await WriteTokenAsync(property.Value, property.Key);
        _stream.WriteByte(0x0B); // End of File / Section
    }

    /// <summary>
    /// Asynchronously and recursively writes a <see cref="VToken"/> to the stream.
    /// </summary>
    private async Task WriteTokenAsync(VToken token, string name)
    {
        if (token is VObject obj)
        {
            _stream.WriteByte(0x00);
            await WriteNullTerminatedStringAsync(name);
            foreach (VProperty prop in obj.Properties())
                await WriteTokenAsync(prop.Value, prop.Key);
            _stream.WriteByte(0x08);
        }
        else if (token is VValue val)
        {
            await WriteValueAsync(val, name);
        }
    }

    /// <summary>
    /// Asynchronously writes a primitive <see cref="VValue"/> and its type header.
    /// </summary>
    private async Task WriteValueAsync(VValue val, string name)
    {
        byte type = GetTypeByte(val);
        _stream.WriteByte(type);
        await WriteNullTerminatedStringAsync(name);

        switch (type)
        {
            case 0x01:
                await WriteNullTerminatedStringAsync(val.ToString());
                break;
            case 0x02:
                BitConverter.TryWriteBytes(_buffer, Convert.ToInt32(val.Value, CultureInfo.InvariantCulture));
                await _stream.WriteAsync(_buffer.AsMemory(0, 4));
                break;
            case 0x03:
                BitConverter.TryWriteBytes(_buffer, Convert.ToSingle(val.Value, CultureInfo.InvariantCulture));
                await _stream.WriteAsync(_buffer.AsMemory(0, 4));
                break;
            case 0x07:
                BitConverter.TryWriteBytes(_buffer, Convert.ToUInt64(val.Value, CultureInfo.InvariantCulture));
                await _stream.WriteAsync(_buffer.AsMemory(0, 8));
                break;
        }
    }

    /// <summary>
    /// Determines the Valve binary type byte based on the underlying type of the <see cref="VValue"/>.
    /// </summary>
    private static byte GetTypeByte(VValue val) => val.Value switch
    {
        int => 0x02,
        float => 0x03,
        ulong => 0x07,
        _ => 0x01 
    };

    /// <summary>
    /// Writes a UTF-8 string followed by a null terminator (0x00).
    /// </summary>
    private void WriteNullTerminatedString(string str)
    {
        _stream.Write(Encoding.UTF8.GetBytes(str));
        _stream.WriteByte(0x00);
    }

    /// <summary>
    /// Asynchronously writes a UTF-8 string followed by a null terminator (0x00).
    /// </summary>
    private async Task WriteNullTerminatedStringAsync(string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        await _stream.WriteAsync(bytes);
        await _stream.WriteAsync(new byte[] { 0x00 });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}