using Gameloop.Vdf.Utilities;
using System.Collections;
using System.Dynamic;
using System.Globalization;
using System.Linq.Expressions;

namespace Gameloop.Vdf.Linq;

public enum VTokenType
{
    None,
    Property,
    Object,
    Value,
    Comment,
    Conditional
}

public static class VdfExtensions
{
    public static bool IsContainer(this VTokenType type)
         => type is VTokenType.Object or VTokenType.Property;
}

public abstract class VToken : IEnumerable<VToken>, IDynamicMetaObjectProvider
{
    public VToken? Parent { get; internal set; }
    public VToken? Previous { get; internal set; }
    public VToken? Next { get; internal set; }

    public VToken Root => Parent?.Root ?? this;

    public string Path => Parent is VProperty p
        ? $"{(p.Parent?.Path is { Length: > 0 } parentPath ? parentPath + "." : "")}{p.Key}"
        : string.Empty;

    public abstract void WriteTo(VdfWriter writer);
    public abstract VTokenType Type { get; }
    public abstract VToken DeepClone();
    protected abstract bool DeepEquals(VToken node);

    public virtual IEnumerable<VToken> Children() => [];
    public IEnumerable<T> Children<T>() where T : VToken => Children().OfType<T>();

    public virtual VToken? this[object key]
    {
        get => throw new InvalidOperationException($"Cannot access child value on {this.GetType().Name}.");
        set => throw new InvalidOperationException($"Cannot set child value on {this.GetType().Name}.");
    }

    public virtual T? Value<T>(object key)
    {
        VToken? token = this[key];
        return token is null ? default : token.Convert<VToken, T>();
    }

    public static bool DeepEquals(VToken? t1, VToken? t2)
        => ReferenceEquals(t1, t2) || (t1 is not null && t2 is not null && t1.DeepEquals(t2));

    public IEnumerator<VToken> GetEnumerator() => Children().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) => GetMetaObject(parameter);

    protected virtual DynamicMetaObject GetMetaObject(Expression parameter)
        => new DynamicProxyMetaObject<VToken>(parameter, this, new DynamicProxy<VToken>());

    public override string ToString()
    {
        using StringWriter stringWriter = new(CultureInfo.InvariantCulture);
        VdfTextWriter vdfTextWriter = new(stringWriter);
        WriteTo(vdfTextWriter);
        return stringWriter.ToString();
    }

    internal static IEqualityComparer<VToken> EqualityComparer { get; } =
        System.Collections.Generic.EqualityComparer<VToken>.Create(DeepEquals);
}
