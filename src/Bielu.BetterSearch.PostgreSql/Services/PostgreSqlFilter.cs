using Npgsql;
using NpgsqlTypes;

namespace Bielu.BetterSearch.PostgreSql.Services;

/// <summary>
/// A filter the translators build, rendered into a SQL condition on the row <c>d</c> of an index's table. Every
/// leaf renders to a condition that is true or false, never <c>NULL</c>, so <c>NOT</c> negates it as Lucene's
/// <c>MUST_NOT</c> does: a document without the field matches the negation.
/// </summary>
internal abstract record PostgreSqlFilter
{
    public static readonly PostgreSqlFilter MatchAll = new Constant(true);
    public static readonly PostgreSqlFilter MatchNone = new Constant(false);

    public abstract string Render(SqlParameters parameters);

    /// <summary>A condition built from parameters; <c>Sql</c> must never return <c>NULL</c>.</summary>
    public sealed record Condition(string Description, Func<SqlParameters, string> Sql) : PostgreSqlFilter
    {
        public override string Render(SqlParameters parameters) => Sql(parameters);
    }

    public sealed record And(IReadOnlyList<PostgreSqlFilter> Clauses) : PostgreSqlFilter
    {
        public override string Render(SqlParameters parameters) => Join(Clauses, parameters, " AND ", "TRUE");
    }

    public sealed record Or(IReadOnlyList<PostgreSqlFilter> Clauses) : PostgreSqlFilter
    {
        public override string Render(SqlParameters parameters) => Join(Clauses, parameters, " OR ", "FALSE");
    }

    public sealed record Not(PostgreSqlFilter Clause) : PostgreSqlFilter
    {
        public override string Render(SqlParameters parameters) => $"NOT ({Clause.Render(parameters)})";
    }

    public sealed record Constant(bool Matches) : PostgreSqlFilter
    {
        public override string Render(SqlParameters parameters) => Matches ? "TRUE" : "FALSE";
    }

    private static string Join(IReadOnlyList<PostgreSqlFilter> clauses, SqlParameters parameters, string separator, string empty) =>
        clauses.Count == 0 ? empty : $"({string.Join(separator, clauses.Select(clause => clause.Render(parameters)))})";
}

/// <summary>The positional parameters of one command, <c>$1</c> first, with the text search configuration as <c>$1</c>.</summary>
internal sealed class SqlParameters
{
    private readonly List<NpgsqlParameter> _parameters = [];

    public SqlParameters(string textSearchConfiguration)
    {
        Configuration = $"{Add(textSearchConfiguration, NpgsqlDbType.Text)}::regconfig";
    }

    /// <summary>The text search configuration, as a <c>regconfig</c>.</summary>
    public string Configuration { get; }

    public IReadOnlyList<NpgsqlParameter> Values => _parameters;

    /// <summary>Adds <paramref name="value"/> and returns its placeholder, such as <c>$2</c>.</summary>
    public string Add(object value, NpgsqlDbType type)
    {
        _parameters.Add(new NpgsqlParameter { Value = value, NpgsqlDbType = type });
        return $"${_parameters.Count}";
    }

    /// <summary>The field's value list, as a <c>jsonb</c> array, or <c>NULL</c> when the document has no such field.</summary>
    public string Field(string field) => $"(d.fields -> {Add(field, NpgsqlDbType.Text)})";

    /// <summary>The elements of the field's value list, none when the document has no such field or stored it as <c>null</c>.</summary>
    public string Elements(string field)
    {
        var value = Field(field);
        return $"jsonb_array_elements(CASE WHEN jsonb_typeof({value}) = 'array' THEN {value} ELSE '[]'::jsonb END)";
    }

    /// <summary>The words of the field's string values, as the table's <c>search</c> column holds them for the whole document.</summary>
    public string Words(string field) => $"jsonb_to_tsvector({Configuration}, coalesce({Field(field)}, '[]'::jsonb), '[\"string\"]')";

    public void AddTo(NpgsqlCommand command)
    {
        foreach (var parameter in _parameters)
        {
            command.Parameters.Add(parameter.Clone());
        }
    }
}
