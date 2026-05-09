using Gameloop.Vdf.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Gameloop.Vdf;

/// <summary>
/// Provides extension methods for converting <see cref="VToken"/> structures into JSON representations.
/// </summary>
public static class VTokenExtensions
{
    /// <summary>
    /// Converts a <see cref="VToken"/> to a <see cref="JsonNode"/>.
    /// </summary>
    /// <param name="tok">The token to convert.</param>
    /// <param name="settings">The settings to handle conversion, such as duplicate key strategies.</param>
    /// <returns>A <see cref="JsonNode"/> representing the token data, or null for empty values.</returns>
    /// <exception cref="VdfException">Thrown if an unsupported <see cref="VToken"/> type is encountered.</exception>
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

    /// <summary>
    /// Converts a <see cref="VObject"/> to a <see cref="JsonObject"/>.
    /// </summary>
    /// <param name="obj">The VDF object to convert.</param>
    /// <param name="settings">The settings defining how to handle duplicate keys during conversion.</param>
    /// <returns>A <see cref="JsonObject"/> containing the properties of the VDF object.</returns>
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

    /// <summary>
    /// Handles duplicate keys where the existing JSON value is a primitive (not an object).
    /// </summary>
    /// <param name="baseObj">The JSON object currently being built.</param>
    /// <param name="prop">The VDF property with the duplicate key.</param>
    /// <param name="settings">The settings defining the handling strategy.</param>
    /// <exception cref="VdfException">Thrown if the strategy is <see cref="DuplicateKeyHandling.Throw"/>.</exception>
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

    /// <summary>
    /// Handles duplicate keys where the existing JSON value is a <see cref="JsonObject"/>.
    /// </summary>
    /// <param name="baseObj">The JSON object currently being built.</param>
    /// <param name="prop">The VDF property with the duplicate key.</param>
    /// <param name="settings">The settings defining the handling strategy.</param>
    /// <exception cref="VdfException">Thrown if the strategy is <see cref="DuplicateKeyHandling.Throw"/> or if merging is impossible.</exception>
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

    /// <summary>
    /// Deeply merges another <see cref="JsonNode"/> into the target <see cref="JsonObject"/>.
    /// </summary>
    /// <param name="target">The target object to merge into.</param>
    /// <param name="other">The source node to merge from. Only <see cref="JsonObject"/> types are processed.</param>
    /// <remarks>
    /// If keys collide and both values are objects, they are recursively merged. 
    /// Otherwise, the target value is replaced with a deep clone of the source value.
    /// </remarks>
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

/// <summary>
/// Specifies settings for handling duplicate keys during JSON to VDF conversion.
/// </summary>
public class VdfJsonConversionSettings
{
    /// <summary>Gets or sets how duplicate keys should be handled when the values are objects.</summary>
    public DuplicateKeyHandling ObjectDuplicateKeyHandling { get; set; } = DuplicateKeyHandling.Throw;

    /// <summary>Gets or sets how duplicate keys should be handled when the values are primitives.</summary>
    /// <exception cref="ArgumentException">Thrown when attempting to set the handling to <see cref="DuplicateKeyHandling.Merge"/>.</exception>
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

/// <summary>
/// Specifies the strategy used to handle duplicate keys during conversion or merging.
/// </summary>
public enum DuplicateKeyHandling 
{
    /// <summary>Keep the existing value and skip the new one.</summary>
    Ignore,

    /// <summary>Recursively merge the new value into the existing one (valid for objects only).</summary>
    Merge,

    /// <summary>Overwrite the existing value with the new one.</summary>
    Replace,

    /// <summary>Throw an exception when a duplicate key is encountered.</summary>
    Throw
}

/// <summary>
/// Provides extension methods to convert <see cref="JsonElement"/> structures into <see cref="VToken"/> structures.
/// </summary>
public static class JsonExtensions
{
    /// <summary>
    /// Converts a <see cref="JsonElement"/> to its equivalent <see cref="VToken"/> representation.
    /// </summary>
    /// <param name="element">The JSON element to convert.</param>
    /// <param name="format">The target VDF format, which influences type hinting and array keys.</param>
    /// <returns>A <see cref="VToken"/> representing the JSON data.</returns>
    /// <exception cref="InvalidOperationException">Thrown if an unsupported JSON value kind is encountered.</exception>
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

    /// <summary>
    /// Converts a JSON object element to a <see cref="VObject"/>.
    /// </summary>
    private static VObject ToVdfObject(this JsonElement element, KeyValuesFormat format)
    {
        VObject resultObj = [];
        foreach (JsonProperty property in element.EnumerateObject())
            resultObj.Add(new VProperty(property.Name, property.Value.ToVdf(format)));
        return resultObj;
    }

    /// <summary>
    /// Converts a JSON array element to a <see cref="VObject"/>.
    /// </summary>
    /// <remarks>
    /// For KV1/KV2, array items are given sequential string keys ("0", "1", etc.). 
    /// For KV3, empty strings are used as keys to represent array-like sequences.
    /// </remarks>
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