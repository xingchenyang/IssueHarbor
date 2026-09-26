using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using IssueHarbor.RedmineMcp;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Protocol;

namespace IssueHarbor.RedmineMcp.Tests;

[TestClass]
[DoNotParallelize]
public sealed class RedmineMcpReadPathTests
{
    private const string ApiKey = "synthetic-scope-007-api-key";
    private const string SourceText = "Synthetic public-safe issue";

    [TestMethod]
    public void ConfigurationDefaultsAndBaseUrlValidationAreDeterministic()
    {
        var options = RedmineMcpOptions.FromConfiguration(Configuration(
            ("ISSUEHARBOR_REDMINE_BASE_URL", "https://redmine.example.invalid/redmine"),
            ("ISSUEHARBOR_REDMINE_API_KEY", ApiKey)));

        Assert.AreEqual("https://redmine.example.invalid/redmine/", options.BaseUri.AbsoluteUri);
        Assert.AreEqual(TimeSpan.FromSeconds(30), options.Timeout);
        Assert.AreEqual(20L * 1024 * 1024, options.MaxAttachmentBytes);
        Assert.IsTrue(RedmineMcpOptions.TryCreateBaseUri("http://redmine.example.invalid/custom/path/", out var httpUri));
        Assert.AreEqual("/custom/path/", httpUri!.AbsolutePath);

        foreach (var invalid in new[]
                 {
                     "relative/redmine",
                     "ftp://redmine.example.invalid/",
                     "https://user:pass@redmine.example.invalid/",
                     "https://@redmine.example.invalid/",
                     "https://redmine.example.invalid/?query=1",
                     "https://redmine.example.invalid/#fragment",
                 })
        {
            Assert.IsFalse(RedmineMcpOptions.TryCreateBaseUri(invalid, out _), invalid);
        }

        Assert.IsFalse(RedmineMcpOptions.TryCreateBaseUri("https://redmine.example.invalid/?", out _));
        Assert.IsFalse(RedmineMcpOptions.TryCreateBaseUri("https://redmine.example.invalid/#", out _));
    }

    [TestMethod]
    public void ConfigurationRequiresSecretAndValidPositiveLimitsWithoutEchoingValues()
    {
        AssertThrows<InvalidOperationException>(() => RedmineMcpOptions.FromConfiguration(Configuration(
            ("ISSUEHARBOR_REDMINE_BASE_URL", "https://redmine.example.invalid/"))));

        foreach (var timeout in new[] { "0", "-1", "abc" })
        {
            var exception = AssertThrows<InvalidOperationException>(() => RedmineMcpOptions.FromConfiguration(Configuration(
                ("ISSUEHARBOR_REDMINE_BASE_URL", "https://redmine.example.invalid/"),
                ("ISSUEHARBOR_REDMINE_API_KEY", ApiKey),
                ("ISSUEHARBOR_REDMINE_TIMEOUT_SECONDS", timeout))));
            Assert.IsFalse(exception.Message.Contains(timeout, StringComparison.Ordinal));
            Assert.IsFalse(exception.Message.Contains(ApiKey, StringComparison.Ordinal));
        }

        foreach (var maxSize in new[] { "0", "-1", "not-a-number" })
        {
            AssertThrows<InvalidOperationException>(() => RedmineMcpOptions.FromConfiguration(Configuration(
                ("ISSUEHARBOR_REDMINE_BASE_URL", "https://redmine.example.invalid/"),
                ("ISSUEHARBOR_REDMINE_API_KEY", ApiKey),
                ("ISSUEHARBOR_REDMINE_MAX_ATTACHMENT_BYTES", maxSize))));
        }

        AssertThrows<InvalidOperationException>(() => RedmineMcpOptions.FromConfiguration(Configuration(
            ("ISSUEHARBOR_REDMINE_BASE_URL", "https://redmine.example.invalid/"),
            ("ISSUEHARBOR_REDMINE_API_KEY", "bad\r\nvalue"))));
    }

    [TestMethod]
    public async Task IssueGetUsesOnlyApiKeyHeaderGetAndPreservesRedmineSubpath()
    {
        var fake = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(IssueDocument())));
        var harness = CreateHarness(fake);

        _ = await harness.ReadPath.GetIssueAsync(17, CancellationToken.None);

        var request = fake.Requests.Single();
        Assert.AreEqual("GET", request.Method);
        Assert.AreEqual("/redmine/issues/17.json", request.Uri.AbsolutePath);
        Assert.AreEqual("X-Redmine-API-Key", request.ApiKeyHeaderName);
        Assert.AreEqual(ApiKey, request.ApiKey);
        Assert.IsNull(request.Authorization);
        Assert.IsNull(request.Impersonation);
        Assert.IsFalse(request.Uri.Query.Contains(ApiKey, StringComparison.Ordinal));
        Assert.IsFalse(request.Uri.Query.Contains("key=", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void PrimaryHttpHandlerDisablesAutomaticRedirectsAndKeepsPlatformTlsValidation()
    {
        using var handler = RedmineMcpOptions.CreatePrimaryHandler();
        Assert.IsFalse(handler.AllowAutoRedirect);
        Assert.IsFalse(handler.UseCookies);
        Assert.IsNull(handler.SslOptions.RemoteCertificateValidationCallback);
    }

    [TestMethod]
    public async Task IssueListMapsSavedQueryAndSinglePagePagination()
    {
        var fake = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(IssueListDocument(38, 37, 100))));
        var harness = CreateHarness(fake);

        var result = await harness.ReadPath.ListIssuesAsync(84, null, null, 37, 100, CancellationToken.None);

        var request = fake.Requests.Single();
        var query = QueryMap(request.Uri);
        Assert.AreEqual("84", query["query_id"]);
        Assert.AreEqual("37", query["offset"]);
        Assert.AreEqual("100", query["limit"]);
        Assert.IsFalse(query.ContainsKey("status_id"));
        Assert.AreEqual(37, result.Offset);
        Assert.AreEqual(100, result.Limit);
        Assert.AreEqual(1, result.Issues.Count);
    }

    [TestMethod]
    public async Task ExplicitIssueIdsIncludeAllStatusesAndRejectMoreThanOneHundredIds()
    {
        var fake = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(IssueListDocument(1, 0, 100))));
        var harness = CreateHarness(fake);

        _ = await harness.ReadPath.ListIssuesAsync(null, [12, 19], null, 0, 100, CancellationToken.None);

        var query = QueryMap(fake.Requests.Single().Uri);
        Assert.AreEqual("12,19", query["issue_id"]);
        Assert.AreEqual("*", query["status_id"]);
        Assert.IsTrue(fake.Requests.All(request => request.Method == "GET"));

        var tooMany = Enumerable.Range(1, 101).Select(value => (long)value).ToArray();
        var failure = await AssertThrowsAsync<RedmineMcpFailure>(() =>
            harness.ReadPath.ListIssuesAsync(null, tooMany, null, 0, 100, CancellationToken.None));
        Assert.AreEqual(RedmineMcpErrorCategories.InvalidArgument, failure.Category);
        Assert.AreEqual(1, fake.Requests.Count);
    }

    [TestMethod]
    public async Task IssueListMapsAllApprovedStatusModesAndPageBoundaries()
    {
        foreach (var pair in new[] { ("open", "open"), ("closed", "closed"), ("all", "*") })
        {
            var fake = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(IssueListDocument(1, 0, 1))));
            var harness = CreateHarness(fake);
            _ = await harness.ReadPath.ListIssuesAsync(null, null, pair.Item1, 0, 1, CancellationToken.None);
            Assert.AreEqual(pair.Item2, QueryMap(fake.Requests.Single().Uri)["status_id"]);
        }

        var boundaryHandler = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(IssueListDocument(1, 0, 100))));
        var boundaryHarness = CreateHarness(boundaryHandler);
        _ = await boundaryHarness.ReadPath.ListIssuesAsync(null, null, "open", 0, 100, CancellationToken.None);
        var values = QueryMap(boundaryHandler.Requests.Single().Uri);
        Assert.AreEqual("0", values["offset"]);
        Assert.AreEqual("100", values["limit"]);

        var invalidHandler = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(IssueListDocument(1, 0, 1))));
        var invalidHarness = CreateHarness(invalidHandler);
        var invalidLimit = await AssertThrowsAsync<RedmineMcpFailure>(() =>
            invalidHarness.ReadPath.ListIssuesAsync(null, null, "open", 0, 101, CancellationToken.None));
        Assert.AreEqual(RedmineMcpErrorCategories.InvalidArgument, invalidLimit.Category);
        Assert.AreEqual(0, invalidHandler.Requests.Count);
    }

    [TestMethod]
    public async Task IssueDetailRequestsOnlyApprovedIncludeSetAndPreservesNormalizedFacts()
    {
        var fake = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(IssueDocument())));
        var harness = CreateHarness(fake);

        var issue = await harness.ReadPath.GetIssueAsync(17, CancellationToken.None);
        var query = QueryMap(fake.Requests.Single().Uri);

        Assert.AreEqual("children,attachments,relations,journals", query["include"]);
        Assert.AreEqual(17, issue.IssueId);
        Assert.AreEqual(SourceText, issue.Subject);
        Assert.AreEqual("2024-01-02T01:04:05.0000000Z", issue.CreatedOn);
        Assert.AreEqual("2024-02-29", issue.StartDate);
        Assert.IsNull(issue.DueDate);
        Assert.IsNull(issue.AssignedTo);
        Assert.IsNull(issue.ParentIssueId);
        Assert.AreEqual("alpha", issue.CustomFields[0].Values.Single().GetString());
        CollectionAssert.AreEqual(new[] { "one", "two" }, issue.CustomFields[1].Values.Select(value => value.GetString()).ToArray());
        Assert.IsTrue(issue.CustomFields[1].Multiple);
        Assert.AreEqual(0, issue.CustomFields[2].Values.Count);
        Assert.AreEqual("old status", issue.Journals[0].Details[0].OldValue!.Value.GetString());
        CollectionAssert.AreEqual(new[] { "new-a", "new-b" }, issue.Journals[0].Details[0].NewValue!.Value.EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.AreEqual(42, issue.Relations.Single().IssueToId);
        Assert.AreEqual(62, issue.Children.Single().Children.Single().IssueId);
        Assert.AreEqual(0, fake.Requests.Count(request => QueryMap(request.Uri).ContainsKey("include") &&
            QueryMap(request.Uri)["include"].Contains("changesets", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task MissingOptionalIssueCollectionsNormalizeToEmptyArraysAndOptionalFieldsToNull()
    {
        var minimal = """
            {"issue":{"id":17,"project":{"id":1,"name":"Synthetic"},"tracker":{"id":2,"name":"Bug"},"status":{"id":3,"name":"Open"},"priority":{"id":4,"name":"Normal"},"author":{"id":5,"name":"Reporter"},"subject":"Minimal","done_ratio":0,"is_private":false,"created_on":"2024-01-02T03:04:05Z","updated_on":"2024-01-02T03:04:05Z"}}
            """;
        var harness = CreateHarness(new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(minimal))));

        var issue = await harness.ReadPath.GetIssueAsync(17, CancellationToken.None);

        Assert.AreEqual(0, issue.CustomFields.Count);
        Assert.AreEqual(0, issue.Journals.Count);
        Assert.AreEqual(0, issue.Attachments.Count);
        Assert.AreEqual(0, issue.Relations.Count);
        Assert.AreEqual(0, issue.Children.Count);
        Assert.IsNull(issue.Description);
        Assert.IsNull(issue.EstimatedHours);
        Assert.IsNull(issue.FixedVersion);
        Assert.IsNull(issue.ClosedOn);
    }

    [TestMethod]
    public async Task QueryListFollowsAllVisiblePagesAndRejectsNonProgressingOrContradictoryMetadata()
    {
        var paged = new FakeHttpMessageHandler((request, _) =>
        {
            var offset = int.Parse(QueryMap(request.Uri)["offset"], System.Globalization.CultureInfo.InvariantCulture);
            var body = offset == 0
                ? """{"total_count":2,"offset":0,"limit":100,"queries":[{"id":1,"name":"Synthetic A","is_public":true,"project_id":null}]}"""
                : """{"total_count":2,"offset":1,"limit":100,"queries":[{"id":2,"name":"Synthetic B","is_public":false,"project_id":8}]}""";
            return Task.FromResult(JsonResponse(body));
        });

        var queries = await CreateHarness(paged).ReadPath.ListQueriesAsync(CancellationToken.None);
        Assert.AreEqual(2, paged.Requests.Count);
        Assert.AreEqual("0", QueryMap(paged.Requests[0].Uri)["offset"]);
        Assert.AreEqual("1", QueryMap(paged.Requests[1].Uri)["offset"]);
        Assert.AreEqual(100, int.Parse(QueryMap(paged.Requests[1].Uri)["limit"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.IsNull(queries.Queries[0].ProjectId);
        Assert.AreEqual("Synthetic B", queries.Queries[1].Name);

        foreach (var invalidPage in new[]
                 {
                     """{"total_count":2,"offset":0,"limit":100,"queries":[]}""",
                     """{"total_count":2,"offset":1,"limit":100,"queries":[{"id":1,"name":"x"}]}""",
                     """{"total_count":"two","offset":0,"limit":100,"queries":[]}""",
                 })
        {
            var fake = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(invalidPage)));
            var failure = await AssertThrowsAsync<RedmineMcpFailure>(() =>
                CreateHarness(fake).ReadPath.ListQueriesAsync(CancellationToken.None));
            Assert.AreEqual(RedmineMcpErrorCategories.InvalidUpstreamResponse, failure.Category);
            Assert.AreEqual(1, fake.Requests.Count);
        }
    }

    [TestMethod]
    [DataRow(401, "authentication_required")]
    [DataRow(403, "forbidden")]
    [DataRow(404, "not_found")]
    [DataRow(408, "upstream_unavailable")]
    [DataRow(429, "upstream_unavailable")]
    [DataRow(500, "upstream_unavailable")]
    [DataRow(302, "invalid_upstream_response")]
    [DataRow(400, "invalid_upstream_response")]
    public async Task HttpStatusCodesMapToFrozenErrorCategories(int statusCode, string expectedCategory)
    {
        var fake = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)statusCode)
        {
            Content = new StringContent("SYNTHETIC-RAW-UPSTREAM-BODY"),
        }));
        var harness = CreateHarness(fake);

        var result = await new RedmineMcpToolHandlers(harness.ReadPath).IssueGet(17, CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual(expectedCategory, result.Content.OfType<TextContentBlock>().Single().Text);
        Assert.AreEqual(1, fake.Requests.Count);
        Assert.IsFalse(result.Content.OfType<TextContentBlock>().Any(block => block.Text.Contains("SYNTHETIC-RAW-UPSTREAM-BODY", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task TransportFailureAndInternalTimeoutAreNormalizedButCallerCancellationPropagates()
    {
        var transport = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("synthetic private transport detail"));
        var transportFailure = await AssertThrowsAsync<RedmineMcpFailure>(() =>
            CreateHarness(transport).ReadPath.GetIssueAsync(17, CancellationToken.None));
        Assert.AreEqual(RedmineMcpErrorCategories.UpstreamUnavailable, transportFailure.Category);
        Assert.AreEqual(RedmineMcpErrorCategories.UpstreamUnavailable, transportFailure.Message);

        var slow = new FakeHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(IssueDocument());
        });
        var timeout = await AssertThrowsAsync<RedmineMcpFailure>(() =>
            CreateHarness(slow, timeout: TimeSpan.FromMilliseconds(40)).ReadPath.GetIssueAsync(17, CancellationToken.None));
        Assert.AreEqual(RedmineMcpErrorCategories.UpstreamUnavailable, timeout.Category);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await AssertThrowsAsync<OperationCanceledException>(() =>
            CreateHarness(new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(IssueDocument()))))
                .ReadPath.GetIssueAsync(17, cancelled.Token));
    }

    [TestMethod]
    public async Task MalformedJsonAndRequiredShapeFailuresMapToInvalidUpstreamResponse()
    {
        foreach (var body in new[] { "{not-json", "{\"issue\":{\"id\":17}}" })
        {
            var harness = CreateHarness(new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(body))));
            var failure = await AssertThrowsAsync<RedmineMcpFailure>(() =>
                harness.ReadPath.GetIssueAsync(17, CancellationToken.None));
            Assert.AreEqual(RedmineMcpErrorCategories.InvalidUpstreamResponse, failure.Category);
        }
    }

    [TestMethod]
    public async Task AttachmentReturnsMetadataAndExactMcpBlobWithoutExposingContentUrlOrPath()
    {
        var bytes = Encoding.UTF8.GetBytes("synthetic attachment bytes");
        var contentUri = "https://redmine.example.invalid/redmine/attachments/download/71/synthetic.txt";
        var fake = new FakeHttpMessageHandler((request, _) => Task.FromResult(
            request.Uri.AbsolutePath.EndsWith("/attachments/71.json", StringComparison.Ordinal)
                ? JsonResponse(AttachmentDocument(71, bytes.Length, contentUri))
                : BinaryResponse(bytes)));
        var harness = CreateHarness(fake);
        var handlers = new RedmineMcpToolHandlers(harness.ReadPath);

        var result = await handlers.AttachmentGet(71, CancellationToken.None);

        Assert.IsFalse(result.IsError, string.Join("; ", result.Content.OfType<TextContentBlock>().Select(block => block.Text)));
        Assert.AreEqual(2, fake.Requests.Count);
        Assert.IsTrue(fake.Requests.All(request => request.Method == "GET"));
        Assert.AreEqual("X-Redmine-API-Key", fake.Requests[1].ApiKeyHeaderName);
        Assert.AreEqual(ApiKey, fake.Requests[1].ApiKey);
        var structured = result.StructuredContent!.Value;
        var serialized = structured.GetRawText();
        Assert.IsFalse(serialized.Contains("content_url", StringComparison.Ordinal));
        Assert.IsFalse(serialized.Contains("redmine.example.invalid", StringComparison.Ordinal));
        Assert.IsFalse(serialized.Contains("temp", StringComparison.OrdinalIgnoreCase));
        var blob = result.Content.OfType<EmbeddedResourceBlock>().Single().Resource as BlobResourceContents;
        Assert.IsNotNull(blob);
        CollectionAssert.AreEqual(bytes, blob!.DecodedData.ToArray());
        Assert.AreEqual("synthetic.txt", structured.GetProperty("attachment").GetProperty("filename").GetString());
    }

    [TestMethod]
    public async Task CrossOriginAttachmentUrlFailsBeforeSecondCredentialedRequest()
    {
        var fake = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(
            AttachmentDocument(72, 3, "https://other.example.invalid/redmine/attachments/download/72/file.bin"))));

        var failure = await AssertThrowsAsync<RedmineMcpFailure>(() =>
            CreateHarness(fake).ReadPath.GetAttachmentAsync(72, CancellationToken.None));

        Assert.AreEqual(RedmineMcpErrorCategories.InvalidUpstreamResponse, failure.Category);
        Assert.AreEqual(1, fake.Requests.Count);

        var emptyUserInfo = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(
            AttachmentDocument(79, 3, "https://@redmine.example.invalid/redmine/file/79"))));
        var unsafeUriFailure = await AssertThrowsAsync<RedmineMcpFailure>(() =>
            CreateHarness(emptyUserInfo).ReadPath.GetAttachmentAsync(79, CancellationToken.None));
        Assert.AreEqual(RedmineMcpErrorCategories.InvalidUpstreamResponse, unsafeUriFailure.Category);
        Assert.AreEqual(1, emptyUserInfo.Requests.Count);
    }

    [TestMethod]
    public async Task AttachmentMetadataSizeIsCheckedAndBinaryReadIsIndependentlyBounded()
    {
        var oversizeMetadata = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(
            AttachmentDocument(73, 6, "https://redmine.example.invalid/redmine/file/73"))));
        var oversize = await AssertThrowsAsync<RedmineMcpFailure>(() =>
            CreateHarness(oversizeMetadata, maxAttachmentBytes: 5).ReadPath.GetAttachmentAsync(73, CancellationToken.None));
        Assert.AreEqual(RedmineMcpErrorCategories.InvalidUpstreamResponse, oversize.Category);
        Assert.AreEqual(1, oversizeMetadata.Requests.Count);

        var unrepresentableMetadata = new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(
            AttachmentDocument(78, long.MaxValue, "https://redmine.example.invalid/redmine/file/78"))));
        var unrepresentable = await AssertThrowsAsync<RedmineMcpFailure>(() =>
            CreateHarness(unrepresentableMetadata, maxAttachmentBytes: long.MaxValue).ReadPath.GetAttachmentAsync(78, CancellationToken.None));
        Assert.AreEqual(RedmineMcpErrorCategories.InvalidUpstreamResponse, unrepresentable.Category);
        Assert.AreEqual(1, unrepresentableMetadata.Requests.Count);

        var stream = new NonSeekableCountingStream(Enumerable.Range(0, 150).Select(value => (byte)value).ToArray());
        var bounded = new FakeHttpMessageHandler((request, _) => Task.FromResult(
            request.Uri.AbsolutePath.EndsWith("/attachments/74.json", StringComparison.Ordinal)
                ? JsonResponse(AttachmentDocument(74, 100, "https://redmine.example.invalid/redmine/file/74"))
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }));
        var actualCeiling = await AssertThrowsAsync<RedmineMcpFailure>(() =>
            CreateHarness(bounded, maxAttachmentBytes: 100).ReadPath.GetAttachmentAsync(74, CancellationToken.None));
        Assert.AreEqual(RedmineMcpErrorCategories.InvalidUpstreamResponse, actualCeiling.Category);
        Assert.IsTrue(stream.BytesRead <= 101, $"Read {stream.BytesRead} bytes despite a 100-byte configured ceiling.");
    }

    [TestMethod]
    public async Task AttachmentByteCountMustMatchMetadataAndUnexpectedRedirectIsNotFollowed()
    {
        var mismatch = new FakeHttpMessageHandler((request, _) => Task.FromResult(
            request.Uri.AbsolutePath.EndsWith("/attachments/75.json", StringComparison.Ordinal)
                ? JsonResponse(AttachmentDocument(75, 5, "https://redmine.example.invalid/redmine/file/75"))
                : BinaryResponse(Encoding.UTF8.GetBytes("abc"))));
        var mismatchFailure = await AssertThrowsAsync<RedmineMcpFailure>(() =>
            CreateHarness(mismatch).ReadPath.GetAttachmentAsync(75, CancellationToken.None));
        Assert.AreEqual(RedmineMcpErrorCategories.InvalidUpstreamResponse, mismatchFailure.Category);

        var redirect = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("https://other.example.invalid/redirect-target") },
        }));
        var redirectFailure = await AssertThrowsAsync<RedmineMcpFailure>(() =>
            CreateHarness(redirect).ReadPath.GetAttachmentAsync(76, CancellationToken.None));
        Assert.AreEqual(RedmineMcpErrorCategories.InvalidUpstreamResponse, redirectFailure.Category);
        Assert.AreEqual(1, redirect.Requests.Count);
    }

    [TestMethod]
    public async Task ApiKeyPrivateSourceTextAndAttachmentBytesNeverEnterErrorOutputOrLogs()
    {
        var writer = new StringWriter();
        var unauthorized = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent($"{ApiKey} {SourceText}"),
        }));
        var harness = CreateHarness(unauthorized, diagnostics: new StderrRedmineMcpDiagnostics(writer));
        var issueError = await new RedmineMcpToolHandlers(harness.ReadPath).IssueGet(17, CancellationToken.None);

        Assert.IsTrue(issueError.IsError);
        Assert.AreEqual(RedmineMcpErrorCategories.AuthenticationRequired, issueError.Content.OfType<TextContentBlock>().Single().Text);
        Assert.IsFalse(issueError.Content.OfType<TextContentBlock>().Single().Text.Contains(ApiKey, StringComparison.Ordinal));
        Assert.IsFalse(writer.ToString().Contains(ApiKey, StringComparison.Ordinal));
        Assert.IsFalse(writer.ToString().Contains(SourceText, StringComparison.Ordinal));

        var bytes = Encoding.UTF8.GetBytes("SYNTHETIC-ATTACHMENT-BYTES");
        var logWriter = new StringWriter();
        var attachmentHandler = new FakeHttpMessageHandler((request, _) => Task.FromResult(
            request.Uri.AbsolutePath.EndsWith("/attachments/77.json", StringComparison.Ordinal)
                ? JsonResponse(AttachmentDocument(77, 3, "https://other.example.invalid/file"))
                : BinaryResponse(bytes)));
        var attachFailure = await new RedmineMcpToolHandlers(CreateHarness(attachmentHandler, diagnostics: new StderrRedmineMcpDiagnostics(logWriter)).ReadPath)
            .AttachmentGet(77, CancellationToken.None);
        Assert.AreEqual(RedmineMcpErrorCategories.InvalidUpstreamResponse, attachFailure.Content.OfType<TextContentBlock>().Single().Text);
        Assert.IsFalse(logWriter.ToString().Contains(Encoding.UTF8.GetString(bytes), StringComparison.Ordinal));
        Assert.IsFalse(logWriter.ToString().Contains(ApiKey, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ToolHandlersDoNotWriteDiagnosticsToStdout()
    {
        var previous = Console.Out;
        using var stdout = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            var result = await new RedmineMcpToolHandlers(
                    CreateHarness(new FakeHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(IssueDocument())))).ReadPath)
                .IssueGet(17, CancellationToken.None);
            Assert.IsFalse(result.IsError, string.Join("; ", result.Content.OfType<TextContentBlock>().Select(block => block.Text)));
        }
        finally
        {
            Console.SetOut(previous);
        }

        Assert.AreEqual(string.Empty, stdout.ToString());
    }

    private static string IssueListDocument(int count, int offset, int limit) => $$"""
        {"total_count":{{count}},"offset":{{offset}},"limit":{{limit}},"issues":[{"id":17,"project":{"id":1,"name":"Synthetic Project"},"tracker":{"id":2,"name":"Bug"},"status":{"id":3,"name":"Open"},"priority":{"id":4,"name":"Normal"},"subject":"{{SourceText}}","author":{"id":5,"name":"Synthetic Reporter"},"assigned_to":null,"created_on":"2024-01-02T03:04:05Z","updated_on":"2024-01-02T03:04:05Z","unknown_upstream_field":"ignored"}]}
        """;

    private static string IssueDocument() => """
        {"issue":{"id":17,"project":{"id":1,"name":"Synthetic Project"},"tracker":{"id":2,"name":"Bug"},"status":{"id":3,"name":"Open"},"priority":{"id":4,"name":"Normal"},"author":{"id":5,"name":"Synthetic Reporter"},"assigned_to":null,"category":null,"fixed_version":null,"parent":null,"subject":"Synthetic public-safe issue","description":"line one\nline two","start_date":"2024-02-29","due_date":null,"done_ratio":25,"is_private":false,"estimated_hours":2.5,"created_on":"2024-01-02T03:04:05+02:00","updated_on":"2024-01-03T03:04:05Z","closed_on":null,"custom_fields":[{"id":1,"name":"Single field","value":"alpha"},{"id":2,"name":"Multiple field","value":["one","two"]},{"id":3,"name":"Empty field","value":null}],"journals":[{"id":31,"user":{"id":5,"name":"Synthetic Reporter"},"notes":"Synthetic journal text","created_on":"2024-01-02T04:05:06Z","updated_on":null,"updated_by":null,"private_notes":false,"details":[{"property":"attr","name":"status_id","old_value":"old status","new_value":["new-a","new-b"]}]}],"attachments":[{"id":41,"filename":"synthetic.txt","filesize":4,"content_type":"text/plain","description":null,"author":null,"created_on":"2024-01-02T05:06:07Z","content_url":"https://redmine.example.invalid/redmine/download/41"}],"relations":[{"id":51,"relation_type":"relates","issue_id":17,"issue_to_id":42}],"children":[{"id":61,"tracker":{"id":2,"name":"Bug"},"subject":"Child one","children":[{"id":62,"tracker":{"id":2,"name":"Bug"},"subject":"Child two"}]}],"unknown_upstream_field":"ignored"}}
        """;

    private static string AttachmentDocument(long id, long fileSize, string contentUrl) => JsonSerializer.Serialize(new
    {
        attachment = new
        {
            id,
            filename = "synthetic.txt",
            filesize = fileSize,
            content_type = "text/plain",
            description = (string?)null,
            author = new { id = 5, name = "Synthetic Reporter" },
            created_on = "2024-01-02T03:04:05Z",
            content_url = contentUrl,
        },
    });

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value)).Build();

    private static Harness CreateHarness(
        FakeHttpMessageHandler handler,
        long maxAttachmentBytes = 20 * 1024 * 1024,
        TimeSpan? timeout = null,
        IRedmineMcpDiagnostics? diagnostics = null)
    {
        var options = new RedmineMcpOptions(
            new Uri("https://redmine.example.invalid/redmine/"),
            ApiKey,
            timeout ?? TimeSpan.FromSeconds(30),
            maxAttachmentBytes);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = options.BaseUri,
            Timeout = options.Timeout,
        };
        var log = diagnostics ?? new StderrRedmineMcpDiagnostics(new StringWriter());
        return new Harness(new RedmineMcpReadPath(options, httpClient, log), log);
    }

    private static Dictionary<string, string> QueryMap(Uri uri) => uri.Query.TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(pair => pair.Split('=', 2))
        .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : string.Empty, StringComparer.Ordinal);

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage BinaryResponse(byte[] bytes) => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(bytes),
    };

    private static TException AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new AssertFailedException($"Expected {typeof(TException).Name}.");
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new AssertFailedException($"Expected {typeof(TException).Name}.");
    }

    private sealed record Harness(RedmineMcpReadPath ReadPath, IRedmineMcpDiagnostics Diagnostics);

    private sealed record RequestSnapshot(
        string Method,
        Uri Uri,
        string? ApiKeyHeaderName,
        string? ApiKey,
        string? Authorization,
        string? Impersonation);

    private sealed class FakeHttpMessageHandler(
        Func<RequestSnapshot, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        private readonly ConcurrentQueue<RequestSnapshot> _requests = new();

        public IReadOnlyList<RequestSnapshot> Requests => _requests.ToArray();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var apiKey = request.Headers.TryGetValues("X-Redmine-API-Key", out var keys) ? keys.SingleOrDefault() : null;
            var impersonation = request.Headers.TryGetValues("X-Redmine-Switch-User", out var switchUsers) ? switchUsers.SingleOrDefault() : null;
            var snapshot = new RequestSnapshot(
                request.Method.Method,
                request.RequestUri!,
                apiKey is null ? null : "X-Redmine-API-Key",
                apiKey,
                request.Headers.Authorization?.ToString(),
                impersonation);
            _requests.Enqueue(snapshot);
            return responseFactory(snapshot, cancellationToken);
        }
    }

    private sealed class NonSeekableCountingStream(byte[] bytes) : Stream
    {
        private int _position;
        public int BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var length = Math.Min(Math.Min(count, 13), bytes.Length - _position);
            Array.Copy(bytes, _position, buffer, offset, length);
            _position += length;
            BytesRead += length;
            return length;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = Math.Min(Math.Min(buffer.Length, 13), bytes.Length - _position);
            bytes.AsMemory(_position, length).CopyTo(buffer);
            _position += length;
            BytesRead += length;
            return ValueTask.FromResult(length);
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
