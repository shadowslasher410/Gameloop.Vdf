using System.Globalization;

namespace Gameloop.Vdf.Linq;

public static class Extensions
{

    public static TU? Value<TU>(this IEnumerable<VToken> value)
        => value.Value<VToken, TU>();

    public static TU? Value<T, TU>(this IEnumerable<T> value) where T : VToken
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.FirstOrDefault().Convert<T, TU>();
    }
    public static TU? Value<TU>(this VToken? token)
        => token.Convert<VToken, TU>();

    internal static TU? Convert<T, TU>(this T? token) where T : VToken
    {
        if (token is null) return default;

        Type typeU = typeof(TU);
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

        if (TryConvertVdf(vValue.Value, out TU? resultObj))
            return resultObj;

        return (TU?)System.Convert.ChangeType(vValue.Value, targetType, CultureInfo.InvariantCulture);
    }

    private static bool TryConvertVdf<TU>(object? value, out TU? result)
    {
        result = default;

        Type typeU = typeof(TU);
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

    internal static string? ToLowerSafe(this string? value)
        => value?.ToLowerInvariant();
}
