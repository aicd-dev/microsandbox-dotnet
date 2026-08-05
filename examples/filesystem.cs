#:project ../src/Microsandbox/Microsandbox.csproj

using System.Text;
using Microsandbox;

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
var cancellationToken = timeout.Token;
var client = LoadClient();
var name = $"dotnet-fs-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
Sandbox? sandbox = null;

try
{
    sandbox = await client.CreateAsync(
        name,
        new SandboxOptions { Image = "alpine:3.20" },
        cancellationToken
    );
    var filesystem = sandbox.Filesystem;

    await filesystem.WriteStringAsync("/tmp/note.txt", "hello\n", cancellationToken);
    Console.WriteLine(
        $"  note: {(await filesystem.ReadStringAsync("/tmp/note.txt", cancellationToken)).Trim()}"
    );

    await filesystem.MkdirAsync("/tmp/work/data", cancellationToken);
    await filesystem.WriteStringAsync("/tmp/work/data/a.txt", "alpha", cancellationToken);
    await filesystem.WriteStringAsync("/tmp/work/data/b.txt", "beta", cancellationToken);
    foreach (var entry in await filesystem.ListAsync("/tmp/work/data", cancellationToken))
    {
        var stat = await filesystem.StatAsync(entry.Path, cancellationToken);
        Console.WriteLine($"  {entry.Path}: {stat.Kind}, {stat.Size} bytes");
    }

    await filesystem.CopyAsync("/tmp/work/data/a.txt", "/tmp/work/data/a.copy", cancellationToken);
    await filesystem.RenameAsync(
        "/tmp/work/data/b.txt",
        "/tmp/work/data/b.renamed",
        cancellationToken
    );
    Console.WriteLine(
        $"  copy exists: {await filesystem.ExistsAsync("/tmp/work/data/a.copy", cancellationToken)}"
    );

    var hostDirectory = Directory.CreateTempSubdirectory("dotnet-sdk-fs-");
    try
    {
        var source = Path.Combine(hostDirectory.FullName, "from-host.txt");
        var destination = Path.Combine(hostDirectory.FullName, "back-to-host.txt");
        await File.WriteAllTextAsync(source, "round-tripped through a microVM", cancellationToken);
        await filesystem.CopyFromHostAsync(source, "/tmp/from-host.txt", cancellationToken);
        await filesystem.CopyToHostAsync("/tmp/from-host.txt", destination, cancellationToken);
        Console.WriteLine(
            $"  host round trip: {await File.ReadAllTextAsync(destination, cancellationToken)}"
        );
    }
    finally
    {
        hostDirectory.Delete(recursive: true);
    }

    await sandbox.ShellAsync(
        "dd if=/dev/zero of=/tmp/big.bin bs=1M count=2 status=none",
        cancellationToken
    );
    await using (var input = await filesystem.ReadStreamAsync("/tmp/big.bin", cancellationToken))
    {
        var sink = new MemoryStream();
        await input.CopyToAsync(sink, cancellationToken);
        Console.WriteLine($"  streamed read: {sink.Length} bytes");
    }

    await using (
        var output = await filesystem.WriteStreamAsync("/tmp/composed.txt", cancellationToken)
    )
    {
        foreach (var chunk in new[] { "alpha;", "beta;", "gamma;" })
        {
            await output.WriteAsync(Encoding.UTF8.GetBytes(chunk), cancellationToken);
        }
    }

    Console.WriteLine(
        $"  streamed write: {await filesystem.ReadStringAsync("/tmp/composed.txt", cancellationToken)}"
    );
    await filesystem.RemoveDirAsync("/tmp/work", cancellationToken);
    Console.WriteLine("Filesystem example passed.");
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
