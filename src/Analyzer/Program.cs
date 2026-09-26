using IssueHarbor.Analyzer.Storage;
using IssueHarbor.Analyzer.SourceContext;

var builder = WebApplication.CreateBuilder(args);

var durableDataRoot = builder.Configuration["IssueHarbor:DataRoot"];
if (!string.IsNullOrWhiteSpace(durableDataRoot))
{
    var connections = new SqliteConnectionFactory(durableDataRoot);
    var migrationsDirectory = Path.Combine(AppContext.BaseDirectory, "Storage", "Migrations");
    new StorageInitializer(connections, migrationsDirectory).InitializeOrVerify();
}

var sourceRepositories = builder.Configuration
    .GetSection("IssueHarbor:SourceRepositories")
    .Get<List<GitRepositoryMappingOptions>>() ?? [];
var sourceRepositoryCatalog = new GitRepositoryCatalog(sourceRepositories);
builder.Services.AddSingleton(sourceRepositoryCatalog);
builder.Services.AddSingleton<GitProcessRunner>();
builder.Services.AddSingleton<GitRepositoryReader>();

builder.Services.AddRazorPages();

var app = builder.Build();

app.MapRazorPages();

app.Run();

public partial class Program { }
