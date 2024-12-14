using System.Text;
using Microsoft.Data.Sqlite;

namespace AnkiNet.CollectionFile.Database;

internal abstract class SqliteRepository<T>
{
    private readonly SqliteConnection _connection;

    protected abstract string TableName { get; }
    protected abstract IReadOnlyList<string> Columns { get; }
    protected abstract IReadOnlyList<object> GetValues(T item);

    protected abstract T Map(SqliteDataReader reader);

    protected SqliteRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<T>> ReadAll()
    {
        var result = new List<T>();

        var readAllSqlQuery = $"SELECT {string.Join(",", Columns)} FROM {TableName}";

        try
        {
            await using var command = new SqliteCommand(readAllSqlQuery, _connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var item = Map(reader);
                result.Add(item);
            }
        }
        catch (Exception e)
        {
            throw new IOException($"Cannot ReadAll {typeof(T).Name}", e);
        }

        return result;
    }

    public async Task Add(IReadOnlyList<T> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var columnNamesString = string.Join(",", Columns);
        var parametersPerRow = Columns.Count;

        var placeholderBuilder = new StringBuilder();
        for (int i = 0; i < parametersPerRow; i++)
        {
            placeholderBuilder.Append($"@p{i},");
        }

        placeholderBuilder.Length--; // Remove the last comma
        var placeholder = $"({placeholderBuilder})";

        var insertStatement = $"INSERT INTO {TableName} ({columnNamesString}) VALUES {placeholder}";

        using var transaction = _connection.BeginTransaction();

        await using var command = new SqliteCommand(insertStatement, _connection, transaction);

        var parameters = new List<SqliteParameter>();
        for (int i = 0; i < parametersPerRow; i++)
        {
            parameters.Add(new SqliteParameter { ParameterName = $"@p{i}" });
        }

        command.Parameters.AddRange(parameters);

        foreach (var item in items)
        {
            var itemValues = GetValues(item);

            for (int i = 0; i < itemValues.Count; i++)
            {
                command.Parameters[i].Value = itemValues[i] ?? DBNull.Value;
            }

            await command.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }
}