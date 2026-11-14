namespace Bielu.BetterSearch.Diagnostics;

/// <summary>
/// What the spans and logs of the BetterSearch services record. <c>AddBetterSearch</c> binds it to the
/// <see cref="SectionKey"/> section; set it in code with <c>services.Configure&lt;BetterSearchInstrumentationOptions&gt;</c>.
/// </summary>
public class BetterSearchInstrumentationOptions
{
    /// <summary>The configuration section <c>AddBetterSearch</c> binds these options to.</summary>
    public const string SectionKey = "Bielu:BetterSearch:Instrumentation";

    /// <summary>
    /// Whether a failure's message goes on the span status description and in the log line, and an exception is
    /// attached to the <c>Error</c> log. Off by default, because a message can quote the query: a Lucene syntax error
    /// quotes the whole query. With it off, a failure records only its <c>error.type</c>.
    /// </summary>
    public bool RecordErrorMessages { get; set; }
}
