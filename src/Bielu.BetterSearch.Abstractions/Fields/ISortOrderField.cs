namespace Bielu.BetterSearch.Abstractions.Fields;

public interface ISortOrderField
{
    string FieldName { get; set; }
    bool Desc { get; set; }
    int Order { get; set; }
}
