using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Query;

/// <summary>
/// A successful result's reason saying a provider translated part of the query only approximately, so its hits may
/// differ from Elasticsearch's. Find it with <c>result.Successes.OfType&lt;TranslationWarning&gt;()</c>.
/// </summary>
public sealed class TranslationWarning : Success
{
    /// <summary>Creates a warning with <paramref name="message"/>.</summary>
    /// <param name="message">What was translated only approximately, and how.</param>
    public TranslationWarning(string message) : base(message)
    {
    }
}
