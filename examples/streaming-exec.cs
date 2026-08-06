#:project ../src/Microsandbox/Microsandbox.csproj

using System.Text;
using Microsandbox;

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
var cancellationToken = timeout.Token;
var client = LoadClient();
var name = $"dotnet-stream-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
Sandbox? sandbox = null;

try
{
    sandbox = await client.CreateAsync(
        name,
        new SandboxOptions { Image = "alpine:3.20" },
        cancellationToken
    );

    await using (
        var command = await sandbox.ShellStreamingAsync(
            "echo out-line; echo err-line >&2; exit 3",
            cancellationToken: cancellationToken
        )
    )
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var exitCode = -1;

        while (await command.ReceiveAsync(cancellationToken) is { } message)
        {
            switch (message)
            {
                case ExecStartedEvent started:
                    Console.WriteLine($"  started pid={started.ProcessId}");
                    break;
                case ExecStandardOutputEvent output:
                    stdout.Append(Encoding.UTF8.GetString(output.Data));
                    break;
                case ExecStandardErrorEvent error:
                    stderr.Append(Encoding.UTF8.GetString(error.Data));
                    break;
                case ExecExitedEvent exited:
                    exitCode = exited.ExitCode;
                    break;
                case ExecDoneEvent:
                    goto Complete;
            }
        }

        Complete:
        Require(exitCode == 3, $"expected exit 3, got {exitCode}");
        Require(stdout.ToString().Contains("out-line"), "stdout event was not received");
        Require(stderr.ToString().Contains("err-line"), "stderr event was not received");
        Console.WriteLine($"  stdout: {stdout.ToString().Trim()}");
        Console.WriteLine($"  stderr: {stderr.ToString().Trim()}");
    }

    await using (
        var sleeper = await sandbox.ShellStreamingAsync(
            "sleep 60",
            cancellationToken: cancellationToken
        )
    )
    {
        while (await sleeper.ReceiveAsync(cancellationToken) is not ExecStartedEvent) { }

        Console.WriteLine("  sending SIGTERM to long-running command");
        await sleeper.SignalAsync(15, cancellationToken);
        var exitCode = await sleeper.WaitAsync(cancellationToken);
        Console.WriteLine($"  command exited with {exitCode}");
    }

    Console.WriteLine("Streaming exec example passed.");
}
finally
{
    if (sandbox is not null)
    {
        await BestEffort(() => sandbox.StopAsync(cancellationToken: CancellationToken.None));
        await BestEffort(async () => await sandbox.DisposeAsync());
    }

    await BestEffort(() => client.RemoveAsync(name));
}

static MicrosandboxClient LoadClient()
{
    var client = MicrosandboxClient.Load();
    var msbPath = Environment.GetEnvironmentVariable("MICROSANDBOX_MSB_PATH");
    if (!string.IsNullOrWhiteSpace(msbPath))
    {
        client.SetMsbPath(msbPath);
    }
    return client;
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static async Task BestEffort(Func<Task> action)
{
    try
    {
        await action();
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"cleanup: {exception.Message}");
    }
}
