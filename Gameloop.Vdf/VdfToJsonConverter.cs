using Gameloop.Vdf.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Gameloop.Vdf;

public static class VTokenExtensions
{
    public static JsonNode? ToJson(this VToken tok, VdfJsonConversionSettings? settings = null)
    {
        settings ??= new();

        return tok switch
        {
            VValue val => JsonValue.Create(val.Value),
            VObject obj => obj.ToJson(settings),
            VProperty p => p.Value.ToJson(settings),
            _ => throw new VdfException($"Unrecognized VToken type: {tok.GetType().Name}")
        };
    }

    public static JsonObject ToJson(this VObject obj, VdfJsonConversionSettings? settings = null)
    {
        settings ??= new();
        JsonObject resultObj = [];

        foreach (VProperty prop in obj.Properties())
        {
            if (!resultObj.ContainsKey(prop.Key))
            {
                resultObj.Add(prop.Key, prop.Value.ToJson(settings));
            }
            else if (resultObj[prop.Key] is not JsonObject)
            {
                HandleValueDuplicateKey(resultObj, prop, settings);
            }
            else
            {
                HandleObjectDuplicateKey(resultObj, prop, settings);
            }
        }

        return resultObj;
    }

    private static void HandleValueDuplicateKey(JsonObject baseObj, VProperty prop, VdfJsonConversionSettings settings)
    {
        switch (settings.ValueDuplicateKeyHandling)
        {
            case DuplicateKeyHandling.Replace:
                baseObj[prop.Key] = prop.Value.ToJson(settings);
                break;
            case DuplicateKeyHandling.Throw:
                throw new VdfException($"Key '{prop.Key}' already exists in object.");
            case DuplicateKeyHandling.Ignore:
                break;
        }
    }

    private static void HandleObjectDuplicateKey(JsonObject baseObj, VProperty prop, VdfJsonConversionSettings settings)
    {
        switch (settings.ObjectDuplicateKeyHandling)
        {
            case DuplicateKeyHandling.Merge:
                if (baseObj[prop.Key] is JsonObject targetObj)
                    targetObj.Merge(prop.Value.ToJson(settings));
                else
                    throw new VdfException($"Cannot merge Key '{prop.Key}': existing value is not an object.");
                break;

            case DuplicateKeyHandling.Replace:
                baseObj[prop.Key] = prop.Value.ToJson(settings);
                break;

            case DuplicateKeyHandling.Throw:
                throw new VdfException($"Key '{prop.Key}' already exists.");

            case DuplicateKeyHandling.Ignore:
                break;
        }
    }

    public static void Merge(this JsonObject target, JsonNode? other)
    {
        if (other is not JsonObject otherObj) return;

        foreach (var (key, value) in otherObj)
        {
            if (!target.TryGetPropertyValue(key, out var existing))
                target[key] = value?.DeepClone();
            else if (existing is JsonObject targetNested && value is JsonObject otherNested)
                targetNested.Merge(otherNested);
            else
                target[key] = value?.DeepClone();
        }
    }
}

public class VdfJsonConversionSettings
{
    public DuplicateKeyHandling ObjectDuplicateKeyHandling { get; set; } = DuplicateKeyHandling.Throw;

    public DuplicateKeyHandling ValueDuplicateKeyHandling
    {
        get => field;
        set
        {
            if (value == DuplicateKeyHandling.Merge)
                throw new ArgumentException("Merge is invalid for VDF values.");
            field = value;
        }
    } = DuplicateKeyHandling.Throw;
}

public enum DuplicateKeyHandling { Ignore, Merge, Replace, Throw }

public static class JsonExtensions
{
    public static VToken ToVdf(this JsonElement element, KeyValuesFormat format = KeyValuesFormat.Kv1) => element.ValueKind switch
    {
        JsonValueKind.Number => new VValue(element.GetRawText())
        {
            TypeHint = format == KeyValuesFormat.Kv3
                ? (element.TryGetInt64(out _) ? "int" : "double")
                : null
        },
        JsonValueKind.True => new VValue("1") { TypeHint = format == KeyValuesFormat.Kv3 ? "boolean" : null },
        JsonValueKind.False => new VValue("0") { TypeHint = format == KeyValuesFormat.Kv3 ? "boolean" : null },
        JsonValueKind.String => new VValue(element.GetString()) { TypeHint = format == KeyValuesFormat.Kv3 ? "string" : null },
        JsonValueKind.Array => element.ToVdfArray(format),
        JsonValueKind.Object => element.ToVdfObject(format),
        JsonValueKind.Null => VValue.CreateEmpty(),
        _ => throw new InvalidOperationException($"Unrecognized JsonValueKind: {element.ValueKind}")
    };

    private static VObject ToVdfObject(this JsonElement element, KeyValuesFormat format)
    {
        VObject resultObj = [];
        foreach (JsonProperty property in element.EnumerateObject())
            resultObj.Add(new VProperty(property.Name, property.Value.ToVdf(format)));
        return resultObj;
    }

    private static VObject ToVdfArray(this JsonElement element, KeyValuesFormat format)
    {
        VObject resultObj = [];
        int index = 0;
        foreach (JsonElement item in element.EnumerateArray())
        {
            string key = (format == KeyValuesFormat.Kv3) ? string.Empty : (index++).ToString();
            resultObj.Add(new VProperty(key, item.ToVdf(format)));
        }
        return resultObj;
    }
}