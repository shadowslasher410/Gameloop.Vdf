using System.Dynamic;
using System.Linq.Expressions;

namespace Gameloop.Vdf.Utilities;

/// <summary>
/// A base class used to provide a simplified interface for implementing dynamic behavior 
/// on VDF tokens without directly inheriting from <see cref="DynamicObject"/>.
/// </summary>
/// <typeparam name="T">The type of the instance being proxied.</typeparam>
internal class DynamicProxy<T>
{
    /// <summary>Returns an enumeration of all dynamic member names.</summary>
    public virtual IEnumerable<string> GetDynamicMemberNames(T instance) => [];

    /// <summary>Provides implementation for binary operations.</summary>
    public virtual bool TryBinaryOperation(T instance, BinaryOperationBinder binder, object arg, out object? result) { result = null; return false; }

    /// <summary>Provides implementation for type conversion operations.</summary>
    public virtual bool TryConvert(T instance, ConvertBinder binder, out object? result) { result = null; return false; }

    /// <summary>Provides implementation for creating a new instance of the object.</summary>
    public virtual bool TryCreateInstance(T instance, CreateInstanceBinder binder, object[] args, out object? result) { result = null; return false; }

    /// <summary>Provides implementation for deleting an index.</summary>
    public virtual bool TryDeleteIndex(T instance, DeleteIndexBinder binder, object[] indexes) => false;

    /// <summary>Provides implementation for deleting a member.</summary>
    public virtual bool TryDeleteMember(T instance, DeleteMemberBinder binder) => false;

    /// <summary>Provides implementation for getting a value by index.</summary>
    public virtual bool TryGetIndex(T instance, GetIndexBinder binder, object[] indexes, out object? result) { result = null; return false; }

    /// <summary>Provides implementation for getting a member value by name.</summary>
    public virtual bool TryGetMember(T instance, GetMemberBinder binder, out object? result) { result = null; return false; }

    /// <summary>Provides implementation for invoking the object.</summary>
    public virtual bool TryInvoke(T instance, InvokeBinder binder, object[] args, out object? result) { result = null; return false; }

    /// <summary>Provides implementation for invoking a member.</summary>
    public virtual bool TryInvokeMember(T instance, InvokeMemberBinder binder, object[] args, out object? result) { result = null; return false; }

    /// <summary>Provides implementation for setting a value by index.</summary>
    public virtual bool TrySetIndex(T instance, SetIndexBinder binder, object[] indexes, object value) => false;

    /// <summary>Provides implementation for setting a member value by name.</summary>
    public virtual bool TrySetMember(T instance, SetMemberBinder binder, object value) => false;

    /// <summary>Provides implementation for unary operations.</summary>
    public virtual bool TryUnaryOperation(T instance, UnaryOperationBinder binder, out object? result) { result = null; return false; }
}

/// <summary>
/// A <see cref="DynamicMetaObject"/> implementation that dispatches dynamic calls to a <see cref="DynamicProxy{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the object to bind to.</typeparam>
internal sealed class DynamicProxyMetaObject<T> : DynamicMetaObject
{
    private readonly DynamicProxy<T> _proxy;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicProxyMetaObject{T}"/> class.
    /// </summary>
    /// <param name="expression">The expression representing the <see cref="DynamicMetaObject"/> during the binding process.</param>
    /// <param name="value">The actual object represented by the <see cref="DynamicMetaObject"/>.</param>
    /// <param name="proxy">The proxy containing the logic for dynamic operations.</param>
    internal DynamicProxyMetaObject(Expression expression, T value, DynamicProxy<T> proxy)
        : base(expression, BindingRestrictions.GetTypeRestriction(expression, typeof(T)), value!)
    {
        _proxy = proxy;
    }

    /// <inheritdoc />
    public override DynamicMetaObject BindGetMember(GetMemberBinder binder) =>
        IsOverridden(nameof(DynamicProxy<>.TryGetMember))
            ? CallMethodWithResult(nameof(DynamicProxy<>.TryGetMember), binder, [], e => binder.FallbackGetMember(this, e))
            : base.BindGetMember(binder);

    /// <inheritdoc />
    public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value) =>
    IsOverridden(nameof(DynamicProxy<>.TrySetMember))
        ? CallMethodReturnLast(nameof(DynamicProxy<>.TrySetMember), binder, [value.Expression], e => binder.FallbackSetMember(this, value, e))
        : base.BindSetMember(binder, value);

    /// <inheritdoc />
    public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder) =>
        IsOverridden(nameof(DynamicProxy<>.TryDeleteMember))
            ? CallMethodNoResult(nameof(DynamicProxy<>.TryDeleteMember), binder, [], e => binder.FallbackDeleteMember(this, e))
            : base.BindDeleteMember(binder);

    /// <inheritdoc />
    public override DynamicMetaObject BindConvert(ConvertBinder binder) =>
        IsOverridden(nameof(DynamicProxy<>.TryConvert))
            ? CallMethodWithResult(nameof(DynamicProxy<>.TryConvert), binder, [], e => binder.FallbackConvert(this, e))
            : base.BindConvert(binder);

    /// <inheritdoc />
    public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
    {
        if (!IsOverridden(nameof(DynamicProxy<>.TryInvokeMember)))
            return base.BindInvokeMember(binder, args);

        return BuildCallMethodWithResult(
            nameof(DynamicProxy<>.TryInvokeMember),
            binder,
            GetArgArray(args),
            BuildCallMethodWithResult(
                nameof(DynamicProxy<>.TryGetMember),
                new GetBinderAdapter(binder),
                [],
                binder.FallbackInvokeMember(this, args, null!),
                e => binder.FallbackInvoke(e, args, null)
            ),
            null!
        );
    }

    /// <inheritdoc />
    public override DynamicMetaObject BindCreateInstance(CreateInstanceBinder binder, DynamicMetaObject[] args) =>
        IsOverridden(nameof(DynamicProxy<>.TryCreateInstance))
            ? CallMethodWithResult(nameof(DynamicProxy<>.TryCreateInstance), binder, GetArgArray(args), e => binder.FallbackCreateInstance(this, args, e))
            : base.BindCreateInstance(binder, args);

    /// <inheritdoc />
    public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args) =>
        IsOverridden(nameof(DynamicProxy<>.TryInvoke))
            ? CallMethodWithResult(nameof(DynamicProxy<>.TryInvoke), binder, GetArgArray(args), e => binder.FallbackInvoke(this, args, e))
            : base.BindInvoke(binder, args);

    /// <inheritdoc />
    public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg) =>
        IsOverridden(nameof(DynamicProxy<>.TryBinaryOperation))
            ? CallMethodWithResult(nameof(DynamicProxy<>.TryBinaryOperation), binder, GetArgs(arg), e => binder.FallbackBinaryOperation(this, arg, e))
            : base.BindBinaryOperation(binder, arg);

    /// <inheritdoc />
    public override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder) =>
        IsOverridden(nameof(DynamicProxy<>.TryUnaryOperation))
            ? CallMethodWithResult(nameof(DynamicProxy<>.TryUnaryOperation), binder, [], e => binder.FallbackUnaryOperation(this, e))
            : base.BindUnaryOperation(binder);

    /// <inheritdoc />
    public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes) =>
        IsOverridden(nameof(DynamicProxy<>.TryGetIndex))
            ? CallMethodWithResult(nameof(DynamicProxy<>.TryGetIndex), binder, GetArgArray(indexes), e => binder.FallbackGetIndex(this, indexes, e))
            : base.BindGetIndex(binder, indexes);

    /// <inheritdoc />
    public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value) =>
        IsOverridden(nameof(DynamicProxy<>.TrySetIndex))
            ? CallMethodReturnLast(nameof(DynamicProxy<>.TrySetIndex), binder, GetArgArray(indexes, value), e => binder.FallbackSetIndex(this, indexes, value, e))
            : base.BindSetIndex(binder, indexes, value);

    /// <inheritdoc />
    public override DynamicMetaObject BindDeleteIndex(DeleteIndexBinder binder, DynamicMetaObject[] indexes) =>
        IsOverridden(nameof(DynamicProxy<>.TryDeleteIndex))
            ? CallMethodNoResult(nameof(DynamicProxy<>.TryDeleteIndex), binder, GetArgArray(indexes), e => binder.FallbackDeleteIndex(this, indexes, e))
            : base.BindDeleteIndex(binder, indexes);

    /// <summary>
    /// Checks if a method is overridden in the specific proxy implementation.
    /// </summary>
    private bool IsOverridden(string method)
    {
        var m = _proxy.GetType().GetMethod(method);
        return m != null && m.DeclaringType != typeof(DynamicProxy<T>);
    }

    /// <summary>
    /// Prepares arguments for an expression, converting value types to object.
    /// </summary>
    private static IEnumerable<Expression> GetArgs(params DynamicMetaObject[] args) =>
    args.Select(arg => arg.Expression.Type.IsValueType
        ? Expression.Convert(arg.Expression, typeof(object))
        : arg.Expression);

    /// <summary>
    /// Wraps dynamic arguments into an array expression.
    /// </summary>
    private static Expression[] GetArgArray(DynamicMetaObject[] args) =>
        [Expression.NewArrayInit(typeof(object), GetArgs(args))];

    /// <summary>
    /// Wraps dynamic arguments and a specific value into an array expression.
    /// </summary>
    private static Expression[] GetArgArray(DynamicMetaObject[] args, DynamicMetaObject value) =>
    [
        Expression.NewArrayInit(typeof(object), GetArgs(args)),
        value.Expression.Type.IsValueType ? Expression.Convert(value.Expression, typeof(object)) : value.Expression
    ];

    /// <summary>
    /// Returns a constant expression for a binder, ensuring visibility.
    /// </summary>
    private static ConstantExpression Constant(DynamicMetaObjectBinder binder)
    {
        Type t = binder.GetType();
        while (!t.IsVisible) t = t.BaseType!;
        return Expression.Constant(binder, t);
    }

    /// <summary>
    /// Initiates the building of an expression tree for a dynamic operation that expects a return value.
    /// </summary>
    /// <param name="methodName">The name of the method to call on the <see cref="DynamicProxy{T}"/>.</param>
    /// <param name="binder">The dynamic binder providing the call site context.</param>
    /// <param name="args">The arguments to be passed to the proxy method.</param>
    /// <param name="fallback">The fallback logic to execute if the proxy cannot handle the operation.</param>
    /// <param name="fallbackInvoke">An optional delegate to handle further invocation of the result (e.g., for <c>BindInvokeMember</c>).</param>
    /// <returns>A <see cref="DynamicMetaObject"/> representing the complete call and fallback logic.</returns>
    private DynamicMetaObject CallMethodWithResult(string methodName, DynamicMetaObjectBinder binder, IEnumerable<Expression> args, Fallback fallback, Fallback? fallbackInvoke = null)
    {
        var fallbackResult = fallback(null!);
        return BuildCallMethodWithResult(methodName, binder, args, fallbackResult, fallbackInvoke);
    }

    /// <summary>
    /// Builds an expression tree that calls a proxy method which returns a value via an out parameter.
    /// </summary>
    /// <param name="methodName">The name of the method to call on the proxy.</param>
    /// <param name="binder">The dynamic binder providing operation context.</param>
    /// <param name="args">The arguments to pass to the method.</param>
    /// <param name="fallbackResult">The result to return if the proxy method returns false.</param>
    /// <param name="fallbackInvoke">An optional delegate to handle further invocation of the result.</param>
    /// <returns>A <see cref="DynamicMetaObject"/> representing the call logic.</returns>
    private DynamicMetaObject BuildCallMethodWithResult(string methodName, DynamicMetaObjectBinder binder, IEnumerable<Expression> args, DynamicMetaObject fallbackResult, Fallback? fallbackInvoke)
    {
        var resultParam = Expression.Parameter(typeof(object), null);

        List<Expression> callArgs = [
            Expression.Convert(Expression, typeof(T)),
            Constant(binder),
            .. args,
            resultParam
        ];

        var resultMO = new DynamicMetaObject(resultParam, BindingRestrictions.Empty);

        if (binder.ReturnType != typeof(object))
        {
            resultMO = new DynamicMetaObject(
                Expression.Convert(resultMO.Expression, binder.ReturnType),
                resultMO.Restrictions);
        }

        if (fallbackInvoke != null)
            resultMO = fallbackInvoke(resultMO);

        return new DynamicMetaObject(
            Expression.Block(
                [resultParam],
                Expression.Condition(
                    Expression.Call(
                        Expression.Constant(_proxy),
                        typeof(DynamicProxy<T>).GetMethod(methodName)!,
                        [.. callArgs]),
                    resultMO.Expression,
                    fallbackResult.Expression,
                    binder.ReturnType
                )
            ),
            GetRestrictions().Merge(resultMO.Restrictions).Merge(fallbackResult.Restrictions)
        );
    }

    /// <summary>
    /// Builds an expression tree for operations that should return the last argument (typically assignment).
    /// </summary>
    /// <param name="methodName">The name of the method to call on the proxy.</param>
    /// <param name="binder">The dynamic binder providing operation context.</param>
    /// <param name="args">The arguments to pass to the method.</param>
    /// <param name="fallback">The fallback logic if the proxy method returns false.</param>
    /// <returns>A <see cref="DynamicMetaObject"/> representing the call logic.</returns>
    private DynamicMetaObject CallMethodReturnLast(string methodName, DynamicMetaObjectBinder binder, IEnumerable<Expression> args, Fallback fallback)
    {
        var fallbackResult = fallback(null!);
        var argList = args.ToList();

        var boxedArgs = argList.Select(exp => exp.Type.IsValueType
            ? Expression.Convert(exp, typeof(object))
            : exp).ToList();

        Expression actualValue = boxedArgs.Count > 0
            ? boxedArgs[^1]
            : Expression.Default(typeof(object));

        List<Expression> callArgs = [
            Expression.Convert(Expression, typeof(T)),
        Constant(binder),
        .. boxedArgs
        ];
        return new DynamicMetaObject(
            Expression.Condition(
                Expression.Call(
                    Expression.Constant(_proxy),
                    _proxy.GetType().GetMethod(methodName)!,
                    [.. callArgs]),
                Expression.Convert(actualValue, typeof(object)),
                Expression.Convert(fallbackResult.Expression, typeof(object))
            ),
            GetRestrictions().Merge(fallbackResult.Restrictions)
        );
    }

    /// <summary>
    /// Builds an expression tree for operations that do not return a data result, such as deletion.
    /// </summary>
    /// <param name="methodName">The name of the method to call on the proxy.</param>
    /// <param name="binder">The dynamic binder providing operation context.</param>
    /// <param name="args">The arguments to pass to the method.</param>
    /// <param name="fallback">The fallback logic if the proxy method returns false.</param>
    /// <returns>A <see cref="DynamicMetaObject"/> representing the call logic.</returns>
    private DynamicMetaObject CallMethodNoResult(string methodName, DynamicMetaObjectBinder binder, IEnumerable<Expression> args, Fallback fallback)
    {
        var fallbackResult = fallback(null!);
        List<Expression> callArgs = [Expression.Convert(Expression, typeof(T)), Constant(binder), .. args];

        return new DynamicMetaObject(
            Expression.Condition(
                Expression.Call(
                    Expression.Constant(_proxy),
                    typeof(DynamicProxy<T>).GetMethod(methodName)!,
                    [.. callArgs]),
                Expression.Default(binder.ReturnType),
                Expression.Convert(fallbackResult.Expression, binder.ReturnType),
                binder.ReturnType
            ),
            GetRestrictions().Merge(fallbackResult.Restrictions)
        );
    }

    /// <summary>
    /// Generates the <see cref="BindingRestrictions"/> for the current expression based on type or instance.
    /// </summary>
    /// <returns>The calculated binding restrictions.</returns>
    private BindingRestrictions GetRestrictions() =>
        (Value == null && HasValue)
            ? BindingRestrictions.GetInstanceRestriction(Expression, null)
            : BindingRestrictions.GetTypeRestriction(Expression, typeof(T));

    /// <summary>
    /// Returns the enumeration of dynamic member names by querying the underlying proxy.
    /// </summary>
    /// <returns>A collection of member names.</returns>
    public override IEnumerable<string> GetDynamicMemberNames() => _proxy.GetDynamicMemberNames((T)Value!);

    /// <summary>
    /// Represents a method that provides a fallback <see cref="DynamicMetaObject"/> if binding fails.
    /// </summary>
    private delegate DynamicMetaObject Fallback(DynamicMetaObject errorSuggestion);

    /// <summary>
    /// An adapter that converts an <see cref="InvokeMemberBinder"/> into a <see cref="GetMemberBinder"/>.
    /// Used internally to attempt member retrieval before invocation.
    /// </summary>
    private sealed class GetBinderAdapter(InvokeMemberBinder binder)
        : GetMemberBinder(binder.Name, binder.IgnoreCase)
    {
        /// <summary>
        /// Always throws <see cref="NotSupportedException"/> as this adapter is used for internal dispatching.
        /// </summary>
        public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject? errorSuggestion)
            => throw new NotSupportedException();
    }
}