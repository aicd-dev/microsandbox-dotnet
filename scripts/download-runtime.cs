using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

var sdkDirectory = Directory.GetCurrentDirectory();
var projectPath = Path.Combine(sdkDirectory, "src", "Microsandbox", "Microsandbox.csproj");
if (!File.Exists(projectPath))
{
    throw new InvalidOperationException("Run this file from the microsandbox-dotnet repository root.");
}

var platform = CurrentPlatform();
var runtimeDirectory = Path.Combine(sdkDirectory, ".runtime");
var temporaryDirectory = $"{runtimeDirectory}.tmp-{Guid.NewGuid():N}";
string? backupDirectory = null;

Directory.CreateDirectory(temporaryDirectory);
try
{
    using var http = new HttpClient();
    http.DefaultRequestHeaders.UserAgent.ParseAdd("microsandbox-dotnet-examples");

    var releaseJson = await http.GetByteArrayAsync(
        "https://api.github.com/repos/superradcompany/microsandbox/releases/latest");
    using var release = JsonDocument.Parse(releaseJson);
    var tag = release.RootElement.GetProperty("tag_name").GetString()
        ?? throw new InvalidDataException("Latest GitHub release did not contain tag_name");
    var version = tag.TrimStart('v');
    var releaseBase = $"https://github.com/superradcompany/microsandbox/releases/download/{tag}";
    var bundleName = $"microsandbox-{platform.ReleaseTarget}.{platform.BundleExtension}";
    var ffiName = $"libmicrosandbox_go_ffi-{platform.FfiTarget}.{platform.FfiExtension}";

    Console.WriteLine($"Downloading microsandbox v{version} for {platform.ReleaseTarget}...");
    var checksumsTask = http.GetByteArrayAsync($"{releaseBase}/checksums.sha256");
    var bundleTask = http.GetByteArrayAsync($"{releaseBase}/{bundleName}");
    var ffiTask = http.GetByteArrayAsync($"{releaseBase}/{ffiName}");
    await Task.WhenAll(checksumsTask, bundleTask, ffiTask);

    var checksums = System.Text.Encoding.UTF8.GetString(await checksumsTask);
    var bundle = await bundleTask;
    var ffi = await ffiTask;
    VerifyChecksum(bundleName, bundle, checksums);
    VerifyChecksum(ffiName, ffi, checksums);

    ExtractBundle(bundle, platform.BundleExtension, temporaryDirectory);
    var ffiPath = Path.Combine(temporaryDirectory, ffiName);
    await File.WriteAllBytesAsync(ffiPath, ffi);

    var msbPath = Path.Combine(temporaryDirectory, platform.MsbFilename);
    if (!File.Exists(msbPath))
    {
        throw new InvalidDataException($"{bundleName} did not contain {platform.MsbFilename}");
    }

    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(msbPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        CreateKrunfwLinks(temporaryDirectory);
    }

    var finalMsbPath = Path.Combine(runtimeDirectory, platform.MsbFilename);
    var finalFfiPath = Path.Combine(runtimeDirectory, ffiName);
    WriteEnvironmentFiles(temporaryDirectory, finalMsbPath, finalFfiPath, version);
    await File.WriteAllTextAsync(Path.Combine(temporaryDirectory, "version"), version + Environment.NewLine);

    if (Directory.Exists(runtimeDirectory))
    {
        backupDirectory = $"{runtimeDirectory}.backup-{Guid.NewGuid():N}";
        Directory.Move(runtimeDirectory, backupDirectory);
    }

    try
    {
        Directory.Move(temporaryDirectory, runtimeDirectory);
    }
    catch
    {
        if (backupDirectory is not null && !Directory.Exists(runtimeDirectory))
        {
            Directory.Move(backupDirectory, runtimeDirectory);
            backupDirectory = null;
        }

        throw;
    }

    if (backupDirectory is not null)
    {
        try
        {
            Directory.Delete(backupDirectory, recursive: true);
            backupDirectory = null;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Warning: could not remove previous runtime at {backupDirectory}: {exception.Message}");
        }
    }

    Console.WriteLine("Release runtime downloaded and checksums verified.");
    Console.WriteLine(OperatingSystem.IsWindows()
        ? @"Next: . .\.runtime\env.ps1"
        : "Next: source .runtime/env.sh");
}
finally
{
    if (Directory.Exists(temporaryDirectory))
    {
        Directory.Delete(temporaryDirectory, recursive: true);
    }
}

static PlatformAssets CurrentPlatform()
{
    var architecture = RuntimeInformation.OSArchitecture;
    if (OperatingSystem.IsMacOS() && architecture == Architecture.Arm64)
    {
        return new("darwin-aarch64", "darwin-arm64", "tar.gz", "dylib", "msb");
    }

    if (OperatingSystem.IsLinux() && architecture is Architecture.X64 or Architecture.Arm64)
    {
        var releaseArchitecture = architecture == Architecture.X64 ? "x86_64" : "aarch64";
        var ffiArchitecture = architecture == Architecture.X64 ? "amd64" : "arm64";
        return new($"linux-{releaseArchitecture}", $"linux-{ffiArchitecture}", "tar.gz", "so", "msb");
    }

    if (OperatingSystem.IsWindows() && architecture is Architecture.X64 or Architecture.Arm64)
    {
        var releaseArchitecture = architecture == Architecture.X64 ? "x86_64" : "aarch64";
        var ffiArchitecture = architecture == Architecture.X64 ? "amd64" : "arm64";
        return new($"windows-{releaseArchitecture}", $"windows-{ffiArchitecture}", "zip", "dll", "msb.exe");
    }

    throw new PlatformNotSupportedException($"Unsupported platform: {RuntimeInformation.OSDescription} {architecture}");
}

static void VerifyChecksum(string filename, byte[] content, string manifest)
{
    var expected = manifest
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        .Where(parts => parts.Length >= 2 && parts[1].TrimStart('*') == filename)
        .Select(parts => parts[0])
        .SingleOrDefault()
        ?? throw new InvalidDataException($"checksums.sha256 did not contain {filename}");
    var actual = Convert.ToHexStringLower(SHA256.HashData(content));
    if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidDataException($"Checksum mismatch for {filename}");
    }
}

static void ExtractBundle(byte[] bundle, string extension, string destination)
{
    using var input = new MemoryStream(bundle);
    if (extension == "zip")
    {
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)))
        {
            entry.ExtractToFile(Path.Combine(destination, entry.Name), overwrite: true);
        }

        return;
    }

    using var gzip = new GZipStream(input, CompressionMode.Decompress);
    using var reader = new TarReader(gzip);
    while (reader.GetNextEntry() is { } entry)
    {
        var filename = Path.GetFileName(entry.Name);
        if (string.IsNullOrEmpty(filename) || entry.DataStream is null)
        {
            continue;
        }

        using var output = File.Create(Path.Combine(destination, filename));
        entry.DataStream.CopyTo(output);
    }
}

static void CreateKrunfwLinks(string directory)
{
    var libraries = Directory.GetFiles(directory, "libkrunfw*")
        .Select(Path.GetFileName)
        .OfType<string>()
        .ToArray();
    if (OperatingSystem.IsMacOS())
    {
        var library = libraries.Single(name => name.StartsWith("libkrunfw.", StringComparison.Ordinal) && name.EndsWith(".dylib", StringComparison.Ordinal));
        File.CreateSymbolicLink(Path.Combine(directory, "libkrunfw.dylib"), library);
        return;
    }

    var versioned = libraries.Single(name => name.Count(character => character == '.') >= 4);
    var components = versioned.Split('.');
    var abiName = $"libkrunfw.so.{components[2]}";
    File.CreateSymbolicLink(Path.Combine(directory, abiName), versioned);
    File.CreateSymbolicLink(Path.Combine(directory, "libkrunfw.so"), abiName);
}

static void WriteEnvironmentFiles(string directory, string msbPath, string ffiPath, string version)
{
    static string ShellQuote(string value) => $"'{value.Replace("'", "'\"'\"'")}'";
    static string PowerShellQuote(string value) => $"'{value.Replace("'", "''")}'";

    File.WriteAllText(Path.Combine(directory, "env.sh"),
        $"export MICROSANDBOX_MSB_PATH={ShellQuote(msbPath)}\n" +
        $"export MICROSANDBOX_FFI_LIBRARY={ShellQuote(ffiPath)}\n" +
        $"export MICROSANDBOX_RELEASE_VERSION={ShellQuote(version)}\n");
    File.WriteAllText(Path.Combine(directory, "env.ps1"),
        $"$env:MICROSANDBOX_MSB_PATH = {PowerShellQuote(msbPath)}\n" +
        $"$env:MICROSANDBOX_FFI_LIBRARY = {PowerShellQuote(ffiPath)}\n" +
        $"$env:MICROSANDBOX_RELEASE_VERSION = {PowerShellQuote(version)}\n");
}

internal sealed record PlatformAssets(
    string ReleaseTarget,
    string FfiTarget,
    string BundleExtension,
    string FfiExtension,
    string MsbFilename);
