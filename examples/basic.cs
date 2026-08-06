#:project ../src/Microsandbox/Microsandbox.csproj

using Microsandbox;

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
var cancellationToken = timeout.Token;
var client = LoadClient();
var name = $"dotnet-basic-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
Sandbox? sandbox = null;

try
{
    Console.WriteLine($"Creating sandbox {name}...");
    sandbox = await client.CreateAsync(
        name,
        new SandboxOptions
        {
            Image = "alpine:3.20",
            MemoryMiB = 256,
            CPUs = 1,
            Environment = new Dictionary<string, string> { ["GREETING"] = "hello-from-dotnet" },
        },
        cancellationToken
    );

    var echo = await sandbox.ExecuteAsync(
        "echo",
        new ExecOptions { Arguments = ["hello", "world"] },
        cancellationToken
    );
    Require(
        echo.IsSuccess && echo.StandardOutput.Contains("hello world"),
        "echo output was unexpected"
    );
    Console.WriteLine($"  echo: {echo.StandardOutput.Trim()}");

    var environment = await sandbox.ShellAsync("echo $GREETING", cancellationToken);
    Require(
        environment.StandardOutput.Contains("hello-from-dotnet"),
        "guest environment was not applied"
    );
    Console.WriteLine($"  environment: {environment.StandardOutput.Trim()}");

    var nonZero = await sandbox.ShellAsync("exit 42", cancellationToken);
    Require(nonZero.ExitCode == 42 && !nonZero.IsSuccess, "non-zero exit code was not preserved");
    Console.WriteLine($"  non-zero exit: {nonZero.ExitCode}");

    const string payload = "microsandbox .NET filesystem works\n";
    await sandbox.Filesystem.WriteStringAsync("/tmp/dotnet-sdk.txt", payload, cancellationToken);
    var roundTrip = await sandbox.Filesystem.ReadStringAsync(
        "/tmp/dotnet-sdk.txt",
        cancellationToken
    );
    Require(roundTrip == payload, "filesystem round trip failed");

    var metrics = await sandbox.MetricsAsync(cancellationToken);
    Console.WriteLine(
        $"  metrics: uptime={metrics.Uptime} memory={metrics.MemoryBytes} cpu={metrics.CpuPercent:F1}%"
    );
    Console.WriteLine("Basic example passed.");
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
