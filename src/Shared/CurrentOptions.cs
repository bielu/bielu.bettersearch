using FluentResults;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Services;

internal static class CurrentOptions
{
    /// <summary>
    /// The monitor's current options, or a failed <see cref="Result"/> that lists every failure when the options
    /// validator rejects them, as it does after a configuration reload to invalid settings.
    /// </summary>
    public static Result<T> TryCurrentValue<T>(this IOptionsMonitor<T> optionsMonitor)
    {
        try
        {
            return Result.Ok(optionsMonitor.CurrentValue);
        }
        catch (OptionsValidationException e)
        {
            return Result.Fail<T>(new Error(e.Message).CausedBy(e));
        }
    }
}
