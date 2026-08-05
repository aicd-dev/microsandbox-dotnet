#:project ../src/Microsandbox/Microsandbox.csproj

using Microsandbox;

const string secretValue = "super-secret-value-abcxyz";
const string placeholder = "$MY_API_KEY_PLACEHOLDER";
const string environmentVariable = "MY_API_KEY";
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
var cancellationToken = timeout.Token;
var client = LoadClient();
var name = $"dotnet-secrets-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
Sandbox? sandbox = null;

try
{
    sandbox = await client.CreateAsync(
        name,
        new SandboxOptions
        {
            Image = "alpine:3.20",
            Secrets =
            [
                new SandboxSecretOptions
                {
                    EnvironmentVariable = environmentVariable,
                    Value = secretValue,
                    AllowedHosts = ["api.example.com"],
                    Placeholder = placeholder,
                },
            ],
        },
        cancellationToken
    );

    var visible = await sandbox.ShellAsync(
        $"printenv {environmentVariable}; true",
        cancellationToken
    );
    Require(
        !visible.StandardOutput.Contains(secretValue),
        "secret value leaked into the guest environment"
    );
    Require(
        visible.StandardOutput.Contains(placeholder),
        "guest did not receive the secret placeholder"
    );
    Console.WriteLine($"  guest sees placeholder: {visible.StandardOutput.Trim()}");

    var environment = await sandbox.ShellAsync("env", cancellationToken);
    Require(
        !environment.StandardOutput.Contains(secretValue),
        "secret value appeared in the full environment"
    );
    Console.WriteLine(
        $"  scanned {environment.StandardOutput.Length} environment bytes without exposing the secret"
    );
    Console.WriteLine("Secrets example passed.");
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
