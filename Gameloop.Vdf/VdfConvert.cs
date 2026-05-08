using Gameloop.Vdf.Linq;
using System.Globalization;
using System.Text;

namespace Gameloop.Vdf;

public static class VdfConvert
{
    private const int DefaultStringBuilderCapacity = 1024;

    public static string Serialize(VToken value) => Serialize(value, VdfSerializerSettings.Common);

    public static string Serialize(VToken value, VdfSerializerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(value);

        StringBuilder stringBuilder = new(DefaultStringBuilderCapacity);
        using StringWriter stringWriter = new(stringBuilder, CultureInfo.InvariantCulture);

        new VdfSerializer(settings).Serialize(stringWriter, value);

        return stringBuilder.ToString();
    }

    public static VProperty Deserialize(string value) => Deserialize(value, VdfSerializerSettings.Common);

    public static VProperty Deserialize(string value, VdfSerializerSettings settings)
        => Deserialize(new StringReader(value), settings);

    public static VProperty Deserialize(Stream stream, VdfSerializerSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Deserialize(reader, settings ?? VdfSerializerSettings.Common);
    }

    public static VProperty Deserialize(TextReader reader) => Deserialize(reader, VdfSerializerSettings.Common);

    public static VProperty Deserialize(TextReader reader, VdfSerializerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return new VdfSerializer(settings).Deserialize(reader);
    }

    public static VObject DeserializeObject(string value, VdfSerializerSettings? settings = null)
    {
        VProperty root = Deserialize(value, settings ?? VdfSerializerSettings.Common);
        if (root.Value is not VObject obj)
            throw new VdfException($"The root of the VDF is not an object (found {root.Value.Type}).");

        return obj;
    }
}

public class VdfException(string message, Exception? inner = null) : Exception(message, inner);
