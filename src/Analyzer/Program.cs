using IssueHarbor.Analyzer.Storage;

var builder = WebApplication.CreateBuilder(args);

var durableDataRoot = builder.Configuration["IssueHarbor:DataRoot"];
if (!string.IsNullOrWhiteSpace(durableDataRoot))
{
    var connections = new SqliteConnectionFactory(durableDataRoot);
    var migrationsDirectory = Path.Combine(AppContext.BaseDirectory, "Storage", "Migrations");
    new StorageInitializer(connections, migrationsDirectory).InitializeOrVerify();
}

builder.Services.AddRazorPages();

var app = builder.Build();

app.MapRazorPages();

app.Run();

public partial class Program { }
