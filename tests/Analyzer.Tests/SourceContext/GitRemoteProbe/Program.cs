using System.Text;

namespace IssueHarbor.TestGitRemoteProbe;

internal static class Program
{
    private static int Main(string[] args)
    {
        var logPath = Environment.GetEnvironmentVariable("ISSUEHARBOR_REMOTE_HELPER_LOG");
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return 2;
        }

        AppendLog($"start {string.Join(' ', args)}");
        string? command;
        while ((command = Console.ReadLine()) is not null)
        {
            AppendLog($"command {command}");
            if (command == "capabilities")
            {
                Console.WriteLine("fetch");
                Console.WriteLine();
                Console.Out.Flush();
            }
            else if (command.StartsWith("fetch ", StringComparison.Ordinal))
            {
                // Report an empty successful response. This fake transport never connects anywhere
                // and intentionally cannot supply the requested object.
                Console.WriteLine();
                Console.Out.Flush();
            }
            else if (command.StartsWith("option ", StringComparison.Ordinal))
            {
                Console.WriteLine("unsupported");
                Console.Out.Flush();
            }
            else if (command == "list")
            {
                Console.WriteLine();
                Console.Out.Flush();
            }
        }

        return 0;

        void AppendLog(string line)
        {
            File.AppendAllText(logPath, line + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }
}
