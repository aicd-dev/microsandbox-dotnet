#:project ../src/Microsandbox/Microsandbox.csproj

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsandbox;

const ushort guestPort = 7777;
const string payload = "hello-from-microvm";
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
var cancellationToken = timeout.Token;
var client = LoadClient();
var name = $"dotnet-ports-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
var hostPort = ReserveHostPort();
Sandbox? sandbox = null;

try
{
    Console.WriteLine($"Publishing host port {hostPort} to guest port {guestPort}...");
    sandbox = await client.CreateAsync(name, new SandboxOptions
    {
        Image = "alpine:3.20",
        Ports = new Dictionary<ushort, ushort> { [hostPort] = guestPort },
    }, cancellationToken);

    await using var command = await sandbox.ShellStreamingAsync(
        $"printf '{payload}' | nc -l -p {guestPort}",
        cancellationToken: cancellationToken);

    using var connection = await ConnectWithRetry("127.0.0.1", hostPort, cancellationToken);
    var buffer = new byte[Encoding.UTF8.GetByteCount(payload)];
    await connection.GetStream().ReadExactlyAsync(buffer, cancellationToken);
    var received = Encoding.UTF8.GetString(buffer);
    Require(received == payload, $"expected {payload}, received {received}");
    Console.WriteLine($"  received {buffer.Length} bytes: {received}");

    var exitCode = await command.WaitAsync(cancellationToken);
    Require(exitCode == 0, $"guest listener exited with {exitCode}");
    Console.WriteLine("Ports example passed.");
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

static ushort ReserveHostPort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = checked((ushort)((IPEndPoint)listener.LocalEndpoint).Port);
    listener.Stop();
    return port;
}

static async Task<TcpClient> ConnectWithRetry(string host, ushort port, CancellationToken cancellationToken)
{
    Exception? lastError = null;
    for (var attempt = 0; attempt < 20; attempt++)
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port, cancellationToken);
            return client;
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            client.Dispose();
            lastError = exception;
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    throw new IOException($"could not connect to {host}:{port}", lastError);
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
