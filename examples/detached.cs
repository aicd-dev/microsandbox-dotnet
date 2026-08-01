#:project ../src/Microsandbox/Microsandbox.csproj

using Microsandbox;

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
var cancellationToken = timeout.Token;
var client = LoadClient();
var name = $"dotnet-detached-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
Sandbox? sandbox = null;
Sandbox? connected = null;
var detached = false;

try
{
    sandbox = await client.CreateAsync(name, new SandboxOptions
    {
        Image = "alpine:3.20",
        Detached = true,
    }, cancellationToken);

    Console.WriteLine($"Initial handle owns lifecycle: {sandbox.OwnsLifecycle}");
    await sandbox.ShellAsync("echo lived-through-detach > /tmp/witness", cancellationToken);
    await sandbox.DetachAsync(cancellationToken);
    detached = true;
    Console.WriteLine("Detached local handle; VM remains running.");

    var persisted = await client.ListAsync(cancellationToken: cancellationToken);
    var handle = persisted.SingleOrDefault(candidate => candidate.Name == name)
        ?? throw new InvalidOperationException("detached sandbox was not listed");
    Console.WriteLine($"Persisted status: {handle.Status}");

    connected = await handle.ConnectAsync(cancellationToken);
    Console.WriteLine($"Connected handle owns lifecycle: {connected.OwnsLifecycle}");
    var witness = await connected.ShellAsync("cat /tmp/witness", cancellationToken);
    Require(witness.StandardOutput.Contains("lived-through-detach"), "VM did not survive detach");
    Console.WriteLine("Detached example passed; witness file survived.");
}
finally
{
    if (connected is not null)
    {
        await BestEffort(() => connected.StopAsync(cancellationToken: CancellationToken.None));
        await BestEffort(async () => await connected.DisposeAsync());
    }
    else if (detached)
    {
        await BestEffort(async () =>
        {
            var handle = await client.LookupAsync(name);
            await handle.StopAsync(cancellationToken: CancellationToken.None);
        });
    }

    if (sandbox is not null)
    {
        await BestEffort(async () => await sandbox.DisposeAsync());
    }

    await BestEffort(() => client.RemoveAsync(name));
}

static MicrosandboxClient LoadClient()
{
    var client = MicrosandboxClient.Load();
    var msbPath = Environment.GetEnvironmentVariable("MICROSANDBOX_MSB_PATH");
    if (!string.IsNullOrWhiteSpace(msbPath)) client.SetMsbPath(msbPath);
    return client;
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task BestEffort(Func<Task> action)
{
    try { await action(); }
    catch (Exception exception) { Console.Error.WriteLine($"cleanup: {exception.Message}"); }
}
