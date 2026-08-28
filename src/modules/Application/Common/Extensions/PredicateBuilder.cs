using System.Linq.Expressions;

namespace KH.Application.Common.Extensions;

/// <summary>
/// Combines predicate expressions so a filter whose shape is only known at runtime still reaches
/// the database as one <c>WHERE</c>.
///
/// Needed because <c>IQueryable</c> cannot express "OR together one clause per element of this
/// in-memory list" in plain LINQ — <c>list.Any(x => ...)</c> over a closure has no SQL translation.
/// The alternative, a <c>UNION</c> per element, would turn one filtered read into several, which is
/// exactly the shape the survey worklist was tuned away from.
/// </summary>
public static class PredicateBuilder
{
    /// <summary>A predicate that matches nothing — the identity for <see cref="Or"/>.</summary>
    public static Expression<Func<T, bool>> False<T>() => _ => false;

    /// <summary>A predicate that matches everything — the identity for <see cref="And"/>.</summary>
    public static Expression<Func<T, bool>> True<T>() => _ => true;

    public static Expression<Func<T, bool>> Or<T>(
        this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right) =>
        Combine(left, right, Expression.OrElse);

    public static Expression<Func<T, bool>> And<T>(
        this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right) =>
        Combine(left, right, Expression.AndAlso);

    /// <summary>
    /// Rebinds <paramref name="right"/> onto <paramref name="left"/>'s parameter before joining
    /// them. Two separately written lambdas have two distinct parameter instances, and an expression
    /// tree referencing a parameter its own lambda does not declare fails at translation time.
    /// </summary>
    private static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> join)
    {
        var parameter = left.Parameters[0];
        var rebound = new ParameterRebinder(right.Parameters[0], parameter).Visit(right.Body);

        return Expression.Lambda<Func<T, bool>>(join(left.Body, rebound), parameter);
    }

    private sealed class ParameterRebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
