#:project ../src/Microsandbox/Microsandbox.csproj

using Microsandbox;

const string markerPath = "/tmp/snapshot-marker.txt";
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
var cancellationToken = timeout.Token;
var client = LoadClient();
var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
var baseName = $"dotnet-snapshot-base-{suffix}";
var forkName = $"dotnet-snapshot-fork-{suffix}";
var snapshotName = $"dotnet-snapshot-{suffix}";
Sandbox? source = null;
Sandbox? fork = null;

try
{
    source = await client.CreateAsync(
        baseName,
        new SandboxOptions { Image = "alpine:3.20" },
        cancellationToken
    );
    var payload = $"created by {baseName}\n";
    await source.Filesystem.WriteStringAsync(markerPath, payload, cancellationToken);
    await source.StopAsync(cancellationToken: cancellationToken);
    await source.DisposeAsync();
    source = null;

    var artifact = await client.Snapshots.CreateAsync(
        new SnapshotCreateOptions
        {
            Name = snapshotName,
            SourceSandbox = baseName,
            RecordIntegrity = true,
        },
        cancellationToken
    );
    Console.WriteLine($"  snapshot: digest={artifact.Digest} size={artifact.SizeBytes} bytes");

    var report = await client.Snapshots.VerifyAsync(snapshotName, cancellationToken);
    Require(report.Upper.Kind == "verified", $"snapshot integrity status was {report.Upper.Kind}");
    Console.WriteLine($"  verification: upper={report.Upper.Kind}");

    var indexed = await client.Snapshots.GetAsync(snapshotName, cancellationToken);
    Require(indexed.Digest == artifact.Digest, "snapshot index digest did not match artifact");
    Console.WriteLine($"  index: name={indexed.Name ?? "(unnamed)"} digest={indexed.Digest}");

    fork = await client.CreateAsync(
        forkName,
        new SandboxOptions { Snapshot = snapshotName },
        cancellationToken
    );
    var restored = await fork.Filesystem.ReadStringAsync(markerPath, cancellationToken);
    Require(restored == payload, "fork did not preserve the snapshot marker");
    Console.WriteLine($"  fork preserved: {restored.Trim()}");
    Console.WriteLine("Snapshot fork example passed.");
}
finally
{
    await CleanupSandbox(fork, forkName, client);
    await CleanupSandbox(source, baseName, client);
    await BestEffort(() => client.Snapshots.RemoveAsync(snapshotName, force: true));
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

static async Task CleanupSandbox(Sandbox? sandbox, string name, MicrosandboxClient client)
{
    if (sandbox is not null)
    {
        await BestEffort(() => sandbox.StopAsync(cancellationToken: CancellationToken.None));
        await BestEffort(async () => await sandbox.DisposeAsync());
    }

    await BestEffort(() => client.RemoveAsync(name));
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
