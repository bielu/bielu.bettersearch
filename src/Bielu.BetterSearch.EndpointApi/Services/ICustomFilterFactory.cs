using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.EndpointApi.Models;

namespace Bielu.BetterSearch.EndpointApi.Services;

public interface ICustomFilterFactory
{
    public IFilter CreateFilter(string filterType);
    public void ApplyFilter(ISearchQuery query, IFilter filter);
}
