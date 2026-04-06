namespace Bielu.BetterSearch.Extensions;

public static class TaskExtensions
{
    public static async Task<IEnumerable<TResult>> SelectAsync<TSource,TResult>(
        this IEnumerable<TSource> source, Func<TSource, Task<TResult>> method)
    {
        return await Task.WhenAll(source.Select(async s => await method(s)));
    }
}
