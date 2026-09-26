using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using IssueHarbor.RedmineMcp;

var builder = Host.CreateApplicationBuilder(args);

var redmineOptions = RedmineMcpOptions.FromConfiguration(builder.Configuration);
var redmineHttpClient = RedmineMcpOptions.CreateHttpClient(redmineOptions);
var readPath = new RedmineMcpReadPath(redmineOptions, redmineHttpClient, StderrRedmineMcpDiagnostics.Instance);
var toolHandlers = new RedmineMcpToolHandlers(readPath);

builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddConsole(options =>
    options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithTools(RedmineMcpManifest.CreateTools(toolHandlers))
    .WithStdioServerTransport();

builder.Services.AddSingleton(redmineOptions);
builder.Services.AddSingleton(redmineHttpClient);
builder.Services.AddSingleton(readPath);
builder.Services.AddSingleton(toolHandlers);

using var host = builder.Build();
await host.RunAsync();
