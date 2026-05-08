using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using Gameloop.Vdf.Linq;
using Gameloop.Vdf.Utilities;
using Microsoft.CSharp.RuntimeBinder;
using Xunit;

namespace Gameloop.Vdf.Tests;

public class DynamicProxyTests
{
    #region Test Mocks

    public class ProxyTarget : IDynamicMetaObjectProvider
    {
        public object? SetValue { get; set; }
        public string? DeletedMember { get; set; }
        public DynamicMetaObject GetMetaObject(Expression parameter) =>
            new DynamicProxyMetaObject<ProxyTarget>(parameter, this, new MockProxy());
    }

    internal class MockProxy : DynamicProxy<ProxyTarget>
    {
        public override bool TryGetMember(ProxyTarget instance, GetMemberBinder binder, out object? result)
        {
            if (binder.Name == "InterceptedProp")
            {
                result = "ProxySuccess";
                return true;
            }
            result = null;
            return false;
        }

        public override bool TrySetMember(ProxyTarget instance, SetMemberBinder binder, object value)
        {
            instance.SetValue = value;
            return true;
        }

        public override bool TryDeleteMember(ProxyTarget instance, DeleteMemberBinder binder)
        {
            instance.DeletedMember = binder.Name;
            return true;
        }

        public override bool TryInvokeMember(ProxyTarget instance, InvokeMemberBinder binder, object[] args, out object? result)
        {
            result = $"Invoked {binder.Name}";
            return true;
        }

        public override IEnumerable<string> GetDynamicMemberNames(ProxyTarget instance) => ["InterceptedProp"];
    }

    #endregion

    #region Functional DLR Tests

    [Fact]
    public void Dynamic_GetMember_CallsProxyOverride()
    {
        dynamic d = new ProxyTarget();
        Assert.Equal("ProxySuccess", d.InterceptedProp);
    }

    [Fact]
    public void Dynamic_SetMember_ReturnsAssignedValueAndUpdatesTarget()
    {
        var target = new ProxyTarget();
        dynamic d = target;

        var result = (d.Health = 999);

        Assert.Equal(999, result);
        Assert.Equal(999, target.SetValue);
    }

    [Fact]
    public void Dynamic_InvokeMember_ReturnsCorrectResult()
    {
        dynamic d = new ProxyTarget();
        string result = d.Initialize();
        Assert.Equal("Invoked Initialize", result);
    }

    [Fact]
    public void Dynamic_MissingMember_FallsBackToStandardBinder()
    {
        dynamic d = new ProxyTarget();
        Assert.Throws<RuntimeBinderException>(() => d.MissingProperty);
        Assert.Throws<RuntimeBinderException>(() => (string)d);
    }

    #endregion

    #region MetaObject Binder & Restriction Tests

    [Fact]
    public void BindGetMember_DispatchesToProxyMethod()
    {
        var target = new ProxyTarget();
        var proxy = new MockProxy();
        var meta = new DynamicProxyMetaObject<ProxyTarget>(Expression.Constant(target), target, proxy);

        var binder = (GetMemberBinder)Microsoft.CSharp.RuntimeBinder.Binder.GetMember(
            Microsoft.CSharp.RuntimeBinder.CSharpBinderFlags.None, "Health", typeof(DynamicProxyTests),
            [Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo.Create(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfoFlags.None, null)]);

        var result = meta.BindGetMember(binder);
        var visitor = new MethodCallFinder(m => m.Name == "TryGetMember");
        visitor.Visit(result.Expression);

        Assert.True(visitor.Found, "The expression tree does not contain a call to 'TryGetMember'.");
    }


    private class MethodCallFinder(Func<System.Reflection.MethodInfo, bool> predicate) : ExpressionVisitor
    {
        public bool Found { get; private set; }
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (predicate(node.Method)) Found = true;
            return base.VisitMethodCall(node);
        }
    }



    [Fact]
    public void MetaObject_Restrictions_IncludesTypeRestriction()
    {
        var target = new ProxyTarget();
        var meta = (DynamicProxyMetaObject<ProxyTarget>)((IDynamicMetaObjectProvider)target)
            .GetMetaObject(Expression.Constant(target));

        Assert.Equal(typeof(ProxyTarget), meta.LimitType);
        Assert.NotEqual(BindingRestrictions.Empty, meta.Restrictions);
    }


    [Fact]
    public void BindSetMember_WhenNotOverridden_ReturnsBaseFallback()
    {
        var target = new VValue("Test");
        var meta = new DynamicProxyMetaObject<VValue>(Expression.Constant(target), target, new DynamicProxy<VValue>());

        var binder = (SetMemberBinder)Microsoft.CSharp.RuntimeBinder.Binder.SetMember(
            CSharpBinderFlags.None,
            "Health",
            typeof(DynamicProxyTests),
            [
            CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
            CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) 
            ]);

        var valueMO = new DynamicMetaObject(Expression.Constant(100), BindingRestrictions.Empty, 100);

        var result = meta.BindSetMember(binder, valueMO);

        Assert.NotNull(result);
        Assert.DoesNotContain("TrySetMember", result.Expression.ToString());
    }

    [Fact]
    public void IsOverridden_CorrectlyIdentifiesOverriddenMethods()
    {
        var target = new ProxyTarget();
        var meta = (DynamicProxyMetaObject<ProxyTarget>)target.GetMetaObject(Expression.Constant(target));
        var method = typeof(DynamicProxyMetaObject<ProxyTarget>)
            .GetMethod("IsOverridden", BindingFlags.NonPublic | BindingFlags.Instance);

        bool isGetOverridden = (bool)method!.Invoke(meta, [nameof(DynamicProxy<>.TryGetMember)])!;
        bool isBinaryOverridden = (bool)method!.Invoke(meta, [nameof(DynamicProxy<>.TryBinaryOperation)])!;

        Assert.True(isGetOverridden);
        Assert.False(isBinaryOverridden);
    }

    [Fact]
    public void GetDynamicMemberNames_ReturnsNamesFromProxy()
    {
        var target = new ProxyTarget();
        var meta = ((IDynamicMetaObjectProvider)target).GetMetaObject(Expression.Constant(target));

        Assert.Contains("InterceptedProp", meta.GetDynamicMemberNames());
    }

    #endregion
}
