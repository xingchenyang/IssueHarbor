namespace IssueHarbor.Analyzer.Storage;

public static class StorageIds
{
    public static Guid NewVersion7() => Guid.CreateVersion7();

    public static string ToCanonicalLowercase(Guid id)
    {
        var value = id.ToString("D").ToLowerInvariant();

        if (value[14] != '7' || value[19] is not ('8' or '9' or 'a' or 'b'))
        {
            throw new ArgumentException("Storage identifiers must be UUID version 7 values.", nameof(id));
        }

        return value;
    }
}
