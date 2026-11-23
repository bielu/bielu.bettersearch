using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent.RangeQueries;

/// <summary>Configures a <see cref="StringRangeSubQuery"/>.</summary>
public class StringRangeQueryConfigurator : SubQueryConfigurator<StringRangeQueryConfigurator>
{
    /// <summary>Creates a configurator for a new <see cref="StringRangeSubQuery"/>.</summary>
    public StringRangeQueryConfigurator()
    {
        Query = new StringRangeSubQuery();
    }

    /// <summary>The range sub-query this configurator builds.</summary>
    public StringRangeSubQuery Query { get; set; }
    /// <inheritdoc />
    protected override ISearchSubQuery SubQuery => Query;
    /// <summary>Sets the <c>Field</c> the sub-query matches on.</summary>
    /// <param name="fieldName">The field name.</param>
    /// <returns>This configurator, for chaining.</returns>
    public StringRangeQueryConfigurator WithField(string fieldName)
    {
        Query.Field = fieldName;
        return this;
    }
    /// <summary>Sets the <see cref="StringRangeSubQuery.MinValue"/> and whether the range includes it (<c>IncludeMinEdge</c>).</summary>
    /// <param name="value">The lower bound.</param>
    /// <param name="includeEdge"><c>true</c> to include the bound in the range.</param>
    /// <returns>This configurator, for chaining.</returns>
    public StringRangeQueryConfigurator WithMinValue(string value, bool includeEdge = true)
    {
        Query.MinValue = value;
        Query.IncludeMinEdge = includeEdge;
        return this;
    }
    /// <summary>Sets the <see cref="StringRangeSubQuery.MaxValue"/> and whether the range includes it (<c>IncludeMaxEdge</c>).</summary>
    /// <param name="value">The upper bound.</param>
    /// <param name="includeEdge"><c>true</c> to include the bound in the range.</param>
    /// <returns>This configurator, for chaining.</returns>
    public StringRangeQueryConfigurator WithMaxValue(string value, bool includeEdge = true)
    {
        Query.MaxValue = value;
        Query.IncludeMaxEdge = includeEdge;
        return this;
    }
}
