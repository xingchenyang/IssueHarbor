using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IssueHarbor.Analyzer.Tests;

[TestClass]
public sealed class BootstrapSmokeTests
{
    [TestMethod]
    public async Task Root_page_returns_successful_response()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(content.Contains("IssueHarbor", StringComparison.Ordinal));
    }
}
