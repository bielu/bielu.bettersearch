namespace Bielu.BetterSearch.Abstractions.Query;

public interface ISearchSubQuery
{
    Occurance Occurance { get; set; }
    int BoostValue { get; set; }

}
