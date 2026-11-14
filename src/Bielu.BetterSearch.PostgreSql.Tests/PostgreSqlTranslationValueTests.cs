using System.Collections;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.PostgreSql.Services;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.PostgreSql.Tests;

/// <summary>
/// The values each translator sends, under a culture that writes 3.5 as "3,5". xUnit runs public test classes only,
/// so <see cref="PostgreSqlQueryTranslationTests"/>, internal as the filter type is, does not run; this class resolves
/// the translators from the container instead.
/// </summary>
public sealed class PostgreSqlTranslationValueTests(IServiceProvider services)
{
    private static readonly DateTimeOffset OffsetDate = new(2026, 11, 8, 1, 0, 0, 120, TimeSpan.FromHours(1));

    private async Task<string> RenderAsync(ISearchSubQuery subQuery)
    {
        using var scope = services.CreateScope();
        var translators = scope.ServiceProvider.GetRequiredService<IEnumerable<ISubQueryTranslator<PostgreSqlFilter>>>().ToList();
        using var _ = CultureScope.CommaDecimal();
        var result = await translators.First(t => t.CanTranslate(subQuery)).TranslateAsync(subQuery, translators);
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        var parameters = new SqlParameters("simple");
        var sql = result.Value.Render(parameters);
        return string.Join(" | ", parameters.Values.Select(parameter => parameter.Value switch
        {
            string text => text,
            IEnumerable values => string.Join(", ", values.Cast<object>().Select(QueryValueText.From)),
            var value => QueryValueText.From(value)
        }).Prepend(sql));
    }

    public static TheoryData<ISearchSubQuery> CommaDecimalQueries => new()
    {
        new TermSubQuery { Field = "Name", Value = 3.5 },
        new TermsSubQuery { Field = "Name", Value = [3.5] },
        new TermSubQuery { Field = "Name", Value = "3.5" },
        new PrefixSubQuery { Field = "Name", Value = 3.5 },
        new PrefixPhraseSubQuery { Field = "Name", Value = 3.5 },
        new FuzzyQuery { Field = "Name", Value = 3.5 },
    };

    [Theory]
    [MemberData(nameof(CommaDecimalQueries))]
    public async Task Translate_CommaDecimalCulture_SendsInvariantValue(ISearchSubQuery subQuery)
    {
        var rendered = await RenderAsync(subQuery);

        rendered.ShouldContain("3.5");
        rendered.ShouldNotContain("3,5");
    }

    public static TheoryData<ISearchSubQuery> DateQueries => new()
    {
        new TermSubQuery { Field = "Born", Value = OffsetDate },
        new TermsSubQuery { Field = "Born", Value = [OffsetDate] },
        new PrefixSubQuery { Field = "Born", Value = OffsetDate },
    };

    [Theory]
    [MemberData(nameof(DateQueries))]
    public async Task Translate_DateValue_SendsIsoDateInUtc(ISearchSubQuery subQuery)
    {
        var rendered = await RenderAsync(subQuery);

        rendered.ShouldContain("2026-11-08T00:00:00.12Z");
        rendered.ShouldNotContain("+01:00");
    }

    [Fact]
    public async Task Translate_FieldName_IsAParameterNotSqlText()
    {
        var rendered = await RenderAsync(new TermSubQuery { Field = "Name'); DROP TABLE x; --", Value = "jane" });

        rendered.Split(" | ")[0].ShouldNotContain("DROP TABLE");
        rendered.ShouldContain("| Name'); DROP TABLE x; --");
    }
}
