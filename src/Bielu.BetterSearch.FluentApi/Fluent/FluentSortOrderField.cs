using Bielu.BetterSearch.Abstractions.Fields;

namespace Bielu.BetterSearch.FluentApi.Fluent;

/// <summary>The sort entry <see cref="QueryCreatorConfigurator.SortBy"/> adds.</summary>
internal sealed class FluentSortOrderField : ISortOrderField
{
    public string FieldName { get; set; } = string.Empty;
    public bool Desc { get; set; }
    public int Order { get; set; }
}
