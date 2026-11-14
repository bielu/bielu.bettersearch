using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.PostgreSql.DepedencyInjection;
using Bielu.BetterSearch.PostgreSql.Services;
using Bielu.BetterSearch.Tests;
using Npgsql;

namespace Bielu.BetterSearch.PostgreSql.Tests;

public class PostgreSqlDepedenyInjectionTests(IServiceProvider collection)
    : DepedenyInjectionTestBase<PostgreSqlIndexingProviderAsync, NpgsqlDataSource, PostgreSqlSearchRequest>(collection)
{
    protected override void AddProvider(Configurator search) => search.AddPostgreSql();

    protected override void AddProvider(Configurator search, string key) => search.AddPostgreSql(key);

    [Fact]
    public void SearchProviderShouldBeTypeOfPostgreSqlSearchProviderAsync() => ServiceShouldBeTypeOf(typeof(ISearchProviderAsync), typeof(PostgreSqlSearchProviderAsync));

    [Fact]
    public void IndexingProviderShouldBeTypeOfPostgreSqlIndexingProviderAsync() => ServiceShouldBeTypeOf(typeof(IIndexingProviderAsync), typeof(PostgreSqlIndexingProviderAsync));
}
