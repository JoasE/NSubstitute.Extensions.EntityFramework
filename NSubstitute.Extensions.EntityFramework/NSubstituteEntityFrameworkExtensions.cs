using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace NSubstitute.Extensions.EntityFramework
{
    public static class NSubstituteEntityFrameworkExtensions
    {
        public static DbSet<TEntity> WithData<TEntity>(this DbSet<TEntity> mock, IEnumerable<TEntity> data)
            where TEntity : class
        {
            var queryable = data.AsQueryable();

            ((IQueryable<TEntity>)mock).Expression.Returns(queryable.Expression);
            ((IQueryable<TEntity>)mock).ElementType.Returns(queryable.ElementType);
            ((IQueryable<TEntity>)mock).GetEnumerator().Returns(queryable.GetEnumerator());

            ((IDbAsyncEnumerable<TEntity>)mock).GetAsyncEnumerator()
                .Returns(new TestDbAsyncEnumerator<TEntity>(queryable.GetEnumerator()));
            ((IQueryable<TEntity>)mock).Provider.Returns(new TestDbAsyncQueryProvider<TEntity>(queryable.Provider));

            return mock;
        }

        private static readonly MethodInfo returnsMethod = typeof(SubstituteExtensions).GetMethods().First(x => x.Name == nameof(SubstituteExtensions.Returns) && x.GetParameters().Length == 3);

        public static TContext WithSet<TContext, TEntity>(this TContext context,
            Expression<Func<TContext, DbSet<TEntity>>> propertySelector, IEnumerable<TEntity> data)
            where TContext : DbContext
            where TEntity : class
        {
            var mockSet = Substitute.For<DbSet<TEntity>, IQueryable<TEntity>, IDbAsyncEnumerable<TEntity>>().WithData(data);

            // TContext x
            var parameter = propertySelector.Parameters.First();

            // x.DbSet
            var dbSetPropertyExpression = (MemberExpression)propertySelector.Body;

            // x.DbSet.Returns(mockSet)
            var returnsCall = Expression.Call(returnsMethod.MakeGenericMethod(typeof(DbSet<TEntity>)), dbSetPropertyExpression, Expression.Constant(mockSet), Expression.Constant(null, typeof(DbSet<TEntity>[])));

            // Invoke the newly build lambda, and discard it
            Expression.Lambda<Action<TContext>>(returnsCall, parameter).Compile()(context);

            return context;
        }
    }
}
