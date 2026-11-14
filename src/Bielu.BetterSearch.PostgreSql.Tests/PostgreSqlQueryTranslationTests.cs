using System.Collections;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.PostgreSql.Services;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.PostgreSql.Tests;

// Internal, as PostgreSqlFilter is.
internal sealed class PostgreSqlQueryTranslationTests(IEnumerable<ISubQueryTranslator<PostgreSqlFilter>> translators)
    : SubQueryTranslationTestBase<PostgreSqlFilter>(translators)
{
    /// <summary>The SQL and every parameter value, as they are sent.</summary>
    protected override string Render(PostgreSqlFilter translated)
    {
        var parameters = new SqlParameters("simple");
        var sql = translated.Render(parameters);
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
}
