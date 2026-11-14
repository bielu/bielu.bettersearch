using Npgsql;

namespace Bielu.BetterSearch.PostgreSql.Services;

/// <summary>The PostgreSQL errors the provider turns into answers rather than failures, and the messages it fails with.</summary>
internal static class PostgreSqlCommands
{
    /// <summary>The table, so the index, does not exist.</summary>
    public static bool IsMissingTable(Exception e) => e is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable };

    /// <summary>
    /// The table already exists: <c>duplicate_table</c>, or the unique violation on its row type that a concurrent
    /// <c>CREATE TABLE</c> of the same name gets.
    /// </summary>
    public static bool IsExistingTable(Exception e) =>
        e is PostgresException { SqlState: PostgresErrorCodes.DuplicateTable or PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.DuplicateObject };

    /// <summary>A function, such as <c>levenshtein</c>, is not installed.</summary>
    public static bool IsMissingFunction(Exception e) => e is PostgresException { SqlState: PostgresErrorCodes.UndefinedFunction };

    public const string MissingFuzzyExtension =
        "FuzzyQuery needs the fuzzystrmatch extension, which this database does not have: run CREATE EXTENSION fuzzystrmatch; in it.";

    public static string Describe(Exception e) => e is PostgresException postgres
        ? $"{postgres.MessageText} (SQLSTATE {postgres.SqlState})"
        : e.Message;

    public static NpgsqlCommand Command(NpgsqlConnection connection, string sql, SqlParameters? parameters = null, NpgsqlTransaction? transaction = null)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        parameters?.AddTo(command);
        return command;
    }
}
