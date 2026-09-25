using Microsoft.Data.Sqlite;

namespace IssueHarbor.Analyzer.Storage;

public sealed class SqliteConnectionFactory
{
    public SqliteConnectionFactory(string durableDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(durableDataRoot);
        DurableDataRoot = Path.GetFullPath(durableDataRoot);
        DatabasePath = Path.Combine(DurableDataRoot, "analyzer.db");
    }

    public string DurableDataRoot { get; }

    public string DatabasePath { get; }

    public SqliteConnection OpenConnection()
    {
        Directory.CreateDirectory(DurableDataRoot);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            ForeignKeys = true,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        var connection = new SqliteConnection(connectionString);

        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
