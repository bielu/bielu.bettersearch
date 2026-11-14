using FluentResults;

namespace Bielu.BetterSearch.Typesense.Services;

/// <summary>
/// A filter the translators build, rendered into Typesense's <c>filter_by</c> once the collection's fields are known.
/// Typesense fails a filter that names a field no document has, and has no <c>NOT</c> for a whole expression, so
/// rendering replaces a missing field by the answer it would give (no match) and pushes each negation down to the
/// comparisons, where <c>:!=</c> negates a match on any value, as Lucene's <c>MUST_NOT</c> does.
/// </summary>
internal abstract record TypesenseFilter
{
    public static readonly TypesenseFilter MatchAll = new Constant(true);
    public static readonly TypesenseFilter MatchNone = new Constant(false);

    /// <summary>
    /// The <c>filter_by</c> text, or <c>null</c> when the filter matches every document. A filter that matches no
    /// document is <see cref="Rendered.None"/>, which the search answers without asking Typesense.
    /// </summary>
    public Result<Rendered> Render(Func<string, bool> fieldExists) => Render(fieldExists, false);

    protected abstract Result<Rendered> Render(Func<string, bool> fieldExists, bool negate);

    /// <summary>The fields the filter names, to check against the collection before rendering.</summary>
    public abstract IEnumerable<string> FieldNames();

    /// <summary>A comparison on one Typesense field, with its negation, or why it can't be negated.</summary>
    public sealed record Comparison(string Field, string Expression, string? Negated, string? NotNegatable = null) : TypesenseFilter
    {
        protected override Result<Rendered> Render(Func<string, bool> fieldExists, bool negate)
        {
            if (!fieldExists(Field))
            {
                // No document has the field, so the comparison matches none of them and its negation all of them.
                return Result.Ok(negate ? Rendered.All : Rendered.None);
            }

            if (!negate)
            {
                return Result.Ok(Rendered.Of(Expression));
            }

            return Negated != null
                ? Result.Ok(Rendered.Of(Negated))
                : Result.Fail<Rendered>(NotNegatable ?? $"Typesense filters cannot negate '{Expression}'.");
        }

        public override IEnumerable<string> FieldNames() => [Field];
    }

    public sealed record And(IReadOnlyList<TypesenseFilter> Clauses) : TypesenseFilter
    {
        protected override Result<Rendered> Render(Func<string, bool> fieldExists, bool negate) =>
            Combine(Clauses, fieldExists, negate, all: !negate);

        public override IEnumerable<string> FieldNames() => Clauses.SelectMany(clause => clause.FieldNames());
    }

    public sealed record Or(IReadOnlyList<TypesenseFilter> Clauses) : TypesenseFilter
    {
        protected override Result<Rendered> Render(Func<string, bool> fieldExists, bool negate) =>
            Combine(Clauses, fieldExists, negate, all: negate);

        public override IEnumerable<string> FieldNames() => Clauses.SelectMany(clause => clause.FieldNames());
    }

    public sealed record Not(TypesenseFilter Clause) : TypesenseFilter
    {
        protected override Result<Rendered> Render(Func<string, bool> fieldExists, bool negate) =>
            Clause.Render(fieldExists, !negate);

        public override IEnumerable<string> FieldNames() => Clause.FieldNames();
    }

    public sealed record Constant(bool Matches) : TypesenseFilter
    {
        protected override Result<Rendered> Render(Func<string, bool> fieldExists, bool negate) =>
            Result.Ok(Matches != negate ? Rendered.All : Rendered.None);

        public override IEnumerable<string> FieldNames() => [];
    }

    /// <summary>
    /// Joins the clauses, each negated when <paramref name="negate"/> is set: with <c>&amp;&amp;</c> when
    /// <paramref name="all"/>, else with <c>||</c>. By De Morgan, a negated <c>AND</c> is an <c>OR</c> of the negations.
    /// </summary>
    private static Result<Rendered> Combine(IReadOnlyList<TypesenseFilter> clauses, Func<string, bool> fieldExists, bool negate, bool all)
    {
        var parts = new List<string>();
        foreach (var clause in clauses)
        {
            var rendered = clause.Render(fieldExists, negate);
            if (rendered.IsFailed)
            {
                return rendered;
            }

            switch (rendered.Value)
            {
                case { MatchesAll: true }:
                    if (!all)
                    {
                        return Result.Ok(Rendered.All);
                    }

                    break;
                case { MatchesNone: true }:
                    if (all)
                    {
                        return Result.Ok(Rendered.None);
                    }

                    break;
                default:
                    parts.Add(rendered.Value.Expression!);
                    break;
            }
        }

        return parts.Count switch
        {
            0 => Result.Ok(all ? Rendered.All : Rendered.None),
            1 => Result.Ok(Rendered.Of(parts[0])),
            _ => Result.Ok(Rendered.Of(string.Join(all ? " && " : " || ", parts.Select(part => $"({part})"))))
        };
    }
}

/// <summary>A rendered filter: <c>filter_by</c> text, or a filter that matches every document or none.</summary>
internal sealed record Rendered(string? Expression, bool MatchesNone)
{
    public static readonly Rendered All = new(null, false);
    public static readonly Rendered None = new(null, true);

    public bool MatchesAll => Expression == null && !MatchesNone;

    public static Rendered Of(string expression) => new(expression, false);
}
