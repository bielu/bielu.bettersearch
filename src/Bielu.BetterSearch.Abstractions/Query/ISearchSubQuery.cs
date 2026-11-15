namespace Bielu.BetterSearch.Abstractions.Query;

public interface ISearchSubQuery
{
    Occurrence Occurrence { get; set; }
    int BoostValue { get; set; }

}
