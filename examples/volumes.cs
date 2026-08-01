#:project ../src/Microsandbox/Microsandbox.csproj

using Microsandbox;

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
var cancellationToken = timeout.Token;
var client = LoadClient();
var name = $"dotnet-volume-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
var removed = false;

try
{
    var volume = await client.Volumes.CreateAsync(name, new VolumeCreateOptions
    {
        QuotaMiB = 64,
        Labels = new Dictionary<string, string>
        {
            ["team"] = "agents",
            ["tier"] = "example",
        },
    }, cancellationToken);
    Require(volume.Name == name, "created volume name did not match");

    var volumes = await client.Volumes.ListAsync(cancellationToken);
    Require(volumes.Any(candidate => candidate.Name == name), "created volume was not listed");
    Console.WriteLine($"  volume visible in list ({volumes.Count} total)");

    try
    {
        await client.Volumes.CreateAsync(name, cancellationToken: cancellationToken);
        throw new InvalidOperationException("duplicate volume creation unexpectedly succeeded");
    }
    catch (MicrosandboxException exception) when (exception.Kind == "volume_already_exists")
    {
        Console.WriteLine($"  duplicate create rejected: {exception.Kind}");
    }

    await client.Volumes.RemoveAsync(name, cancellationToken);
    removed = true;
    volumes = await client.Volumes.ListAsync(cancellationToken);
    Require(volumes.All(candidate => candidate.Name != name), "removed volume was still listed");
    Console.WriteLine("Volumes example passed.");
}
finally
{
    if (!removed)
    {
        await BestEffort(() => client.Volumes.RemoveAsync(name));
    }
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
