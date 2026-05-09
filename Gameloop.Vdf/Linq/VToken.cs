using Gameloop.Vdf.Utilities;
using System.Collections;
using System.Dynamic;
using System.Globalization;
using System.Linq.Expressions;

namespace Gameloop.Vdf.Linq;

/// <summary>
/// Specifies the type of a <see cref="VToken"/>.
/// </summary>
public enum VTokenType
{
    /// <summary>No type has been assigned.</summary>
    None,
    /// <summary>A <see cref="VProperty"/> containing a key, value, and optional conditional.</summary>
    Property,
    /// <summary>A <see cref="VObject"/> containing a collection of children.</summary>
    Object,
    /// <summary>A <see cref="VValue"/> containing a primitive value (string, int, etc.).</summary>
    Value,
    /// <summary>A <see cref="Comment"/> containing a text comment.</summary>
    Comment,
    /// <summary>A <see cref="VConditional"/> containing environment-based inclusion logic.</summary>
    Conditional
}

/// <summary>
/// Provides utility extension methods for the <see cref="VTokenType"/> enumeration.
/// </summary>
public static class VdfExtensions
{
    /// <summary>
    /// Determines whether the specified token type represents a container that can hold child tokens.
    /// </summary>
    /// <param name="type">The <see cref="VTokenType"/> to check.</param>
    /// <returns>
    /// <c>true</c> if the type is <see cref="VTokenType.Object"/> or <see cref="VTokenType.Property"/>; 
    /// otherwise, <c>false</c>.
    /// </returns>
    public static bool IsContainer(this VTokenType type)
         => type is VTokenType.Object or VTokenType.Property;
}

/// <summary>
/// Represents the abstract base class for all VDF tokens.
/// Provides tree navigation, serialization, and dynamic object support.
/// </summary>
public abstract class VToken : IEnumerable<VToken>, IDynamicMetaObjectProvider
{
    /// <summary>Gets the parent <see cref="VToken"/> of this node.</summary>
    public VToken? Parent { get; internal set; }

    /// <summary>Gets the sibling token immediately preceding this one.</summary>
    public VToken? Previous { get; internal set; }

    /// <summary>Gets the sibling token immediately following this one.</summary>
    public VToken? Next { get; internal set; }

    /// <summary>Gets the top-most <see cref="VToken"/> in the current tree.</summary>
    public VToken Root => Parent?.Root ?? this;

    /// <summary>
    /// Gets the dot-separated path to this token from the root (e.g., "Parent.Child.Property").
    /// </summary>
    public string Path => Parent is VProperty p
        ? $"{(p.Parent?.Path is { Length: > 0 } parentPath ? parentPath + "." : "")}{p.Key}"
        : string.Empty;

    /// <summary>Writes the token to the specified <see cref="VdfWriter"/>.</summary>
    /// <param name="writer">The writer to use for serialization.</param>
    public abstract void WriteTo(VdfWriter writer);

    /// <summary>Gets the <see cref="VTokenType"/> for this token.</summary>
    public abstract VTokenType Type { get; }

    /// <summary>Creates a deep copy of the token and its children.</summary>
    /// <returns>A new <see cref="VToken"/> that is a deep clone of the current instance.</returns>
    public abstract VToken DeepClone();

    /// <summary>Determines whether the specified token is equal to the current token by comparing values and children.</summary>
    /// <param name="node">The token to compare with.</param>
    /// <returns><c>true</c> if the tokens are deeply equal; otherwise, <c>false</c>.</returns>
    protected abstract bool DeepEquals(VToken node);

    /// <summary>Returns an enumerable collection of child tokens for this token.</summary>
    /// <returns>An <see cref="IEnumerable{VToken}"/> containing the children.</returns>
    public virtual IEnumerable<VToken> Children() => [];

    /// <summary>Returns an enumerable collection of child tokens of the specified type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The type of child tokens to return.</typeparam>
    public IEnumerable<T> Children<T>() where T : VToken => Children().OfType<T>();

    /// <summary>Gets or sets a child value using the specified key.</summary>
    /// <param name="key">The key or index of the child to access.</param>
    /// <exception cref="InvalidOperationException">Thrown if the current token type does not support child access.</exception>
    public virtual VToken? this[object key]
    {
        get => throw new InvalidOperationException($"Cannot access child value on {this.GetType().Name}.");
        set => throw new InvalidOperationException($"Cannot set child value on {this.GetType().Name}.");
    }

    /// <summary>Gets the child token for the specified key and converts it to <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The type to convert the child's value to.</typeparam>
    /// <param name="key">The key of the child token.</param>
    /// <returns>The converted value, or the default value of <typeparamref name="T"/> if the token is null.</returns>
    public virtual T? Value<T>(object key)
    {
        VToken? token = this[key];
        return token is null ? default : token.Convert<VToken, T>();
    }

    /// <summary>Static helper to compare two tokens for deep equality, handling nulls.</summary>
    public static bool DeepEquals(VToken? t1, VToken? t2)
        => ReferenceEquals(t1, t2) || (t1 is not null && t2 is not null && t1.DeepEquals(t2));

    /// <inheritdoc />
    public IEnumerator<VToken> GetEnumerator() => Children().GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) => GetMetaObject(parameter);

    /// <summary>Returns a <see cref="DynamicMetaObject"/> for the current token to support the DLR.</summary>
    protected virtual DynamicMetaObject GetMetaObject(Expression parameter)
        => new DynamicProxyMetaObject<VToken>(parameter, this, new DynamicProxy<VToken>());

    /// <summary>Returns the VDF string representation of this token.</summary>
    public override string ToString()
    {
        using StringWriter stringWriter = new(CultureInfo.InvariantCulture);
        VdfTextWriter vdfTextWriter = new(stringWriter);
        WriteTo(vdfTextWriter);
        return stringWriter.ToString();
    }

    /// <summary>Gets an equality comparer capable of performing deep equality checks on VTokens.</summary>
    internal static IEqualityComparer<VToken> EqualityComparer { get; } =
        System.Collections.Generic.EqualityComparer<VToken>.Create(DeepEquals);
}