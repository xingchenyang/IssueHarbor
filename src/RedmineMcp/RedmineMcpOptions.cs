using System.Globalization;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace IssueHarbor.RedmineMcp;

internal sealed record RedmineMcpOptions(
    Uri BaseUri,
    string ApiKey,
    TimeSpan Timeout,
    long MaxAttachmentBytes)
{
    internal const int DefaultTimeoutSeconds = 30;
    internal const long DefaultMaxAttachmentBytes = 20 * 1024 * 1024;

    internal static RedmineMcpOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var rawBaseUrl = configuration["ISSUEHARBOR_REDMINE_BASE_URL"];
        if (!TryCreateBaseUri(rawBaseUrl, out var baseUri))
        {
            throw new InvalidOperationException("ISSUEHARBOR_REDMINE_BASE_URL must be an absolute HTTP or HTTPS URL without userinfo, query, or fragment.");
        }

        var apiKey = configuration["ISSUEHARBOR_REDMINE_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains('\r') || apiKey.Contains('\n'))
        {
            throw new InvalidOperationException("ISSUEHARBOR_REDMINE_API_KEY must be configured as a non-empty single-line secret.");
        }

        var timeout = ParsePositiveInt(
            configuration["ISSUEHARBOR_REDMINE_TIMEOUT_SECONDS"],
            DefaultTimeoutSeconds,
            "ISSUEHARBOR_REDMINE_TIMEOUT_SECONDS");
        var maxAttachmentBytes = ParsePositiveLong(
            configuration["ISSUEHARBOR_REDMINE_MAX_ATTACHMENT_BYTES"],
            DefaultMaxAttachmentBytes,
            "ISSUEHARBOR_REDMINE_MAX_ATTACHMENT_BYTES");

        return new RedmineMcpOptions(baseUri!, apiKey, TimeSpan.FromSeconds(timeout), maxAttachmentBytes);
    }

    internal static bool TryCreateBaseUri(string? value, out Uri? baseUri)
    {
        baseUri = null;
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(parsed.Host) ||
            !string.IsNullOrEmpty(parsed.UserInfo) ||
            HasUserInfoSyntax(value) ||
            !string.IsNullOrEmpty(parsed.Query) ||
            !string.IsNullOrEmpty(parsed.Fragment) ||
            value.Contains('?') ||
            value.Contains('#'))
        {
            return false;
        }

        var normalized = new UriBuilder(parsed)
        {
            Path = parsed.AbsolutePath.TrimEnd('/') + "/",
            Query = string.Empty,
            Fragment = string.Empty,
            UserName = string.Empty,
            Password = string.Empty,
        }.Uri;

        baseUri = normalized;
        return true;
    }

    internal static bool HasUserInfoSyntax(string absoluteUrl)
    {
        var schemeSeparator = absoluteUrl.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparator < 0)
        {
            return false;
        }

        var authorityStart = schemeSeparator + 3;
        var authorityEnd = absoluteUrl.IndexOfAny(['/', '?', '#'], authorityStart);
        if (authorityEnd < 0)
        {
            authorityEnd = absoluteUrl.Length;
        }

        return absoluteUrl.AsSpan(authorityStart, authorityEnd - authorityStart).Contains('@');
    }

    internal static SocketsHttpHandler CreatePrimaryHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
    };

    internal static HttpClient CreateHttpClient(RedmineMcpOptions options) =>
        new(CreatePrimaryHandler())
        {
            BaseAddress = options.BaseUri,
            Timeout = options.Timeout,
        };

    private static int ParsePositiveInt(string? value, int defaultValue, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            throw new InvalidOperationException($"{settingName} must be a positive integer.");
        }

        return parsed;
    }

    private static long ParsePositiveLong(string? value, long defaultValue, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            throw new InvalidOperationException($"{settingName} must be a positive integer.");
        }

        return parsed;
    }
}
