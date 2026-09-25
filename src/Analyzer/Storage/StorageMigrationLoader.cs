using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace IssueHarbor.Analyzer.Storage;

internal sealed record StorageMigration(int Version, string Name, string Sql, string Sha256);

internal static partial class StorageMigrationLoader
{
    [GeneratedRegex("^(?<version>[0-9]{3})_(?<name>[a-z0-9][a-z0-9_-]*)\\.sql$", RegexOptions.CultureInvariant)]
    private static partial Regex MigrationNamePattern();

    public static IReadOnlyList<StorageMigration> Load(string migrationsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationsDirectory);

        if (!Directory.Exists(migrationsDirectory))
        {
            throw new InvalidOperationException("The storage migration directory is missing.");
        }

        var migrations = new List<StorageMigration>();
        foreach (var path in Directory.EnumerateFiles(migrationsDirectory, "*.sql", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(path);
            var match = MigrationNamePattern().Match(fileName);
            if (!match.Success)
            {
                throw new InvalidOperationException("A storage migration file has an invalid name.");
            }

            var version = int.Parse(match.Groups["version"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
            var name = Path.GetFileNameWithoutExtension(fileName);
            var sql = File.ReadAllText(path, new UTF8Encoding(false, true));
            if (sql.Length == 0)
            {
                throw new InvalidOperationException("A storage migration file is empty.");
            }

            var canonicalSql = sql.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var canonicalBytes = Encoding.UTF8.GetBytes(canonicalSql);
            migrations.Add(new StorageMigration(
                version,
                name,
                canonicalSql,
                Convert.ToHexString(SHA256.HashData(canonicalBytes)).ToLowerInvariant()));
        }

        var ordered = migrations.OrderBy(migration => migration.Version).ToArray();
        if (ordered.Length == 0)
        {
            throw new InvalidOperationException("No storage migrations were found.");
        }

        for (var index = 0; index < ordered.Length; index++)
        {
            if (ordered[index].Version != index + 1)
            {
                throw new InvalidOperationException("Storage migration versions must be unique and contiguous from 001.");
            }
        }

        return Array.AsReadOnly(ordered);
    }
}
