using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent.RangeQueries;

/// <summary>Configures a <see cref="DateRangeSubQuery"/>.</summary>
public class DateRangeQueryConfigurator : SubQueryConfigurator<DateRangeQueryConfigurator>
{
    /// <summary>Creates a configurator for a new <see cref="DateRangeSubQuery"/>.</summary>
    public DateRangeQueryConfigurator()
    {
        Query = new DateRangeSubQuery();
    }

    /// <summary>The range sub-query this configurator builds.</summary>
    public DateRangeSubQuery Query { get; set; }
    /// <inheritdoc />
    protected override ISearchSubQuery SubQuery => Query;
    /// <summary>Sets the <c>Field</c> the sub-query matches on.</summary>
    /// <param name="fieldName">The field name.</param>
    /// <returns>This configurator, for chaining.</returns>
    public DateRangeQueryConfigurator WithField(string fieldName)
    {
        Query.Field = fieldName;
        return this;
    }
    /// <summary>The minimum: a <see cref="DateTime"/> or a <see cref="DateTimeOffset"/>; an unspecified <see cref="DateTime"/> is UTC.</summary>
    public DateRangeQueryConfigurator WithMinValue(DateRangeValue value, bool includeEdge = true)
    {
        Query.MinValue = value;
        Query.IncludeMinEdge = includeEdge;
        return this;
    }
    /// <summary>The maximum: a <see cref="DateTime"/> or a <see cref="DateTimeOffset"/>; an unspecified <see cref="DateTime"/> is UTC.</summary>
    public DateRangeQueryConfigurator WithMaxValue(DateRangeValue value, bool includeEdge = true)
    {
        Query.MaxValue = value;
        Query.IncludeMaxEdge = includeEdge;
        return this;
    }
}
