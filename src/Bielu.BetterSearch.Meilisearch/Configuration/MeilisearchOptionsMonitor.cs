using FluentResults;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Meilisearch.Configuration;

internal static class MeilisearchOptionsMonitor
{
    /// <summary>
    /// The current options, or a failed <c>Result</c> naming the invalid settings. The validator runs whenever the
    /// options are read, so after a configuration reload with an invalid value <c>CurrentValue</c> throws instead.
    /// </summary>
    public static Result<MeilisearchOptions> Current(this IOptionsMonitor<MeilisearchOptions> optionsMonitor)
    {
        try
        {
            return Result.Ok(optionsMonitor.CurrentValue);
        }
        catch (OptionsValidationException e)
        {
            return Result.Fail<MeilisearchOptions>(new Error($"The Meilisearch options are invalid: {e.Message}").CausedBy(e));
        }
    }
}
