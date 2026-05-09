using System.Globalization;

namespace Gameloop.Vdf.Linq;

/// <summary>
/// Provides extension methods for <see cref="VToken"/> and collections of tokens to simplify data retrieval and conversion.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Returns the value of the first token in the collection converted to <typeparamref name="TU"/>.
    /// </summary>
    /// <typeparam name="TU">The type to convert the value to.</typeparam>
    /// <param name="value">The collection of tokens.</param>
    /// <returns>The converted value, or the default value of <typeparamref name="TU"/> if the collection is empty.</returns>
    public static TU? Value<TU>(this IEnumerable<VToken> value)
        => value.Value<VToken, TU>();

    /// <summary>
    /// Returns the value of the first token in a specific token collection converted to <typeparamref name="TU"/>.
    /// </summary>
    /// <typeparam name="T">The specific type of <see cref="VToken"/>.</typeparam>
    /// <typeparam name="TU">The type to convert the value to.</typeparam>
    /// <param name="value">The collection of tokens.</param>
    /// <returns>The converted value of the first element.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the value collection is null.</exception>
    public static TU? Value<T, TU>(this IEnumerable<T> value) where T : VToken
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.FirstOrDefault().Convert<T, TU>();
    }

    /// <summary>
    /// Converts a single <see cref="VToken"/> to the specified type <typeparamref name="TU"/>.
    /// </summary>
    /// <typeparam name="TU">The type to convert the token to.</typeparam>
    /// <param name="token">The token to convert.</param>
    /// <returns>The converted value, or default if the token is null.</returns>
    public static TU? Value<TU>(this VToken? token)
        => token.Convert<VToken, TU>();

    /// <summary>
    /// Internal helper to handle the conversion logic between VDF tokens and .NET types.
    /// </summary>
    /// <remarks>
    /// This method handles direct casts, <see cref="VValue"/> extraction, and <see cref="IConvertible"/> types 
    /// using <see cref="CultureInfo.InvariantCulture"/>.
    /// </remarks>
    internal static TU? Convert<T, TU>(this T? token) where T : VToken
    {
        if (token is null) return default;

        Type typeU = typeof(TU);

        // Direct cast for compatible types, excluding interfaces that might require specific string parsing
        if (token is TU result && typeU != typeof(IComparable) && typeU != typeof(IFormattable))
            return result;

        if (token is not VValue vValue)
            throw new InvalidCastException($"Cannot cast {token.GetType().Name} to {typeof(T).Name}.");

        if (vValue.Value is TU directMatch)
            return directMatch;

        Type? underlyingType = Nullable.GetUnderlyingType(typeU);
        Type targetType = underlyingType ?? typeU;

        if (underlyingType != null && vValue.Value is null)
            return default;

        // Try VDF-specific shorthand conversions (like "1"/"0" for bool)
        if (TryConvertVdf(vValue.Value, out TU? resultObj))
            return resultObj;

        return (TU?)System.Convert.ChangeType(vValue.Value, targetType, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Attempts to convert VDF-specific string representations to .NET types.
    /// </summary>
    /// <param name="value">The raw value from a <see cref="VValue"/>.</param>
    /// <param name="result">The resulting converted value.</param>
    /// <returns><c>true</c> if a VDF-specific conversion was applied; otherwise, <c>false</c>.</returns>
    private static bool TryConvertVdf<TU>(object? value, out TU? result)
    {
        result = default;

        Type typeU = typeof(TU);
        // Handle VDF boolean strings "1" and "0"
        if (typeU == typeof(bool) || Nullable.GetUnderlyingType(typeU) == typeof(bool))
        {
            if (value is "1")
            {
                result = (TU)(object)true;
                return true;
            }
            if (value is "0")
            {
                result = (TU)(object)false;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the lower-case invariant version of a string, or null if the string is null.
    /// </summary>
    internal static string? ToLowerSafe(this string? value)
        => value?.ToLowerInvariant();
}
