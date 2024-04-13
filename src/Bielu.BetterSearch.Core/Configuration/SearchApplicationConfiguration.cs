namespace Bielu.BetterSearch.Configuration;

public class SearchApplicationConfiguration
{
    private static SearchApplicationConfiguration _currentInstance;
    public static SearchApplicationConfiguration CurrentInstance
    {
        get
        {
            if (_currentInstance == null)
            {
                _currentInstance = new SearchApplicationConfiguration();
            }
            return _currentInstance;
        }
    }
    public Type DocumentValidatorType { get; set; }
    public Type IndexingServiceType { get; set; }
}
