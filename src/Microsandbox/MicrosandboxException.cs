namespace Microsandbox;

/// <summary>Represents an error returned by the native microsandbox ABI.</summary>
public sealed class MicrosandboxException : Exception
{
    internal MicrosandboxException(string kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    /// <summary>Gets the stable native error category.</summary>
    public string Kind { get; }
}
