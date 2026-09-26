using System.Diagnostics;

namespace IssueHarbor.Analyzer.Tests.SourceContext.GitProcessStub;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "hold")
        {
            var temporaryPath = $"{args[1]}.tmp";
            await File.WriteAllTextAsync(temporaryPath, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            File.Move(temporaryPath, args[1]);
            Console.WriteLine("ready");
            await Task.Delay(Timeout.InfiniteTimeSpan);
            return 0;
        }

        if (args.Length == 1 && args[0] == "environment")
        {
            Console.WriteLine(string.Join('|',
                Environment.GetEnvironmentVariable("GIT_NO_LAZY_FETCH"),
                Environment.GetEnvironmentVariable("GIT_NO_REPLACE_OBJECTS"),
                Environment.GetEnvironmentVariable("GIT_OPTIONAL_LOCKS")));
            return 0;
        }

        if (args.Length == 2 && args[0] == "overflow")
        {
            var temporaryPath = $"{args[1]}.tmp";
            await File.WriteAllTextAsync(temporaryPath, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            File.Move(temporaryPath, args[1]);
            Console.WriteLine(new string('x', 1024 * 1024));
            return 0;
        }

        return 2;
    }
}

public sealed class GitProcessStubMarker { }
