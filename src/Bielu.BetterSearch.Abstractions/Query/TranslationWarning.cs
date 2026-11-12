using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Query;

/// <summary>
/// A successful result's reason saying a provider translated part of the query only approximately, so its hits may
/// differ from Elasticsearch's. Find it with <c>result.Successes.OfType&lt;TranslationWarning&gt;()</c>.
/// </summary>
public sealed class TranslationWarning : Success
{
    public TranslationWarning(string message) : base(message)
    {
    }
}
