using Gameloop.Vdf.Utilities;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq.Expressions;

namespace Gameloop.Vdf.Linq;

/// <summary>
/// Initializes a new instance of the <see cref="VObject"/> class.
/// </summary>
/// <remarks>
/// A <see cref="VObject"/> acts as a container for other VDF tokens, typically <see cref="VProperty"/> nodes.
/// It supports dynamic access, index-based access, and key-based dictionary access.
/// </remarks>
public class VObject() : VToken, IList<VToken>, IDictionary<string, VToken>
{
    /// <summary>The internal list of child tokens.</summary>
    private readonly List<VToken> _children = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="VObject"/> class by deep-cloning the children of another <see cref="VObject"/>.
    /// </summary>
    /// <param name="other">The <see cref="VObject"/> to clone.</param>
    public VObject(VObject other) : this()
    {
        _children.AddRange(other._children.Select(x => x.DeepClone()));
        for (int i = 0; i < _children.Count; i++) SetPointers(_children[i], i);
    }

    /// <summary>
    /// Sets the parent and sibling pointers for a token to maintain the tree structure.
    /// </summary>
    /// <param name="token">The token to update.</param>
    /// <param name="index">The current index of the token in the child list.</param>
    private void SetPointers(VToken token, int index)
    {
        token.Parent = this;
        token.Previous = (index > 0) ? _children[index - 1] : null;
        token.Next = (index < _children.Count - 1) ? _children[index + 1] : null;

        token.Previous?.Next = token;
        token.Next?.Previous = token;
    }

    /// <summary>
    /// Clears the parent and sibling pointers of a token, effectively detaching it from this object.
    /// </summary>
    /// <param name="token">The token to detach.</param>
    private static void ClearPointers(VToken token)
    {
        token.Previous?.Next = token.Next;
        token.Next?.Previous = token.Previous;

        token.Parent = null;
        token.Previous = null;
        token.Next = null;
    }

    /// <summary>Gets the VDF token type, which is always <see cref="VTokenType.Object"/>.</summary>
    public override VTokenType Type => VTokenType.Object;

    /// <summary>Gets the number of child tokens in this object.</summary>
    public int Count => _children.Count;

    /// <summary>Gets a value indicating whether this collection is read-only. Always returns <c>false</c>.</summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets a child token using a property name.
    /// </summary>
    /// <param name="key">The name of the property. Must be a <see cref="string"/>.</param>
    /// <returns>The <see cref="VToken"/> associated with the key, or null if not found.</returns>
    /// <exception cref="ArgumentException">Thrown if the key is not a string.</exception>
    public override VToken? this[object key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);
            return key is string name ? this[name] : throw new ArgumentException("Property name expected.", nameof(key));
        }
        set
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key is string name) this[name] = value;
            else throw new ArgumentException("Property name expected.", nameof(key));
        }
    }

    /// <summary>
    /// Gets or sets the child token at the specified zero-based index.
    /// </summary>
    /// <param name="index">The zero-based index of the child token to get or set.</param>

    public VToken this[int index]
    {
        get => _children[index];
        set
        {
            ClearPointers(_children[index]);
            _children[index] = value;
            SetPointers(value, index);
        }
    }

    /// <summary>
    /// Gets or sets the <see cref="VToken"/> value associated with the specified property key.
    /// </summary>
    /// <param name="key">The key of the property to find.</param>
    /// <remarks>
    /// If setting a value for a key that already exists, the existing property's value is updated.
    /// If the key does not exist, a new <see cref="VProperty"/> is added to the object.
    /// </remarks>
    public VToken? this[string key]
    {
        get => TryGetValue(key, out var result) ? result : null;
        set
        {
            var prop = Properties().FirstOrDefault(x => x.Key == key);
            VToken valueToSet = (value is VProperty p && p.Key == key) ? p.Value : (value ?? VValue.CreateEmpty());
            if (prop != null)
                prop.Value = valueToSet;
            else
                Add(key, valueToSet);
        }
    }

    /// <summary>
    /// Returns an enumerable collection of all child tokens in this object.
    /// </summary>
    public override IEnumerable<VToken> Children() => _children;

    /// <summary>
    /// Returns an enumerable collection of children that are specifically <see cref="VProperty"/> instances.
    /// </summary>
    public IEnumerable<VProperty> Properties() => _children.OfType<VProperty>();

    /// <summary>
    /// Adds a new <see cref="VProperty"/> with the specified key and value to the object.
    /// </summary>
    /// <param name="key">The key for the new property.</param>
    /// <param name="value">The value for the new property.</param>
    public void Add(string key, VToken value) => Add(new VProperty(key, value));

    /// <summary>
    /// Adds an existing <see cref="VProperty"/> to the object.
    /// </summary>
    /// <param name="property">The property to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if the property or its value is null.</exception>
    public void Add(VProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(property.Value);
        Add((VToken)property);
    }

    /// <summary>
    /// Adds a generic <see cref="VToken"/> to the object and updates internal tree pointers.
    /// </summary>
    /// <param name="token">The token to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if the token is null.</exception>
    public void Add(VToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        _children.Add(token);
        SetPointers(token, _children.Count - 1);
    }

    /// <summary>
    /// Inserts a <see cref="VToken"/> at the specified index and updates the sibling pointers for the item and its immediate neighbors.
    /// </summary>
    /// <param name="index">The zero-based index at which the item should be inserted.</param>
    /// <param name="item">The token to insert.</param>
    /// <exception cref="ArgumentNullException">Thrown if the item is null.</exception>
    public void Insert(int index, VToken item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _children.Insert(index, item);
        for (int i = Math.Max(0, index - 1); i <= Math.Min(_children.Count - 1, index + 1); i++)
            SetPointers(_children[i], i);
    }


    /// <summary>
    /// Removes the first occurrence of a specific <see cref="VToken"/> from the object.
    /// </summary>
    /// <param name="item">The token to remove.</param>
    /// <returns><c>true</c> if the item was successfully removed; otherwise, <c>false</c>.</returns>
    public bool Remove(VToken item)
    {
        int index = _children.IndexOf(item);
        if (index == -1) return false;
        RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Removes the <see cref="VToken"/> at the specified index and repairs the sibling pointer chain.
    /// </summary>
    /// <param name="index">The zero-based index of the token to remove.</param>
    public void RemoveAt(int index)
    {
        VToken token = _children[index];
        _children.RemoveAt(index);
        ClearPointers(token);
        if (index < _children.Count) SetPointers(_children[index], index);
    }

    /// <summary>
    /// Removes all child tokens from the <see cref="VObject"/> and clears their parent/sibling associations.
    /// </summary>
    public void Clear()
    {
        foreach (var child in _children) ClearPointers(child);
        _children.Clear();
    }

    /// <summary>
    /// Attempts to find the value of a property with the specified key.
    /// </summary>
    /// <param name="key">The key of the property to find.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified key, if found; otherwise, null.</param>
    /// <returns><c>true</c> if a property with the key exists; otherwise, <c>false</c>.</returns>
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out VToken value)
    {
        value = Properties().FirstOrDefault(x => x.Key == key)?.Value;
        return value != null;
    }

    /// <summary>
    /// Serializes the current object and all its children to the specified <see cref="VdfWriter"/>.
    /// </summary>
    /// <param name="writer">The writer to which the object will be written.</param>
    public override void WriteTo(VdfWriter writer)
    {
        writer.WriteObjectStart();
        foreach (var child in _children) child.WriteTo(writer);
        writer.WriteObjectEnd();
    }

    /// <summary>
    /// Gets a collection containing the keys of all child properties.
    /// </summary>
    ICollection<string> IDictionary<string, VToken>.Keys =>
        [.. Properties().Select(x => x.Key)];

    /// <summary>
    /// Gets a collection containing the values of all child properties.
    /// </summary>
    ICollection<VToken> IDictionary<string, VToken>.Values =>
        [.. Properties().Select(x => x.Value)];

    /// <inheritdoc />
    /// <exception cref="KeyNotFoundException">Thrown if the key does not exist.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the value is null.</exception>
    VToken IDictionary<string, VToken>.this[string key]
    {
        get => this[key] ?? throw new KeyNotFoundException($"The key '{key}' was not found in the VObject.");
        set => this[key] = value ?? throw new ArgumentNullException(nameof(value), "VDF values cannot be null.");
    }

    /// <inheritdoc />
    IEnumerator<VToken> IEnumerable<VToken>.GetEnumerator() => _children.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => _children.GetEnumerator();

    /// <inheritdoc />
    IEnumerator<KeyValuePair<string, VToken>> IEnumerable<KeyValuePair<string, VToken>>.GetEnumerator()
    {
        foreach (VProperty property in Properties())
            yield return new KeyValuePair<string, VToken>(property.Key, property.Value);
    }

    /// <inheritdoc />
    void ICollection<KeyValuePair<string, VToken>>.Add(KeyValuePair<string, VToken> item) => Add(item.Key, item.Value);

    /// <inheritdoc />
    bool ICollection<KeyValuePair<string, VToken>>.Contains(KeyValuePair<string, VToken> item)
        => TryGetValue(item.Key, out var v) && v == item.Value;

    /// <inheritdoc />
    void ICollection<KeyValuePair<string, VToken>>.CopyTo(KeyValuePair<string, VToken>[] array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);
        List<KeyValuePair<string, VToken>> kvps = [.. Properties().Select(p => new KeyValuePair<string, VToken>(p.Key, p.Value))];
        kvps.CopyTo(array, index);
    }

    /// <inheritdoc />
    bool ICollection<KeyValuePair<string, VToken>>.Remove(KeyValuePair<string, VToken> item)
        => ((ICollection<KeyValuePair<string, VToken>>)this).Contains(item) && Remove(item.Key);

    /// <summary>Determines whether the <see cref="VObject"/> contains a specific <see cref="VToken"/>.</summary>
    /// <param name="item">The token to locate.</param>
    /// <returns><c>true</c> if the item is found; otherwise, <c>false</c>.</returns>
    public bool Contains(VToken item) => _children.Contains(item);

    /// <summary>Determines whether the <see cref="VObject"/> contains a property with the specified key.</summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is found; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(string key) => Properties().Any(x => x.Key == key);

    /// <summary>Copies the child tokens to an array, starting at a particular array index.</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in the array at which copying begins.</param>
    public void CopyTo(VToken[] array, int arrayIndex) => _children.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public override VToken DeepClone() => new VObject(this);

    /// <summary>Determines the index of a specific child token.</summary>
    /// <param name="item">The token to locate.</param>
    /// <returns>The index of the item if found; otherwise, -1.</returns>
    public int IndexOf(VToken item) => _children.IndexOf(item);

    /// <summary>Removes all child properties that match the specified key.</summary>
    /// <param name="key">The key of the properties to remove.</param>
    /// <returns><c>true</c> if any properties were removed; otherwise, <c>false</c>.</returns>
    public bool Remove(string key) => _children.RemoveAll(x => x is VProperty p && p.Key == key) > 0;

    /// <inheritdoc />
    protected override bool DeepEquals(VToken token)
    {
        if (token is not VObject other || _children.Count != other._children.Count) return false;

        return _children.SequenceEqual(other._children, VToken.EqualityComparer);
    }

    /// <inheritdoc />
    protected override DynamicMetaObject GetMetaObject(Expression parameter)
        => new DynamicProxyMetaObject<VObject>(parameter, this, new VObjectDynamicProxy());

    /// <summary>
    /// Provides the dynamic behavior for <see cref="VObject"/>, enabling member access through the 
    /// DLR (Dynamic Language Runtime). This allows VDF properties to be accessed as if they were 
    /// native C# properties.
    /// </summary>
    private class VObjectDynamicProxy : DynamicProxy<VObject>
    {
        /// <summary>
        /// Attempts to retrieve a value from the <see cref="VObject"/> using the dynamic member name as a key.
        /// </summary>
        /// <param name="instance">The <see cref="VObject"/> instance being accessed.</param>
        /// <param name="binder">Provides information about the dynamic member being requested.</param>
        /// <param name="result">The result of the member access. If the token is a <see cref="VProperty"/>, its value is returned.</param>
        /// <returns>Always returns <c>true</c>, returning <c>null</c> if the key does not exist.</returns>
        public override bool TryGetMember(VObject instance, GetMemberBinder binder, out object? result)
        {
            VToken? token = instance[binder.Name];

            if (token is VProperty prop)
                result = prop.Value;
            else
                result = token;

            return true;
        }

        /// <summary>
        /// Attempts to set a value in the <see cref="VObject"/> using the dynamic member name as a key.
        /// </summary>
        /// <param name="instance">The <see cref="VObject"/> instance being modified.</param>
        /// <param name="binder">Provides information about the dynamic member being set.</param>
        /// <param name="value">The value to set. If not already a <see cref="VToken"/>, it is converted to a <see cref="VValue"/>.</param>
        /// <returns>Always returns <c>true</c>.</returns>
        public override bool TrySetMember(VObject instance, SetMemberBinder binder, object? value)
        {
            VToken v = value is VToken token ? token : new VValue(value);
            instance[binder.Name] = v;
            return true;
        }

        /// <summary>
        /// Returns the names of all child properties to support IDE inspection and debugging.
        /// </summary>
        /// <param name="instance">The <see cref="VObject"/> instance to inspect.</param>
        /// <returns>An enumeration of all property keys currently in the object.</returns>
        public override IEnumerable<string> GetDynamicMemberNames(VObject instance)
            => instance.Properties().Select(p => p.Key);
    }
}

/// <summary>
/// Provides a read-only <see cref="ICollection{T}"/> wrapper around an <see cref="IEnumerable{T}"/>.
/// This is used internally to expose dictionary keys and values without allowing direct modification 
/// of the underlying property sequence.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
/// <param name="source">The enumerable source to wrap.</param>
internal class EnumerableCollection<T>(IEnumerable<T> source) : ICollection<T>
{
    /// <summary>
    /// The underlying data source for the collection.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the provided source is null.</exception>
    private readonly IEnumerable<T> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>Gets the number of elements contained in the collection.</summary>
    public int Count => _source.Count();

    /// <summary>Gets a value indicating whether the collection is read-only. Always returns <c>true</c>.</summary>
    public bool IsReadOnly => true;

    /// <exception cref="NotSupportedException">Always thrown as the collection is read-only.</exception>
    public void Add(T item) => throw new NotSupportedException("Collection is read-only.");

    /// <exception cref="NotSupportedException">Always thrown as the collection is read-only.</exception>
    public void Clear() => throw new NotSupportedException("Collection is read-only.");

    /// <exception cref="NotSupportedException">Always thrown as the collection is read-only.</exception>
    public bool Remove(T item) => throw new NotSupportedException("Collection is read-only.");

    /// <summary>Determines whether the collection contains a specific value.</summary>
    /// <param name="item">The object to locate.</param>
    /// <returns><c>true</c> if the item is found; otherwise, <c>false</c>.</returns>
    public bool Contains(T item) => _source.Contains(item);

    /// <summary>Copies the elements of the collection to an array, starting at a particular array index.</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in the array at which copying begins.</param>
    public void CopyTo(T[] array, int arrayIndex) => _source.ToList().CopyTo(array, arrayIndex);

    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <returns>An enumerator for the underlying source.</returns>
    public IEnumerator<T> GetEnumerator() => _source.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
