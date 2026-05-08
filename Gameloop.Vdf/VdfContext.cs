global using State = Gameloop.Vdf.VdfState;
using Gameloop.Vdf.Linq;
using System.Globalization;
using System.IO;
using System.Text;

namespace Gameloop.Vdf;

public enum VdfState
{
    Start, Key, Value, Property, Object, ObjectStart, ObjectEnd,
    ArrayStart, ArrayEnd, Comment, Conditional, Finished, Closed
}

public abstract class VdfBinaryReader(Stream stream) : IDisposable, IAsyncDisposable
{
    private readonly Stream _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    private readonly byte[] _buffer = new byte[8];

    public VProperty Read()
    {
        byte type = (byte)_stream.ReadByte();
        string name = ReadNullTerminatedString();
        return new VProperty(name, ReadValue(type));
    }

    private VToken ReadValue(byte type) => type switch
    {
        0x00 => ReadObject(),
        0x01 => new VValue(ReadNullTerminatedString()),
        0x02 => new VValue(ReadInt32()),
        0x03 => new VValue(ReadSingle()),
        0x07 => new VValue(ReadUInt64()),
        _ => throw new VdfException($"Unknown binary type: {type}")
    };

    private VObject ReadObject()
    {
        VObject obj = [];
        while (true)
        {
            byte type = (byte)_stream.ReadByte();
            if (type == 0x08 || type == 0x0B) break;

            string name = ReadNullTerminatedString();
            obj.Add(new VProperty(name, ReadValue(type)));
        }
        return obj;
    }

    public async Task<VProperty> ReadAsync()
    {
        await FillBufferAsync(1);
        byte type = _buffer[0];
        string name = await ReadNullTerminatedStringAsync();
        return new VProperty(name, await ReadValueAsync(type));
    }

    private async Task<VToken> ReadValueAsync(byte type) => type switch
    {
        0x00 => await ReadObjectAsync(),
        0x01 => new VValue(await ReadNullTerminatedStringAsync()),
        0x02 => new VValue(await ReadInt32Async()),
        0x03 => new VValue(await ReadSingleAsync()),
        0x07 => new VValue(await ReadUInt64Async()),
        _ => throw new VdfException($"Unknown binary type: {type}")
    };

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

    private string ReadNullTerminatedString()
    {
        using MemoryStream ms = new();
        int b;
        while ((b = _stream.ReadByte()) != 0x00 && b != -1) ms.WriteByte((byte)b);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private async Task<string> ReadNullTerminatedStringAsync()
    {
        using MemoryStream ms = new();
        byte[] b = new byte[1];
        while (await _stream.ReadAsync(b.AsMemory(0, 1)) > 0 && b[0] != 0x00)
            ms.WriteByte(b[0]);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private int ReadInt32() { FillBuffer(4); return BitConverter.ToInt32(_buffer, 0); }
    private float ReadSingle() { FillBuffer(4); return BitConverter.ToSingle(_buffer, 0); }
    private ulong ReadUInt64() { FillBuffer(8); return BitConverter.ToUInt64(_buffer, 0); }

    private async Task<int> ReadInt32Async() { await FillBufferAsync(4); return BitConverter.ToInt32(_buffer, 0); }
    private async Task<float> ReadSingleAsync() { await FillBufferAsync(4); return BitConverter.ToSingle(_buffer, 0); }
    private async Task<ulong> ReadUInt64Async() { await FillBufferAsync(8); return BitConverter.ToUInt64(_buffer, 0); }

    private void FillBuffer(int count)
    {
        int read = _stream.Read(_buffer, 0, count);
        if (read < count) throw new EndOfStreamException();
    }

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

    public void Dispose()
    {
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}


public abstract class VdfReader(VdfSerializerSettings settings) : IDisposable, IAsyncDisposable
{
    public VdfSerializerSettings Settings { get; } = settings;
    public bool CloseInput { get; set; } = true;
    public string? Value { get; set; } = null;
    public VdfState CurrentState { get; protected set; } = VdfState.Start;

    protected VdfReader() : this(VdfSerializerSettings.Default) { }

    public abstract bool ReadToken();
    public abstract Task<bool> ReadTokenAsync();

    public virtual void Close()
    {
        CurrentState = VdfState.Closed;
        Value = null;
    }

    public void Dispose()
    {
        if (CurrentState == VdfState.Closed) return;
        Close();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (CurrentState == VdfState.Closed) return;
        await DisposeAsyncCore().ConfigureAwait(false);
        Close();
        GC.SuppressFinalize(this);
    }

    protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;
}

public abstract class VdfWriter(VdfSerializerSettings settings) : IDisposable, IAsyncDisposable
{
    public VdfSerializerSettings Settings { get; } = settings;
    public bool CloseOutput { get; set; } = true;
    public VdfState CurrentState { get; protected set; } = VdfState.Start;

    protected VdfWriter() : this(VdfSerializerSettings.Default) { }

    public abstract void WriteArrayStart();
    public abstract void WriteArrayEnd();
    public abstract void WriteObjectStart();
    public abstract void WriteObjectEnd();
    public abstract void WriteKey(string key);
    public abstract void WriteComment(string text);
    public abstract void WriteConditional(IReadOnlyList<VConditional.Token> tokens);

    public virtual void WriteValue(VValue value) => WriteValue(value.ToString(), value.TypeHint);
    public abstract void WriteValue(string value, string? typeHint = null);

    public virtual void Close() => CurrentState = VdfState.Closed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (CurrentState == VdfState.Closed) return;
        if (disposing) Close();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual ValueTask DisposeAsyncCore()
    {
        if (CurrentState != VdfState.Closed) Close();
        return ValueTask.CompletedTask;
    }
    public abstract Task WriteKeyAsync(string key);
    public abstract Task WriteValueAsync(string value, string? typeHint = null);
    public virtual Task WriteValueAsync(VValue value) => WriteValueAsync(value.ToString(), value.TypeHint);
    public abstract Task WriteObjectStartAsync();
    public abstract Task WriteObjectEndAsync();
    public abstract Task WriteArrayStartAsync();
    public abstract Task WriteArrayEndAsync();
    public abstract Task WriteCommentAsync(string text);
    public abstract Task WriteConditionalAsync(IReadOnlyList<VConditional.Token> tokens);
}
public abstract class VdfBinaryWriter(Stream stream) : IDisposable, IAsyncDisposable
{
    private readonly Stream _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    private readonly byte[] _buffer = new byte[8];

    public void Write(VProperty property)
    {
        WriteToken(property.Value, property.Key);
        _stream.WriteByte(0x0B);
    }

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

    private void WriteValue(VValue val, string name)
    {
        byte type = GetTypeByte(val);
        _stream.WriteByte(type);
        WriteNullTerminatedString(name);

        switch (type)
        {
            case 0x01: 
                WriteNullTerminatedString(val.ToString()); 
                break;
            case 0x02: 
                // Suggestion 1: Use System.Convert for resilience against string-stored numbers
                BitConverter.TryWriteBytes(_buffer, Convert.ToInt32(val.Value, CultureInfo.InvariantCulture)); 
                _stream.Write(_buffer, 0, 4); 
                break;
            case 0x03: 
                BitConverter.TryWriteBytes(_buffer, Convert.ToSingle(val.Value, CultureInfo.InvariantCulture)); 
                _stream.Write(_buffer, 0, 4); 
                break;
            case 0x07: 
                BitConverter.TryWriteBytes(_buffer, Convert.ToUInt64(val.Value, CultureInfo.InvariantCulture)); 
                _stream.Write(_buffer, 0, 8); 
                break;
        }
    }

    public async Task WriteAsync(VProperty property)
    {
        await WriteTokenAsync(property.Value, property.Key);
        _stream.WriteByte(0x0B); // EOF
    }

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


    private static byte GetTypeByte(VValue val) => val.Value switch
    {
        int => 0x02,
        float => 0x03,
        ulong => 0x07,
        _ => 0x01 
    };

    private void WriteNullTerminatedString(string str)
    {
        _stream.Write(Encoding.UTF8.GetBytes(str));
        _stream.WriteByte(0x00);
    }

    private async Task WriteNullTerminatedStringAsync(string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        await _stream.WriteAsync(bytes);
        await _stream.WriteAsync(new byte[] { 0x00 });
    }

    public void Dispose()
    {
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}