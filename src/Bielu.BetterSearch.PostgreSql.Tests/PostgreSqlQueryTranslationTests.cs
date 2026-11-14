using System.Collections;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.PostgreSql.Services;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Tests;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.PostgreSql.Tests;

/// <summary>
/// The shared translation tests. xUnit runs public classes only, and <see cref="PostgreSqlFilter"/> is internal, so
/// the class runs them over <see cref="object"/>, with each registered translator behind an adapter.
/// </summary>
public sealed class PostgreSqlQueryTranslationTests(IServiceProvider services)
    : SubQueryTranslationTestBase<object>(Adapt(services))
{
    private static List<ISubQueryTranslator<object>> Adapt(IServiceProvider services)
    {
        var translators = services.GetRequiredService<IEnumerable<ISubQueryTranslator<PostgreSqlFilter>>>().ToList();
        return translators.Select(translator => (ISubQueryTranslator<object>)new ObjectTranslator(translator, translators)).ToList();
    }

    /// <summary>The SQL and every parameter value, as they are sent.</summary>
    protected override string Render(object translated)
    {
        var parameters = new SqlParameters("simple");
        var sql = ((PostgreSqlFilter)translated).Render(parameters);
        return string.Join(" | ", parameters.Values.Select(parameter => parameter.Value switch
        {
            string text => text,
            IEnumerable values => string.Join(", ", values.Cast<object>().Select(QueryValueText.From)),
            var value => QueryValueText.From(value)
        }).Prepend(sql));
    }

    // PostgreSQL has no Lucene syntax.
    public override async Task LuceneSubQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new LuceneSubQuery { LuceneQuery = "Name:jane" });

    /// <summary>A translator as an <see cref="ISubQueryTranslator{T}"/> of <see cref="object"/>, translating nested queries with every translator.</summary>
    private sealed class ObjectTranslator(ISubQueryTranslator<PostgreSqlFilter> translator, IReadOnlyList<ISubQueryTranslator<PostgreSqlFilter>> all)
        : ISubQueryTranslator<object>
    {
        public bool CanTranslate(ISearchSubQuery query) => translator.CanTranslate(query);

        public async Task<Result<object>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<object>> subQueryTranslators)
        {
            var result = await translator.TranslateAsync(query, all);
            return result.IsFailed ? result.ToResult<object>() : Result.Ok<object>(result.Value).WithSuccesses(result.Successes);
        }
    }
}
