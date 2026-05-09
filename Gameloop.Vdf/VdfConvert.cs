using Gameloop.Vdf.Linq;
using System.Globalization;
using System.Text;

namespace Gameloop.Vdf;

/// <summary>
/// Provides methods for converting between VDF (Valve Data Format) strings and <see cref="VToken"/> objects.
/// </summary>
public static class VdfConvert
{
    /// <summary>
    /// The default initial capacity for the <see cref="StringBuilder"/> used during serialization.
    /// A value of 1024 is chosen to handle most standard VDF files without frequent reallocations.
    /// </summary>
    private const int DefaultStringBuilderCapacity = 1024;

    /// <summary>
    /// Serializes the specified <see cref="VToken"/> to a VDF string using common settings.
    /// </summary>
    /// <param name="value">The token to serialize.</param>
    /// <returns>A VDF string representation of the token.</returns>
    public static string Serialize(VToken value) => Serialize(value, VdfSerializerSettings.Common);

    /// <summary>
    /// Serializes the specified <see cref="VToken"/> to a VDF string using custom settings.
    /// </summary>
    /// <param name="value">The token to serialize.</param>
    /// <param name="settings">The settings to use for serialization.</param>
    /// <returns>A VDF string representation of the token.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static string Serialize(VToken value, VdfSerializerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(value);

        StringBuilder stringBuilder = new(DefaultStringBuilderCapacity);
        using StringWriter stringWriter = new(stringBuilder, CultureInfo.InvariantCulture);

        new VdfSerializer(settings).Serialize(stringWriter, value);

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Deserializes a VDF string into a <see cref="VProperty"/> using common settings.
    /// </summary>
    /// <param name="value">The VDF string to deserialize.</param>
    /// <returns>The deserialized <see cref="VProperty"/>.</returns>
    public static VProperty Deserialize(string value) => Deserialize(value, VdfSerializerSettings.Common);

    /// <summary>
    /// Deserializes a VDF string into a <see cref="VProperty"/> using custom settings.
    /// </summary>
    /// <param name="value">The VDF string to deserialize.</param>
    /// <param name="settings">The settings to use for deserialization.</param>
    /// <returns>The deserialized <see cref="VProperty"/>.</returns>
    public static VProperty Deserialize(string value, VdfSerializerSettings settings)
        => Deserialize(new StringReader(value), settings);

    /// <summary>
    /// Deserializes VDF data from a <see cref="Stream"/> into a <see cref="VProperty"/>.
    /// </summary>
    /// <param name="stream">The stream containing VDF data.</param>
    /// <param name="settings">The optional settings to use for deserialization.</param>
    /// <returns>The deserialized <see cref="VProperty"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    public static VProperty Deserialize(Stream stream, VdfSerializerSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Deserialize(reader, settings ?? VdfSerializerSettings.Common);
    }

    /// <summary>
    /// Deserializes VDF data from a <see cref="TextReader"/> into a <see cref="VProperty"/> using common settings.
    /// </summary>
    /// <param name="reader">The reader containing VDF data.</param>
    /// <returns>The deserialized <see cref="VProperty"/>.</returns>
    public static VProperty Deserialize(TextReader reader) => Deserialize(reader, VdfSerializerSettings.Common);

    /// <summary>
    /// Deserializes VDF data from a <see cref="TextReader"/> into a <see cref="VProperty"/> using custom settings.
    /// </summary>
    /// <param name="reader">The reader containing VDF data.</param>
    /// <param name="settings">The settings to use for deserialization.</param>
    /// <returns>The deserialized <see cref="VProperty"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is null.</exception>
    public static VProperty Deserialize(TextReader reader, VdfSerializerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return new VdfSerializer(settings).Deserialize(reader);
    }

    /// <summary>
    /// Deserializes a VDF string and ensures the root value is a <see cref="VObject"/>.
    /// </summary>
    /// <param name="value">The VDF string to deserialize.</param>
    /// <param name="settings">The optional settings to use for deserialization.</param>
    /// <returns>The root <see cref="VObject"/> of the VDF data.</returns>
    /// <exception cref="VdfException">Thrown if the root value is not an object.</exception>
    public static VObject DeserializeObject(string value, VdfSerializerSettings? settings = null)
    {
        VProperty root = Deserialize(value, settings ?? VdfSerializerSettings.Common);
        if (root.Value is not VObject obj)
            throw new VdfException($"The root of the VDF is not an object (found {root.Value.Type}).");

        return obj;
    }
}

/// <summary>
/// Represents errors that occur during VDF serialization or deserialization.
/// </summary>
/// <param name="message">The message that describes the error.</param>
/// <param name="inner">The exception that is the cause of the current exception.</param>
public class VdfException(string message, Exception? inner = null) : Exception(message, inner);