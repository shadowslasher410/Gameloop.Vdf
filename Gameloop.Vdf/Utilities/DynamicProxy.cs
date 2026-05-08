using System.Dynamic;
using System.Linq.Expressions;

namespace Gameloop.Vdf.Utilities;

internal class DynamicProxy<T>
{
    public virtual IEnumerable<string> GetDynamicMemberNames(T instance) => [];
    public virtual bool TryBinaryOperation(T instance, BinaryOperationBinder binder, object arg, out object? result) { result = null; return false; }
    public virtual bool TryConvert(T instance, ConvertBinder binder, out object? result) { result = null; return false; }
    public virtual bool TryCreateInstance(T instance, CreateInstanceBinder binder, object[] args, out object? result) { result = null; return false; }
    public virtual bool TryDeleteIndex(T instance, DeleteIndexBinder binder, object[] indexes) => false;
    public virtual bool TryDeleteMember(T instance, DeleteMemberBinder binder) => false;
    public virtual bool TryGetIndex(T instance, GetIndexBinder binder, object[] indexes, out object? result) { result = null; return false; }
    public virtual bool TryGetMember(T instance, GetMemberBinder binder, out object? result) { result = null; return false; }
    public virtual bool TryInvoke(T instance, InvokeBinder binder, object[] args, out object? result) { result = null; return false; }
    public virtual bool TryInvokeMember(T instance, InvokeMemberBinder binder, object[] args, out object? result) { result = null; return false; }
    public virtual bool TrySetIndex(T instance, SetIndexBinder binder, object[] indexes, object value) => false;
    public virtual bool TrySetMember(T instance, SetMemberBinder binder, object value) => false;
    public virtual bool TryUnaryOperation(T instance, UnaryOperationBinder binder, out object? result) { result = null; return false; }
}

internal sealed class DynamicProxyMetaObject<T> : DynamicMetaObject
{
    private readonly DynamicProxy<T> _proxy;

    internal DynamicProxyMetaObject(Expression expression, T value, DynamicProxy<T> proxy)
        : base(expression, BindingRestrictions.GetTypeRestriction(expression, typeof(T)), value!)
    {
        _proxy = proxy;
    }

    public override DynamicMetaObject BindGetMember(GetMemberBinder binder) =>
        IsOverridden(nameof(DynamicProxy<>.TryGetMember))
            ? CallMethodWithResult(nameof(DynamicProxy<>.TryGetMember), binder, [], e => binder.FallbackGetMember(this, e))
            : base.BindGetMember(binder);

    public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value) =>
    IsOverridden(nameof(DynamicProxy<>.TrySetMember))
        ? CallMethodReturnLast(nameof(DynamicProxy<>.TrySetMember), binder, [value.Expression], e => binder.FallbackSetMember(this, value, e))
        : base.BindSetMember(binder, value);

    public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder) =>
        IsOverridden(nameof(DynamicProxy<>.TryDeleteMember))
            ? CallMethodNoResult(nameof(DynamicProxy<>.TryDeleteMember), binder, [], e => binder.FallbackDeleteMember(this, e))
            : base.BindDeleteMember(binder);

    public override DynamicMetaObject BindConvert(ConvertBinder binder) =>
        IsOverridden(nameof(DynamicProxy<>.TryConvert))
            ? CallMethodWithResult(nameof(DynamicProxy<>.TryConvert), binder, [], e => binder.FallbackConvert(this, e))
            : base.BindConvert(binder);

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

    public override DynamicMetaObject BindCreateInstance(CreateInstanceBinder binder, DynamicMetaObject[] args) =>
        IsOverridden(nameof(DynamicProxy<>.TryCreateInstance))
            ? CallMethodWithResult(nameof(DynamicProxy<>.TryCreateInstance), binder, GetArgArray(args), e => binder.FallbackCreateInstance(this, args, e))
            : base.BindCreateInstance(binder, args);

    public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args) =>
        IsOverridden(nameof(DynamicProxy<>.TryInvoke))
            ? CallMethodWithResult(nameof(DynamicProxy<>.TryInvoke), binder, GetArgArray(args), e => binder.FallbackInvoke(this, args, e))
            : base.BindInvoke(binder, args);

    public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg) =>
        IsOverridden(nameof(DynamicProxy<>.TryBinaryOperation))
            ? CallMethodWithResult(nameof(DynamicProxy<>.TryBinaryOperation), binder, GetArgs(arg), e => binder.FallbackBinaryOperation(this, arg, e))
            : base.BindBinaryOperation(binder, arg);

    public override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder) =>
        IsOverridden(nameof(DynamicProxy<>.TryUnaryOperation))
            ? CallMethodWithResult(nameof(DynamicProxy<>.TryUnaryOperation), binder, [], e => binder.FallbackUnaryOperation(this, e))
            : base.BindUnaryOperation(binder);

    public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes) =>
        IsOverridden(nameof(DynamicProxy<>.TryGetIndex))
            ? CallMethodWithResult(nameof(DynamicProxy<>.TryGetIndex), binder, GetArgArray(indexes), e => binder.FallbackGetIndex(this, indexes, e))
            : base.BindGetIndex(binder, indexes);

    public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value) =>
        IsOverridden(nameof(DynamicProxy<>.TrySetIndex))
            ? CallMethodReturnLast(nameof(DynamicProxy<>.TrySetIndex), binder, GetArgArray(indexes, value), e => binder.FallbackSetIndex(this, indexes, value, e))
            : base.BindSetIndex(binder, indexes, value);

    public override DynamicMetaObject BindDeleteIndex(DeleteIndexBinder binder, DynamicMetaObject[] indexes) =>
        IsOverridden(nameof(DynamicProxy<>.TryDeleteIndex))
            ? CallMethodNoResult(nameof(DynamicProxy<>.TryDeleteIndex), binder, GetArgArray(indexes), e => binder.FallbackDeleteIndex(this, indexes, e))
            : base.BindDeleteIndex(binder, indexes);

    private bool IsOverridden(string method)
    {
        var m = _proxy.GetType().GetMethod(method);
        return m != null && m.DeclaringType != typeof(DynamicProxy<T>);
    }



    private static IEnumerable<Expression> GetArgs(params DynamicMetaObject[] args) =>
    args.Select(arg => arg.Expression.Type.IsValueType
        ? Expression.Convert(arg.Expression, typeof(object))
        : arg.Expression);

    private static Expression[] GetArgArray(DynamicMetaObject[] args) =>
        [Expression.NewArrayInit(typeof(object), GetArgs(args))];

    private static Expression[] GetArgArray(DynamicMetaObject[] args, DynamicMetaObject value) =>
    [
        Expression.NewArrayInit(typeof(object), GetArgs(args)),
        value.Expression.Type.IsValueType ? Expression.Convert(value.Expression, typeof(object)) : value.Expression
    ];

    private static ConstantExpression Constant(DynamicMetaObjectBinder binder)
    {
        Type t = binder.GetType();
        while (!t.IsVisible) t = t.BaseType!;
        return Expression.Constant(binder, t);
    }
    private DynamicMetaObject CallMethodWithResult(string methodName, DynamicMetaObjectBinder binder, IEnumerable<Expression> args, Fallback fallback, Fallback? fallbackInvoke = null)
    {
        var fallbackResult = fallback(null!);
        return BuildCallMethodWithResult(methodName, binder, args, fallbackResult, fallbackInvoke);
    }

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


    private BindingRestrictions GetRestrictions() =>
        (Value == null && HasValue)
            ? BindingRestrictions.GetInstanceRestriction(Expression, null)
            : BindingRestrictions.GetTypeRestriction(Expression, typeof(T));

    public override IEnumerable<string> GetDynamicMemberNames() => _proxy.GetDynamicMemberNames((T)Value!);

    private delegate DynamicMetaObject Fallback(DynamicMetaObject errorSuggestion);

    private sealed class GetBinderAdapter(InvokeMemberBinder binder)
        : GetMemberBinder(binder.Name, binder.IgnoreCase)
    {
        public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject? errorSuggestion)
            => throw new NotSupportedException();
    }
}