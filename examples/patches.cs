#:project ../src/Microsandbox/Microsandbox.csproj

using Microsandbox;

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
var cancellationToken = timeout.Token;
var client = LoadClient();
var name = $"dotnet-patches-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
var hostDirectory = Directory.CreateTempSubdirectory("dotnet-sdk-patches-");
Sandbox? sandbox = null;

try
{
    var configPath = Path.Combine(hostDirectory.FullName, "config.toml");
    var scriptsPath = Directory
        .CreateDirectory(Path.Combine(hostDirectory.FullName, "scripts"))
        .FullName;
    await File.WriteAllTextAsync(configPath, "staged = true\n", cancellationToken);
    await File.WriteAllTextAsync(
        Path.Combine(scriptsPath, "hello.sh"),
        "#!/bin/sh\necho hello-from-script\n",
        cancellationToken
    );

    sandbox = await client.CreateAsync(
        name,
        new SandboxOptions
        {
            Image = "alpine:3.20",
            Patches =
            [
                new SandboxPatchOptions
                {
                    Kind = "text",
                    Path = "/etc/greeting.txt",
                    Content = "hello from a patched rootfs\n",
                },
                new SandboxPatchOptions
                {
                    Kind = "append",
                    Path = "/etc/profile",
                    Content = "\n# dotnet-patches-example\nexport PATCHED=1\n",
                },
                new SandboxPatchOptions
                {
                    Kind = "mkdir",
                    Path = "/opt/dotnet-sdk",
                    Mode = 493,
                },
                new SandboxPatchOptions
                {
                    Kind = "symlink",
                    Target = "/etc/greeting.txt",
                    Link = "/etc/greeting.link",
                },
                new SandboxPatchOptions
                {
                    Kind = "copy_file",
                    Source = configPath,
                    Destination = "/etc/dotnet-sdk-config.toml",
                },
                new SandboxPatchOptions
                {
                    Kind = "copy_dir",
                    Source = scriptsPath,
                    Destination = "/opt/dotnet-sdk/scripts",
                },
                new SandboxPatchOptions { Kind = "remove", Path = "/etc/motd" },
            ],
        },
        cancellationToken
    );

    await Check(
        sandbox,
        "Text",
        "cat /etc/greeting.txt",
        "hello from a patched rootfs",
        cancellationToken
    );
    await Check(
        sandbox,
        "Append",
        "grep dotnet-patches-example /etc/profile",
        "dotnet-patches-example",
        cancellationToken
    );
    await Check(
        sandbox,
        "Mkdir",
        "test -d /opt/dotnet-sdk && echo dir-exists",
        "dir-exists",
        cancellationToken
    );
    await Check(
        sandbox,
        "Symlink",
        "readlink /etc/greeting.link",
        "/etc/greeting.txt",
        cancellationToken
    );
    await Check(
        sandbox,
        "CopyFile",
        "cat /etc/dotnet-sdk-config.toml",
        "staged = true",
        cancellationToken
    );
    await Check(
        sandbox,
        "CopyDir",
        "sh /opt/dotnet-sdk/scripts/hello.sh",
        "hello-from-script",
        cancellationToken
    );
    await Check(
        sandbox,
        "Remove",
        "test -e /etc/motd && echo present || echo absent",
        "absent",
        cancellationToken
    );
    Console.WriteLine("Patches example passed.");
}
finally
{
    if (sandbox is not null)
    {
        await BestEffort(() => sandbox.StopAsync(cancellationToken: CancellationToken.None));
        await BestEffort(async () => await sandbox.DisposeAsync());
    }

    await BestEffort(() => client.RemoveAsync(name));
    hostDirectory.Delete(recursive: true);
}

static async Task Check(
    Sandbox sandbox,
    string label,
    string command,
    string expected,
    CancellationToken cancellationToken
)
{
    var result = await sandbox.ShellAsync(command, cancellationToken);
    var output = result.StandardOutput + result.StandardError;
    if (!result.IsSuccess || !output.Contains(expected))
    {
        throw new InvalidOperationException($"{label} patch did not produce {expected}: {output}");
    }

    Console.WriteLine($"  {label, -8} {expected}");
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
