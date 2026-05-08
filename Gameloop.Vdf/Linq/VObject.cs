using Gameloop.Vdf.Utilities;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq.Expressions;

namespace Gameloop.Vdf.Linq;

public class VObject() : VToken, IList<VToken>, IDictionary<string, VToken>
{
    private readonly List<VToken> _children = [];

    public VObject(VObject other) : this()
    {
        _children.AddRange(other._children.Select(x => x.DeepClone()));
        for (int i = 0; i < _children.Count; i++) SetPointers(_children[i], i);
    }

    private void SetPointers(VToken token, int index)
    {
        token.Parent = this;
        token.Previous = (index > 0) ? _children[index - 1] : null;
        token.Next = (index < _children.Count - 1) ? _children[index + 1] : null;

        token.Previous?.Next = token;
        token.Next?.Previous = token;
    }

    private static void ClearPointers(VToken token)
    {
        token.Previous?.Next = token.Next;
        token.Next?.Previous = token.Previous;

        token.Parent = null;
        token.Previous = null;
        token.Next = null;
    }

    public override VTokenType Type => VTokenType.Object;
    public int Count => _children.Count;
    public bool IsReadOnly => false;

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

    public override IEnumerable<VToken> Children() => _children;
    public IEnumerable<VProperty> Properties() => _children.OfType<VProperty>();

    public void Add(string key, VToken value) => Add(new VProperty(key, value));

    public void Add(VProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(property.Value);
        Add((VToken)property);
    }

    public void Add(VToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        _children.Add(token);
        SetPointers(token, _children.Count - 1);
    }

    public void Insert(int index, VToken item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _children.Insert(index, item);
        for (int i = Math.Max(0, index - 1); i <= Math.Min(_children.Count - 1, index + 1); i++)
            SetPointers(_children[i], i);
    }

    public bool Remove(VToken item)
    {
        int index = _children.IndexOf(item);
        if (index == -1) return false;
        RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index)
    {
        VToken token = _children[index];
        _children.RemoveAt(index);
        ClearPointers(token);
        if (index < _children.Count) SetPointers(_children[index], index);
    }

    public void Clear()
    {
        foreach (var child in _children) ClearPointers(child);
        _children.Clear();
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out VToken value)
    {
        value = Properties().FirstOrDefault(x => x.Key == key)?.Value;
        return value != null;
    }

    public override void WriteTo(VdfWriter writer)
    {
        writer.WriteObjectStart();
        foreach (var child in _children) child.WriteTo(writer);
        writer.WriteObjectEnd();
    }

    ICollection<string> IDictionary<string, VToken>.Keys =>
        [.. Properties().Select(x => x.Key)];

    ICollection<VToken> IDictionary<string, VToken>.Values =>
        [.. Properties().Select(x => x.Value)];

    VToken IDictionary<string, VToken>.this[string key]
    {
        get => this[key] ?? throw new KeyNotFoundException($"The key '{key}' was not found in the VObject.");
        set => this[key] = value ?? throw new ArgumentNullException(nameof(value), "VDF values cannot be null.");
    }

    IEnumerator<VToken> IEnumerable<VToken>.GetEnumerator() => _children.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _children.GetEnumerator();

    IEnumerator<KeyValuePair<string, VToken>> IEnumerable<KeyValuePair<string, VToken>>.GetEnumerator()
    {
        foreach (VProperty property in Properties())
            yield return new KeyValuePair<string, VToken>(property.Key, property.Value);
    }

    void ICollection<KeyValuePair<string, VToken>>.Add(KeyValuePair<string, VToken> item) => Add(item.Key, item.Value);
    bool ICollection<KeyValuePair<string, VToken>>.Contains(KeyValuePair<string, VToken> item)
        => TryGetValue(item.Key, out var v) && v == item.Value;

    void ICollection<KeyValuePair<string, VToken>>.CopyTo(KeyValuePair<string, VToken>[] array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);
        List<KeyValuePair<string, VToken>> kvps = [.. Properties().Select(p => new KeyValuePair<string, VToken>(p.Key, p.Value))];
        kvps.CopyTo(array, index);
    }

    bool ICollection<KeyValuePair<string, VToken>>.Remove(KeyValuePair<string, VToken> item)
        => ((ICollection<KeyValuePair<string, VToken>>)this).Contains(item) && Remove(item.Key);

    public bool Contains(VToken item) => _children.Contains(item);
    public bool ContainsKey(string key) => Properties().Any(x => x.Key == key);
    public void CopyTo(VToken[] array, int arrayIndex) => _children.CopyTo(array, arrayIndex);
    public override VToken DeepClone() => new VObject(this);
    public int IndexOf(VToken item) => _children.IndexOf(item);
    public bool Remove(string key) => _children.RemoveAll(x => x is VProperty p && p.Key == key) > 0;

    protected override bool DeepEquals(VToken token)
    {
        if (token is not VObject other || _children.Count != other._children.Count) return false;

        return _children.SequenceEqual(other._children, VToken.EqualityComparer);
    }

    protected override DynamicMetaObject GetMetaObject(Expression parameter)
        => new DynamicProxyMetaObject<VObject>(parameter, this, new VObjectDynamicProxy());

    private class VObjectDynamicProxy : DynamicProxy<VObject>
    {
       public override bool TryGetMember(VObject instance, GetMemberBinder binder, out object? result)
        {
            VToken? token = instance[binder.Name];

            if (token is VProperty prop)
                result = prop.Value;
            else
                result = token;

            return true;
        }

        public override bool TrySetMember(VObject instance, SetMemberBinder binder, object? value)
        {
            VToken v = value is VToken token ? token : new VValue(value);
            instance[binder.Name] = v;
            return true;
        }

        public override IEnumerable<string> GetDynamicMemberNames(VObject instance)
            => instance.Properties().Select(p => p.Key);
    }
}

internal class EnumerableCollection<T>(IEnumerable<T> source) : ICollection<T>
{
    private readonly IEnumerable<T> _source = source ?? throw new ArgumentNullException(nameof(source));

    public int Count => _source.Count();
    public bool IsReadOnly => true;

    public void Add(T item) => throw new NotSupportedException("Collection is read-only.");
    public void Clear() => throw new NotSupportedException("Collection is read-only.");
    public bool Remove(T item) => throw new NotSupportedException("Collection is read-only.");

    public bool Contains(T item) => _source.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => _source.ToList().CopyTo(array, arrayIndex);

    public IEnumerator<T> GetEnumerator() => _source.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
