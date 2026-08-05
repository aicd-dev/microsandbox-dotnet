#:project ../src/Microsandbox/Microsandbox.csproj

using Microsandbox;

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
var cancellationToken = timeout.Token;
var client = LoadClient();
var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
var names = new[] { $"dotnet-metrics-a-{suffix}", $"dotnet-metrics-b-{suffix}" };
var sandboxes = new List<Sandbox>();

try
{
    foreach (var name in names)
    {
        sandboxes.Add(
            await client.CreateAsync(
                name,
                new SandboxOptions { Image = "alpine:3.20", MemoryMiB = 256 },
                cancellationToken
            )
        );
    }

    foreach (var sandbox in sandboxes)
    {
        await sandbox.ShellAsync("for i in 1 2 3; do sleep 0.1; echo ok; done", cancellationToken);
    }

    var point = await sandboxes[0].MetricsAsync(cancellationToken);
    Console.WriteLine(
        $"{sandboxes[0].Name}: cpu={point.CpuPercent:F1}% memory={point.MemoryBytes >> 10} KiB uptime={point.Uptime}"
    );

    await using (
        var stream = await sandboxes[0]
            .MetricsStreamAsync(TimeSpan.FromMilliseconds(250), cancellationToken)
    )
    {
        for (var index = 1; index <= 3; index++)
        {
            using var receiveTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            receiveTimeout.CancelAfter(TimeSpan.FromSeconds(5));
            var sample =
                await stream.ReceiveAsync(receiveTimeout.Token)
                ?? throw new InvalidOperationException("metrics stream ended early");
            Console.WriteLine(
                $"  #{index}: cpu={sample.CpuPercent:F1}% rx={sample.NetworkReceiveBytes} tx={sample.NetworkTransmitBytes}"
            );
        }
    }

    var all = await client.AllMetricsAsync(cancellationToken);
    Console.WriteLine($"AllMetricsAsync returned {all.Count} running sandboxes:");
    foreach (var (name, metrics) in all)
    {
        Console.WriteLine(
            $"  {name}: memory={metrics.MemoryBytes >> 10} KiB uptime={metrics.Uptime}"
        );
    }

    Console.WriteLine("Metrics example passed.");
}
finally
{
    foreach (var sandbox in sandboxes)
    {
        await BestEffort(() => sandbox.StopAsync(cancellationToken: CancellationToken.None));
        await BestEffort(async () => await sandbox.DisposeAsync());
    }

    foreach (var name in names)
    {
        await BestEffort(() => client.RemoveAsync(name));
    }
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
