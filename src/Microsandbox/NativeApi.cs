using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsandbox;

internal sealed class NativeApi
{
    private const int BufferSize = 1 << 20;
    private const int FilesystemStreamBufferSize = 6 << 20;
    private static readonly string ManagedVersion = GetManagedVersion();

    // Matches Go: enough for the runtime's bounded 10 MiB x3 history after base64/JSON expansion.
    private const int LogsBufferSize = 48 << 20;
    private readonly FreeStringFn _freeString;
    private readonly SetMsbPathFn _setMsbPath;
    private readonly CancelAllocFn _cancelAlloc;
    private readonly CancelTriggerFn _cancelTrigger;
    private readonly CancelUnregisterFn _cancelUnregister;
    private readonly SandboxCreateFn _sandboxCreate;
    private readonly SandboxLookupFn _sandboxLookup;
    private readonly SandboxConnectFn _sandboxConnect;
    private readonly SandboxStartFn _sandboxStart;
    private readonly SandboxByNameTimeoutFn _sandboxHandleStop;
    private readonly SandboxByNameFn _sandboxHandleRequestStop;
    private readonly SandboxByNameTimeoutFn _sandboxHandleKill;
    private readonly SandboxByNameFn _sandboxHandleRequestKill;
    private readonly SandboxByNameFn _sandboxHandleRequestDrain;
    private readonly SandboxByNameFn _sandboxHandleWaitUntilStopped;
    private readonly SandboxByNameFn _sandboxHandlePing;
    private readonly SandboxByNameFn _sandboxHandleTouch;
    private readonly SandboxByNameOptionsFn _sandboxHandleModify;
    private readonly SandboxListFn _sandboxList;
    private readonly SandboxByNameFn _sandboxRemove;
    private readonly SandboxExecFn _sandboxExec;
    private readonly SandboxExecFn _sandboxExecStream;
    private readonly ExecHandleFn _execReceive;
    private readonly ExecHandleFn _execClose;
    private readonly ExecHandleFn _execCollect;
    private readonly ExecHandleFn _execWait;
    private readonly ExecHandleFn _execKill;
    private readonly ExecIdFn _execId;
    private readonly ExecSignalFn _execSignal;
    private readonly ExecResizeFn _execResize;
    private readonly ExecStdinWriteFn _execStdinWrite;
    private readonly ExecHandleFn _execStdinClose;
    private readonly SandboxStopFn _sandboxStop;
    private readonly SandboxHandleFn _sandboxRequestStop;
    private readonly SandboxHandleFn _sandboxStopAndWait;
    private readonly SandboxStopFn _sandboxKill;
    private readonly SandboxHandleFn _sandboxRequestKill;
    private readonly SandboxHandleFn _sandboxDrain;
    private readonly SandboxHandleFn _sandboxRequestDrain;
    private readonly SandboxHandleFn _sandboxWait;
    private readonly SandboxHandleFn _sandboxWaitUntilStopped;
    private readonly SandboxHandleFn _sandboxPing;
    private readonly SandboxHandleFn _sandboxTouch;
    private readonly SandboxHandleFn _sandboxDetach;
    private readonly SandboxOwnsLifecycleFn _sandboxOwnsLifecycle;
    private readonly SandboxCloseFn _sandboxClose;
    private readonly SandboxHandleOptionsFn _sandboxModify;
    private readonly SandboxExecFn _sandboxAttach;
    private readonly SandboxHandleFn _sandboxAttachShell;
    private readonly SandboxHandleFn _sandboxRemovePersisted;
    private readonly SandboxHandleOptionsFn _sandboxSshConnect;
    private readonly SandboxHandleOptionsFn _sandboxSshServer;
    private readonly SandboxExecFn _sshClientExec;
    private readonly SandboxHandleOptionsFn _sshClientAttach;
    private readonly SandboxHandleFn _sshClientSftp;
    private readonly SandboxHandleFn _sshClientClose;
    private readonly SandboxHandleFn _sshServerClose;
    private readonly SandboxHandleFn _sshServerServeConnection;
    private readonly SandboxHandleFn _sshServerServeStandardIo;
    private readonly SandboxHandleStringFn _sftpRead;
    private readonly SandboxHandleTwoStringsFn _sftpWrite;
    private readonly SandboxHandleStringFn _sftpMkdir;
    private readonly SandboxHandleStringFn _sftpRemoveFile;
    private readonly SandboxHandleStringFn _sftpRemoveDir;
    private readonly SandboxHandleTwoStringsFn _sftpRename;
    private readonly SandboxHandleStringFn _sftpRealPath;
    private readonly SandboxHandleStringFn _sftpReadLink;
    private readonly SandboxHandleTwoStringsFn _sftpSymlink;
    private readonly SandboxHandleFn _sftpClose;
    private readonly SandboxHandleStringFn _fsRead;
    private readonly SandboxHandleTwoStringsFn _fsWrite;
    private readonly SandboxHandleStringFn _fsList;
    private readonly SandboxHandleStringFn _fsStat;
    private readonly SandboxHandleTwoStringsFn _fsCopyFromHost;
    private readonly SandboxHandleTwoStringsFn _fsCopyToHost;
    private readonly SandboxHandleStringFn _fsMkdir;
    private readonly SandboxHandleStringFn _fsRemove;
    private readonly SandboxHandleStringFn _fsRemoveDir;
    private readonly SandboxHandleTwoStringsFn _fsCopy;
    private readonly SandboxHandleTwoStringsFn _fsRename;
    private readonly SandboxHandleStringFn _fsExists;
    private readonly SandboxHandleStringFn _fsReadStream;
    private readonly StreamHandleFn _fsReadStreamReceive;
    private readonly StreamCloseFn _fsReadStreamClose;
    private readonly SandboxHandleStringFn _fsWriteStream;
    private readonly StreamWriteFn _fsWriteStreamWrite;
    private readonly StreamHandleFn _fsWriteStreamClose;
    private readonly SandboxHandleOptionsFn _sandboxLogs;
    private readonly SandboxByNameOptionsFn _sandboxHandleLogs;
    private readonly SandboxHandleOptionsFn _sandboxLogStream;
    private readonly SandboxByNameOptionsFn _sandboxHandleLogStream;
    private readonly StreamHandleFn _logReceive;
    private readonly StreamCloseFn _logClose;
    private readonly SandboxHandleFn _sandboxMetrics;
    private readonly MetricsStreamStartFn _sandboxMetricsStream;
    private readonly StreamHandleFn _metricsReceive;
    private readonly StreamCloseFn _metricsClose;
    private readonly CancellableNoArgFn _allSandboxMetrics;
    private readonly SandboxByNameFn _sandboxHandleMetrics;
    private readonly TwoStringsFn _volumeCreate;
    private readonly StringFn _volumeGet;
    private readonly CancellableNoArgFn _volumeList;
    private readonly StringFn _volumeRemove;
    private readonly StringFn _imageGet;
    private readonly CancellableNoArgFn _imageList;
    private readonly StringFn _imageInspect;
    private readonly StringBoolFn _imageRemove;
    private readonly CancellableNoArgFn _imagePrune;
    private readonly TwoStringsFn _imageLoad;
    private readonly ThreeStringsFn _imageSave;
    private readonly TwoStringsFn _snapshotCreate;
    private readonly StringFn _snapshotOpen;
    private readonly StringFn _snapshotVerify;
    private readonly StringFn _snapshotGet;
    private readonly CancellableNoArgFn _snapshotList;
    private readonly StringFn _snapshotListDir;
    private readonly StringBoolFn _snapshotRemove;
    private readonly StringFn _snapshotReindex;
    private readonly ThreeStringsFn _snapshotExport;
    private readonly TwoStringsFn _snapshotImport;
    private readonly TwoStringsFn _sandboxHandleSnapshot;
    private readonly AgentSocketPathFn _agentSocketPath;
    private readonly AgentOpenFn _agentOpenSandbox;
    private readonly AgentOpenFn _agentOpenPath;
    private readonly AgentRequestFn _agentRequest;
    private readonly AgentStreamOpenFn _agentStreamOpen;
    private readonly AgentStreamNextFn _agentStreamNext;
    private readonly AgentStreamCloseFn _agentStreamClose;
    private readonly AgentSendFn _agentSend;
    private readonly AgentReadyBytesFn _agentReadyBytes;
    private readonly AgentCloseFn _agentClose;
    private readonly AgentFreeBytesFn _agentFreeBytes;
    private readonly VersionFn _version;

    private NativeApi(nint library)
    {
        _freeString = GetExport<FreeStringFn>(library, "msb_free_string");
        _setMsbPath = GetExport<SetMsbPathFn>(library, "msb_set_sdk_msb_path");
        _cancelAlloc = GetExport<CancelAllocFn>(library, "msb_cancel_alloc");
        _cancelTrigger = GetExport<CancelTriggerFn>(library, "msb_cancel_trigger");
        _cancelUnregister = GetExport<CancelUnregisterFn>(library, "msb_cancel_unregister");
        _sandboxCreate = GetExport<SandboxCreateFn>(library, "msb_sandbox_create");
        _sandboxLookup = GetExport<SandboxLookupFn>(library, "msb_sandbox_lookup");
        _sandboxConnect = GetExport<SandboxConnectFn>(library, "msb_sandbox_connect");
        _sandboxStart = GetExport<SandboxStartFn>(library, "msb_sandbox_start");
        _sandboxHandleStop = GetExport<SandboxByNameTimeoutFn>(library, "msb_sandbox_handle_stop");
        _sandboxHandleRequestStop = GetExport<SandboxByNameFn>(
            library,
            "msb_sandbox_handle_request_stop"
        );
        _sandboxHandleKill = GetExport<SandboxByNameTimeoutFn>(library, "msb_sandbox_handle_kill");
        _sandboxHandleRequestKill = GetExport<SandboxByNameFn>(
            library,
            "msb_sandbox_handle_request_kill"
        );
        _sandboxHandleRequestDrain = GetExport<SandboxByNameFn>(
            library,
            "msb_sandbox_handle_request_drain"
        );
        _sandboxHandleWaitUntilStopped = GetExport<SandboxByNameFn>(
            library,
            "msb_sandbox_handle_wait_until_stopped"
        );
        _sandboxHandlePing = GetExport<SandboxByNameFn>(library, "msb_sandbox_handle_ping");
        _sandboxHandleTouch = GetExport<SandboxByNameFn>(library, "msb_sandbox_handle_touch");
        _sandboxHandleModify = GetExport<SandboxByNameOptionsFn>(
            library,
            "msb_sandbox_handle_modify"
        );
        _sandboxList = GetExport<SandboxListFn>(library, "msb_sandbox_list");
        _sandboxRemove = GetExport<SandboxByNameFn>(library, "msb_sandbox_remove");
        _sandboxExec = GetExport<SandboxExecFn>(library, "msb_sandbox_exec");
        _sandboxExecStream = GetExport<SandboxExecFn>(library, "msb_sandbox_exec_stream");
        _execReceive = GetExport<ExecHandleFn>(library, "msb_exec_recv");
        _execClose = GetExport<ExecHandleFn>(library, "msb_exec_close");
        _execCollect = GetExport<ExecHandleFn>(library, "msb_exec_collect");
        _execWait = GetExport<ExecHandleFn>(library, "msb_exec_wait");
        _execKill = GetExport<ExecHandleFn>(library, "msb_exec_kill");
        _execId = GetExport<ExecIdFn>(library, "msb_exec_id");
        _execSignal = GetExport<ExecSignalFn>(library, "msb_exec_signal");
        _execResize = GetExport<ExecResizeFn>(library, "msb_exec_resize");
        _execStdinWrite = GetExport<ExecStdinWriteFn>(library, "msb_exec_stdin_write");
        _execStdinClose = GetExport<ExecHandleFn>(library, "msb_exec_stdin_close");
        _sandboxStop = GetExport<SandboxStopFn>(library, "msb_sandbox_stop");
        _sandboxRequestStop = GetExport<SandboxHandleFn>(library, "msb_sandbox_request_stop");
        _sandboxStopAndWait = GetExport<SandboxHandleFn>(library, "msb_sandbox_stop_and_wait");
        _sandboxKill = GetExport<SandboxStopFn>(library, "msb_sandbox_kill");
        _sandboxRequestKill = GetExport<SandboxHandleFn>(library, "msb_sandbox_request_kill");
        _sandboxDrain = GetExport<SandboxHandleFn>(library, "msb_sandbox_drain");
        _sandboxRequestDrain = GetExport<SandboxHandleFn>(library, "msb_sandbox_request_drain");
        _sandboxWait = GetExport<SandboxHandleFn>(library, "msb_sandbox_wait");
        _sandboxWaitUntilStopped = GetExport<SandboxHandleFn>(
            library,
            "msb_sandbox_wait_until_stopped"
        );
        _sandboxPing = GetExport<SandboxHandleFn>(library, "msb_sandbox_ping");
        _sandboxTouch = GetExport<SandboxHandleFn>(library, "msb_sandbox_touch");
        _sandboxDetach = GetExport<SandboxHandleFn>(library, "msb_sandbox_detach");
        _sandboxOwnsLifecycle = GetExport<SandboxOwnsLifecycleFn>(
            library,
            "msb_sandbox_owns_lifecycle"
        );
        _sandboxClose = GetExport<SandboxCloseFn>(library, "msb_sandbox_close");
        _sandboxModify = GetExport<SandboxHandleOptionsFn>(library, "msb_sandbox_modify");
        _sandboxAttach = GetExport<SandboxExecFn>(library, "msb_sandbox_attach");
        _sandboxAttachShell = GetExport<SandboxHandleFn>(library, "msb_sandbox_attach_shell");
        _sandboxRemovePersisted = GetExport<SandboxHandleFn>(
            library,
            "msb_sandbox_remove_persisted"
        );
        _sandboxSshConnect = GetExport<SandboxHandleOptionsFn>(library, "msb_sandbox_ssh_connect");
        _sandboxSshServer = GetExport<SandboxHandleOptionsFn>(library, "msb_sandbox_ssh_server");
        _sshClientExec = GetExport<SandboxExecFn>(library, "msb_ssh_client_exec");
        _sshClientAttach = GetExport<SandboxHandleOptionsFn>(library, "msb_ssh_client_attach");
        _sshClientSftp = GetExport<SandboxHandleFn>(library, "msb_ssh_client_sftp");
        _sshClientClose = GetExport<SandboxHandleFn>(library, "msb_ssh_client_close");
        _sshServerClose = GetExport<SandboxHandleFn>(library, "msb_ssh_server_close");
        _sshServerServeConnection = GetExport<SandboxHandleFn>(
            library,
            "msb_ssh_server_serve_connection"
        );
        _sshServerServeStandardIo = GetExport<SandboxHandleFn>(
            library,
            "msb_ssh_server_serve_stdio"
        );
        _sftpRead = GetExport<SandboxHandleStringFn>(library, "msb_sftp_read");
        _sftpWrite = GetExport<SandboxHandleTwoStringsFn>(library, "msb_sftp_write");
        _sftpMkdir = GetExport<SandboxHandleStringFn>(library, "msb_sftp_mkdir");
        _sftpRemoveFile = GetExport<SandboxHandleStringFn>(library, "msb_sftp_remove_file");
        _sftpRemoveDir = GetExport<SandboxHandleStringFn>(library, "msb_sftp_remove_dir");
        _sftpRename = GetExport<SandboxHandleTwoStringsFn>(library, "msb_sftp_rename");
        _sftpRealPath = GetExport<SandboxHandleStringFn>(library, "msb_sftp_real_path");
        _sftpReadLink = GetExport<SandboxHandleStringFn>(library, "msb_sftp_read_link");
        _sftpSymlink = GetExport<SandboxHandleTwoStringsFn>(library, "msb_sftp_symlink");
        _sftpClose = GetExport<SandboxHandleFn>(library, "msb_sftp_close");
        _fsRead = GetExport<SandboxHandleStringFn>(library, "msb_fs_read");
        _fsWrite = GetExport<SandboxHandleTwoStringsFn>(library, "msb_fs_write");
        _fsList = GetExport<SandboxHandleStringFn>(library, "msb_fs_list");
        _fsStat = GetExport<SandboxHandleStringFn>(library, "msb_fs_stat");
        _fsCopyFromHost = GetExport<SandboxHandleTwoStringsFn>(library, "msb_fs_copy_from_host");
        _fsCopyToHost = GetExport<SandboxHandleTwoStringsFn>(library, "msb_fs_copy_to_host");
        _fsMkdir = GetExport<SandboxHandleStringFn>(library, "msb_fs_mkdir");
        _fsRemove = GetExport<SandboxHandleStringFn>(library, "msb_fs_remove");
        _fsRemoveDir = GetExport<SandboxHandleStringFn>(library, "msb_fs_remove_dir");
        _fsCopy = GetExport<SandboxHandleTwoStringsFn>(library, "msb_fs_copy");
        _fsRename = GetExport<SandboxHandleTwoStringsFn>(library, "msb_fs_rename");
        _fsExists = GetExport<SandboxHandleStringFn>(library, "msb_fs_exists");
        _fsReadStream = GetExport<SandboxHandleStringFn>(library, "msb_fs_read_stream");
        _fsReadStreamReceive = GetExport<StreamHandleFn>(library, "msb_fs_read_stream_recv");
        _fsReadStreamClose = GetExport<StreamCloseFn>(library, "msb_fs_read_stream_close");
        _fsWriteStream = GetExport<SandboxHandleStringFn>(library, "msb_fs_write_stream");
        _fsWriteStreamWrite = GetExport<StreamWriteFn>(library, "msb_fs_write_stream_write");
        _fsWriteStreamClose = GetExport<StreamHandleFn>(library, "msb_fs_write_stream_close");
        _sandboxLogs = GetExport<SandboxHandleOptionsFn>(library, "msb_sandbox_logs");
        _sandboxHandleLogs = GetExport<SandboxByNameOptionsFn>(library, "msb_sandbox_handle_logs");
        _sandboxLogStream = GetExport<SandboxHandleOptionsFn>(library, "msb_sandbox_log_stream");
        _sandboxHandleLogStream = GetExport<SandboxByNameOptionsFn>(
            library,
            "msb_sandbox_handle_log_stream"
        );
        _logReceive = GetExport<StreamHandleFn>(library, "msb_log_recv");
        _logClose = GetExport<StreamCloseFn>(library, "msb_log_close");
        _sandboxMetrics = GetExport<SandboxHandleFn>(library, "msb_sandbox_metrics");
        _sandboxMetricsStream = GetExport<MetricsStreamStartFn>(
            library,
            "msb_sandbox_metrics_stream"
        );
        _metricsReceive = GetExport<StreamHandleFn>(library, "msb_metrics_recv");
        _metricsClose = GetExport<StreamCloseFn>(library, "msb_metrics_close");
        _allSandboxMetrics = GetExport<CancellableNoArgFn>(library, "msb_all_sandbox_metrics");
        _sandboxHandleMetrics = GetExport<SandboxByNameFn>(library, "msb_sandbox_handle_metrics");
        _volumeCreate = GetExport<TwoStringsFn>(library, "msb_volume_create");
        _volumeGet = GetExport<StringFn>(library, "msb_volume_get");
        _volumeList = GetExport<CancellableNoArgFn>(library, "msb_volume_list");
        _volumeRemove = GetExport<StringFn>(library, "msb_volume_remove");
        _imageGet = GetExport<StringFn>(library, "msb_image_get");
        _imageList = GetExport<CancellableNoArgFn>(library, "msb_image_list");
        _imageInspect = GetExport<StringFn>(library, "msb_image_inspect");
        _imageRemove = GetExport<StringBoolFn>(library, "msb_image_remove");
        _imagePrune = GetExport<CancellableNoArgFn>(library, "msb_image_prune");
        _imageLoad = GetExport<TwoStringsFn>(library, "msb_image_load");
        _imageSave = GetExport<ThreeStringsFn>(library, "msb_image_save");
        _snapshotCreate = GetExport<TwoStringsFn>(library, "msb_snapshot_create");
        _snapshotOpen = GetExport<StringFn>(library, "msb_snapshot_open");
        _snapshotVerify = GetExport<StringFn>(library, "msb_snapshot_verify");
        _snapshotGet = GetExport<StringFn>(library, "msb_snapshot_get");
        _snapshotList = GetExport<CancellableNoArgFn>(library, "msb_snapshot_list");
        _snapshotListDir = GetExport<StringFn>(library, "msb_snapshot_list_dir");
        _snapshotRemove = GetExport<StringBoolFn>(library, "msb_snapshot_remove");
        _snapshotReindex = GetExport<StringFn>(library, "msb_snapshot_reindex");
        _snapshotExport = GetExport<ThreeStringsFn>(library, "msb_snapshot_export");
        _snapshotImport = GetExport<TwoStringsFn>(library, "msb_snapshot_import");
        _sandboxHandleSnapshot = GetExport<TwoStringsFn>(library, "msb_sandbox_handle_snapshot");
        _agentSocketPath = GetExport<AgentSocketPathFn>(library, "msb_agent_socket_path");
        _agentOpenSandbox = GetExport<AgentOpenFn>(library, "msb_agent_open_sandbox");
        _agentOpenPath = GetExport<AgentOpenFn>(library, "msb_agent_open_path");
        _agentRequest = GetExport<AgentRequestFn>(library, "msb_agent_request");
        _agentStreamOpen = GetExport<AgentStreamOpenFn>(library, "msb_agent_stream_open");
        _agentStreamNext = GetExport<AgentStreamNextFn>(library, "msb_agent_stream_next");
        _agentStreamClose = GetExport<AgentStreamCloseFn>(library, "msb_agent_stream_close");
        _agentSend = GetExport<AgentSendFn>(library, "msb_agent_send");
        _agentReadyBytes = GetExport<AgentReadyBytesFn>(library, "msb_agent_ready_bytes");
        _agentClose = GetExport<AgentCloseFn>(library, "msb_agent_close");
        _agentFreeBytes = GetExport<AgentFreeBytesFn>(library, "msb_agent_free_bytes");
        _version = GetExport<VersionFn>(library, "msb_version");
    }

    internal static NativeApi Load(string? explicitPath)
    {
        var candidates = CandidatePaths(explicitPath).ToArray();
        foreach (var candidate in candidates)
        {
            if (NativeLibrary.TryLoad(candidate, out var library))
            {
                try
                {
                    var native = new NativeApi(library);
                    ValidateVersion(native.Version(), ManagedVersion);
                    // Match the Go SDK: keep compatible libraries loaded for process lifetime so handles stay valid.
                    return native;
                }
                catch (Exception exception)
                    when (exception
                            is EntryPointNotFoundException
                                or InvalidDataException
                                or JsonException
                                or MicrosandboxException
                    )
                {
                    NativeLibrary.Free(library);
                }
            }
        }

        throw new DllNotFoundException(
            $"Could not load the microsandbox native library. Tried: {string.Join(", ", candidates)}"
        );
    }

    internal string Version() => ParseVersion(Invoke((buffer, length) => _version(buffer, length)));

    internal void SetMsbPath(string path)
    {
        using var nativePath = new Utf8String(path);
        _setMsbPath(nativePath.Pointer);
    }

    internal async Task<ulong> CreateAsync(
        string name,
        string optionsJson,
        CancellationToken cancellationToken
    )
    {
        using var nativeName = new Utf8String(name);
        using var nativeOptions = new Utf8String(optionsJson);
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxCreate(
                        cancelId,
                        nativeName.Pointer,
                        nativeOptions.Pointer,
                        buffer,
                        length
                    ),
                cancellationToken
            )
            .ConfigureAwait(false);
        try
        {
            return Deserialize<CreateResponse>(json).Handle;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            var handle = SalvageHandle(json);
            if (handle != 0)
            {
                try
                {
                    await CloseAsync(handle, CancellationToken.None).ConfigureAwait(false);
                }
                catch (MicrosandboxException)
                {
                    // Preserve the response parsing failure that made ownership unsafe.
                }
            }

            throw;
        }
    }

    internal async Task<SandboxHandle> LookupAsync(string name, CancellationToken cancellationToken)
    {
        using var nativeName = new Utf8String(name);
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxLookup(cancelId, nativeName.Pointer, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
        return new SandboxHandle(this, ParseMetadata(json));
    }

    internal async Task<Sandbox> ConnectAsync(string name, CancellationToken cancellationToken)
    {
        using var nativeName = new Utf8String(name);
        var handle = await AcquireHandleAsync(
                (cancelId, buffer, length) =>
                    _sandboxConnect(cancelId, nativeName.Pointer, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
        return new Sandbox(this, name, handle);
    }

    internal async Task<Sandbox> StartAsync(
        string name,
        bool detached,
        CancellationToken cancellationToken
    )
    {
        using var nativeName = new Utf8String(name);
        var handle = await AcquireHandleAsync(
                (cancelId, buffer, length) =>
                    _sandboxStart(cancelId, nativeName.Pointer, detached, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
        return new Sandbox(this, name, handle);
    }

    internal async Task<IReadOnlyList<SandboxHandle>> ListAsync(
        string filterJson,
        CancellationToken cancellationToken
    )
    {
        using var nativeFilter = new Utf8String(filterJson);
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxList(cancelId, nativeFilter.Pointer, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
        return Deserialize<List<SandboxMetadataResponse>>(json)
            .Select(item => new SandboxHandle(this, item.ToMetadata()))
            .ToArray();
    }

    internal Task RemoveAsync(string name, CancellationToken cancellationToken) =>
        InvokeByNameAsync(_sandboxRemove, name, cancellationToken);

    internal Task StopByNameAsync(
        string name,
        ulong timeoutMilliseconds,
        CancellationToken cancellationToken
    ) => InvokeByNameAsync(_sandboxHandleStop, name, timeoutMilliseconds, cancellationToken);

    internal Task RequestStopByNameAsync(string name, CancellationToken cancellationToken) =>
        InvokeByNameAsync(_sandboxHandleRequestStop, name, cancellationToken);

    internal Task KillByNameAsync(
        string name,
        ulong timeoutMilliseconds,
        CancellationToken cancellationToken
    ) => InvokeByNameAsync(_sandboxHandleKill, name, timeoutMilliseconds, cancellationToken);

    internal Task RequestKillByNameAsync(string name, CancellationToken cancellationToken) =>
        InvokeByNameAsync(_sandboxHandleRequestKill, name, cancellationToken);

    internal Task RequestDrainByNameAsync(string name, CancellationToken cancellationToken) =>
        InvokeByNameAsync(_sandboxHandleRequestDrain, name, cancellationToken);

    internal async Task<SandboxStopResult> WaitUntilStoppedByNameAsync(
        string name,
        CancellationToken cancellationToken
    ) =>
        ParseStopResult(
            await InvokeByNameWithResultAsync(
                    _sandboxHandleWaitUntilStopped,
                    name,
                    cancellationToken
                )
                .ConfigureAwait(false)
        );

    internal async Task<SandboxPingResult> PingByNameAsync(
        string name,
        CancellationToken cancellationToken
    ) =>
        ParsePingResult(
            await InvokeByNameWithResultAsync(_sandboxHandlePing, name, cancellationToken)
                .ConfigureAwait(false)
        );

    internal async Task<SandboxTouchResult> TouchByNameAsync(
        string name,
        CancellationToken cancellationToken
    ) =>
        ParseTouchResult(
            await InvokeByNameWithResultAsync(_sandboxHandleTouch, name, cancellationToken)
                .ConfigureAwait(false)
        );

    internal async Task<string> ModifyByNameAsync(
        string name,
        string optionsJson,
        CancellationToken cancellationToken
    )
    {
        using var nativeName = new Utf8String(name);
        using var nativeOptions = new Utf8String(optionsJson);
        return await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxHandleModify(
                        cancelId,
                        nativeName.Pointer,
                        nativeOptions.Pointer,
                        buffer,
                        length
                    ),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    internal async Task<ExecResult> ExecAsync(
        ulong handle,
        string command,
        string optionsJson,
        CancellationToken cancellationToken
    )
    {
        using var nativeCommand = new Utf8String(command);
        using var nativeOptions = new Utf8String(optionsJson);
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxExec(
                        cancelId,
                        handle,
                        nativeCommand.Pointer,
                        nativeOptions.Pointer,
                        buffer,
                        length
                    ),
                cancellationToken
            )
            .ConfigureAwait(false);
        var response = Deserialize<ExecResponse>(json);
        return new ExecResult(response.Stdout, response.Stderr, response.ExitCode ?? -1);
    }

    internal async Task<ulong> ExecStreamAsync(
        ulong handle,
        string command,
        string optionsJson,
        CancellationToken cancellationToken
    )
    {
        using var nativeCommand = new Utf8String(command);
        using var nativeOptions = new Utf8String(optionsJson);
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxExecStream(
                        cancelId,
                        handle,
                        nativeCommand.Pointer,
                        nativeOptions.Pointer,
                        buffer,
                        length
                    ),
                cancellationToken
            )
            .ConfigureAwait(false);
        return await ParseOwnedHandleAsync(json, "exec_handle", ExecCloseAsync)
            .ConfigureAwait(false);
    }

    internal string ExecId(ulong handle) =>
        Deserialize<IdResponse>(Invoke((buffer, length) => _execId(handle, buffer, length))).Id;

    internal async Task<ExecEvent> ExecReceiveAsync(
        ulong handle,
        CancellationToken cancellationToken
    ) =>
        ParseExecEvent(
            await InvokeExecAsync(_execReceive, handle, cancellationToken).ConfigureAwait(false)
        );

    internal async Task<ExecResult> ExecCollectAsync(
        ulong handle,
        CancellationToken cancellationToken
    ) =>
        ParseExecCollect(
            await InvokeExecAsync(_execCollect, handle, cancellationToken).ConfigureAwait(false)
        );

    internal async Task<int> ExecWaitAsync(ulong handle, CancellationToken cancellationToken) =>
        Deserialize<ExecWaitResponse>(
            await InvokeExecAsync(_execWait, handle, cancellationToken).ConfigureAwait(false)
        ).ExitCode;

    internal Task ExecKillAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeExecAsync(_execKill, handle, cancellationToken);

    internal Task ExecSignalAsync(ulong handle, int signal, CancellationToken cancellationToken) =>
        InvokeAsync(
            (cancelId, buffer, length) => _execSignal(cancelId, handle, signal, buffer, length),
            cancellationToken
        );

    internal Task ExecResizeAsync(
        ulong handle,
        ushort rows,
        ushort columns,
        CancellationToken cancellationToken
    ) =>
        InvokeAsync(
            (cancelId, buffer, length) =>
                _execResize(cancelId, handle, rows, columns, buffer, length),
            cancellationToken
        );

    internal async Task ExecStdinWriteAsync(
        ulong handle,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken
    )
    {
        using var encoded = new Utf8String(Convert.ToBase64String(data.Span));
        await InvokeAsync(
                (cancelId, buffer, length) =>
                    _execStdinWrite(cancelId, handle, encoded.Pointer, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    internal Task ExecStdinCloseAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeAsync(
            (cancelId, buffer, length) => _execStdinClose(cancelId, handle, buffer, length),
            cancellationToken,
            checkInitialCancellation: false
        );

    internal Task ExecCloseAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeExecAsync(_execClose, handle, cancellationToken);

    internal async Task StopAsync(
        ulong handle,
        ulong timeoutMilliseconds,
        CancellationToken cancellationToken
    )
    {
        await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxStop(cancelId, handle, timeoutMilliseconds, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    internal Task RequestStopAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeHandleAsync(_sandboxRequestStop, handle, cancellationToken);

    internal async Task<int?> StopAndWaitAsync(ulong handle, CancellationToken cancellationToken) =>
        Deserialize<WaitResponse>(
            await InvokeHandleAsync(_sandboxStopAndWait, handle, cancellationToken)
                .ConfigureAwait(false)
        ).ExitCode;

    internal async Task KillAsync(
        ulong handle,
        ulong timeoutMilliseconds,
        CancellationToken cancellationToken
    )
    {
        await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxKill(cancelId, handle, timeoutMilliseconds, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    internal Task RequestKillAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeHandleAsync(_sandboxRequestKill, handle, cancellationToken);

    internal Task DrainAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeHandleAsync(_sandboxDrain, handle, cancellationToken);

    internal Task RequestDrainAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeHandleAsync(_sandboxRequestDrain, handle, cancellationToken);

    internal async Task<int?> WaitAsync(ulong handle, CancellationToken cancellationToken)
    {
        var json = await InvokeHandleAsync(_sandboxWait, handle, cancellationToken)
            .ConfigureAwait(false);
        return Deserialize<WaitResponse>(json).ExitCode;
    }

    internal async Task<SandboxStopResult> WaitUntilStoppedAsync(
        ulong handle,
        CancellationToken cancellationToken
    ) =>
        ParseStopResult(
            await InvokeHandleAsync(_sandboxWaitUntilStopped, handle, cancellationToken)
                .ConfigureAwait(false)
        );

    internal async Task<SandboxPingResult> PingAsync(
        ulong handle,
        CancellationToken cancellationToken
    ) =>
        ParsePingResult(
            await InvokeHandleAsync(_sandboxPing, handle, cancellationToken).ConfigureAwait(false)
        );

    internal async Task<SandboxTouchResult> TouchAsync(
        ulong handle,
        CancellationToken cancellationToken
    ) =>
        ParseTouchResult(
            await InvokeHandleAsync(_sandboxTouch, handle, cancellationToken).ConfigureAwait(false)
        );

    internal async Task DetachAsync(ulong handle, CancellationToken cancellationToken)
    {
        await InvokeAsync(
                (cancelId, buffer, length) => _sandboxDetach(cancelId, handle, buffer, length),
                cancellationToken,
                checkInitialCancellation: false
            )
            .ConfigureAwait(false);
    }

    internal bool OwnsLifecycle(ulong handle)
    {
        var json = Invoke((buffer, length) => _sandboxOwnsLifecycle(handle, buffer, length));
        return Deserialize<OwnsLifecycleResponse>(json).Owns;
    }

    internal async Task CloseAsync(ulong handle, CancellationToken cancellationToken)
    {
        await InvokeAsync(
                (cancelId, buffer, length) => _sandboxClose(cancelId, handle, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    internal async Task<string> ModifyAsync(
        ulong handle,
        string optionsJson,
        CancellationToken cancellationToken
    )
    {
        using var nativeOptions = new Utf8String(optionsJson);
        return await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxModify(cancelId, handle, nativeOptions.Pointer, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    internal async Task<int> AttachAsync(
        ulong handle,
        string command,
        string optionsJson,
        CancellationToken cancellationToken
    )
    {
        using var nativeCommand = new Utf8String(command);
        using var nativeOptions = new Utf8String(optionsJson);
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxAttach(
                        cancelId,
                        handle,
                        nativeCommand.Pointer,
                        nativeOptions.Pointer,
                        buffer,
                        length
                    ),
                cancellationToken
            )
            .ConfigureAwait(false);
        return Deserialize<WaitResponse>(json).ExitCode
            ?? throw new InvalidDataException(
                "The native attach response did not include an exit code."
            );
    }

    internal async Task<int> AttachShellAsync(ulong handle, CancellationToken cancellationToken)
    {
        var json = await InvokeHandleAsync(_sandboxAttachShell, handle, cancellationToken)
            .ConfigureAwait(false);
        return Deserialize<WaitResponse>(json).ExitCode
            ?? throw new InvalidDataException(
                "The native shell attach response did not include an exit code."
            );
    }

    internal Task RemovePersistedAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeConsumingHandleAsync(_sandboxRemovePersisted, handle, cancellationToken);

    internal async Task<ulong> SshConnectAsync(
        ulong sandboxHandle,
        string optionsJson,
        CancellationToken cancellationToken
    )
    {
        using var nativeOptions = new Utf8String(optionsJson);
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxSshConnect(
                        cancelId,
                        sandboxHandle,
                        nativeOptions.Pointer,
                        buffer,
                        length
                    ),
                cancellationToken
            )
            .ConfigureAwait(false);
        return await ParseOwnedHandleAsync(json, "client_handle", SshClientCloseAsync)
            .ConfigureAwait(false);
    }

    internal async Task<ulong> SshServerAsync(
        ulong sandboxHandle,
        string optionsJson,
        CancellationToken cancellationToken
    )
    {
        using var nativeOptions = new Utf8String(optionsJson);
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxSshServer(
                        cancelId,
                        sandboxHandle,
                        nativeOptions.Pointer,
                        buffer,
                        length
                    ),
                cancellationToken
            )
            .ConfigureAwait(false);
        return await ParseOwnedHandleAsync(json, "server_handle", SshServerCloseAsync)
            .ConfigureAwait(false);
    }

    internal async Task<SshOutput> SshClientExecAsync(
        ulong clientHandle,
        string command,
        string optionsJson,
        CancellationToken cancellationToken
    )
    {
        using var nativeCommand = new Utf8String(command);
        using var nativeOptions = new Utf8String(optionsJson);
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sshClientExec(
                        cancelId,
                        clientHandle,
                        nativeCommand.Pointer,
                        nativeOptions.Pointer,
                        buffer,
                        length
                    ),
                cancellationToken
            )
            .ConfigureAwait(false);
        return ParseSshOutput(json);
    }

    internal async Task<int> SshClientAttachAsync(
        ulong clientHandle,
        string optionsJson,
        CancellationToken cancellationToken
    )
    {
        using var nativeOptions = new Utf8String(optionsJson);
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sshClientAttach(cancelId, clientHandle, nativeOptions.Pointer, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
        return Deserialize<SshAttachResponse>(json).Status;
    }

    internal async Task<ulong> SshClientSftpAsync(
        ulong clientHandle,
        CancellationToken cancellationToken
    )
    {
        var json = await InvokeHandleAsync(_sshClientSftp, clientHandle, cancellationToken)
            .ConfigureAwait(false);
        return await ParseOwnedHandleAsync(json, "sftp_handle", SftpCloseAsync)
            .ConfigureAwait(false);
    }

    internal Task SshClientCloseAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeConsumingHandleAsync(_sshClientClose, handle, cancellationToken);

    internal Task SshServerCloseAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeConsumingHandleAsync(_sshServerClose, handle, cancellationToken);

    internal Task SshServerServeConnectionAsync(
        ulong handle,
        CancellationToken cancellationToken
    ) => InvokeHandleAsync(_sshServerServeConnection, handle, cancellationToken);

    internal Task SshServerServeStandardIoAsync(
        ulong handle,
        CancellationToken cancellationToken
    ) => InvokeHandleAsync(_sshServerServeStandardIo, handle, cancellationToken);

    internal async Task<byte[]> SftpReadAsync(
        ulong handle,
        string path,
        CancellationToken cancellationToken
    )
    {
        var json = await InvokeHandleStringAsync(_sftpRead, handle, path, cancellationToken)
            .ConfigureAwait(false);
        return Convert.FromBase64String(Deserialize<SftpReadResponse>(json).Data);
    }

    internal Task SftpWriteAsync(
        ulong handle,
        string path,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken
    ) =>
        InvokeHandleTwoStringsAsync(
            _sftpWrite,
            handle,
            path,
            Convert.ToBase64String(data.Span),
            cancellationToken
        );

    internal Task SftpMkdirAsync(ulong handle, string path, CancellationToken cancellationToken) =>
        InvokeHandleStringAsync(_sftpMkdir, handle, path, cancellationToken);

    internal Task SftpRemoveFileAsync(
        ulong handle,
        string path,
        CancellationToken cancellationToken
    ) => InvokeHandleStringAsync(_sftpRemoveFile, handle, path, cancellationToken);

    internal Task SftpRemoveDirectoryAsync(
        ulong handle,
        string path,
        CancellationToken cancellationToken
    ) => InvokeHandleStringAsync(_sftpRemoveDir, handle, path, cancellationToken);

    internal Task SftpRenameAsync(
        ulong handle,
        string oldPath,
        string newPath,
        CancellationToken cancellationToken
    ) => InvokeHandleTwoStringsAsync(_sftpRename, handle, oldPath, newPath, cancellationToken);

    internal async Task<string> SftpRealPathAsync(
        ulong handle,
        string path,
        CancellationToken cancellationToken
    ) =>
        Deserialize<SftpPathResponse>(
            await InvokeHandleStringAsync(_sftpRealPath, handle, path, cancellationToken)
                .ConfigureAwait(false)
        ).Path;

    internal async Task<string> SftpReadLinkAsync(
        ulong handle,
        string path,
        CancellationToken cancellationToken
    ) =>
        Deserialize<SftpTargetResponse>(
            await InvokeHandleStringAsync(_sftpReadLink, handle, path, cancellationToken)
                .ConfigureAwait(false)
        ).Target;

    internal Task SftpSymlinkAsync(
        ulong handle,
        string target,
        string linkPath,
        CancellationToken cancellationToken
    ) => InvokeHandleTwoStringsAsync(_sftpSymlink, handle, target, linkPath, cancellationToken);

    internal Task SftpCloseAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeConsumingHandleAsync(_sftpClose, handle, cancellationToken);

    internal async Task<byte[]> FsReadAsync(
        ulong handle,
        string path,
        CancellationToken cancellationToken
    )
    {
        var json = await InvokeHandleStringAsync(_fsRead, handle, path, cancellationToken)
            .ConfigureAwait(false);
        return Convert.FromBase64String(Deserialize<FsReadResponse>(json).Data);
    }

    internal Task FsWriteAsync(
        ulong handle,
        string path,
        byte[] data,
        CancellationToken cancellationToken
    ) =>
        InvokeHandleTwoStringsAsync(
            _fsWrite,
            handle,
            path,
            Convert.ToBase64String(data),
            cancellationToken
        );

    internal async Task<IReadOnlyList<FilesystemEntry>> FsListAsync(
        ulong handle,
        string path,
        CancellationToken cancellationToken
    ) =>
        Deserialize<List<FilesystemEntry>>(
            await InvokeHandleStringAsync(_fsList, handle, path, cancellationToken)
                .ConfigureAwait(false)
        );

    internal async Task<FilesystemStat> FsStatAsync(
        ulong handle,
        string path,
        CancellationToken cancellationToken
    ) =>
        Deserialize<FilesystemStat>(
            await InvokeHandleStringAsync(_fsStat, handle, path, cancellationToken)
                .ConfigureAwait(false)
        );

    internal Task FsCopyFromHostAsync(
        ulong handle,
        string hostPath,
        string guestPath,
        CancellationToken cancellationToken
    ) =>
        InvokeHandleTwoStringsAsync(
            _fsCopyFromHost,
            handle,
            hostPath,
            guestPath,
            cancellationToken
        );

    internal Task FsCopyToHostAsync(
        ulong handle,
        string guestPath,
        string hostPath,
        CancellationToken cancellationToken
    ) => InvokeHandleTwoStringsAsync(_fsCopyToHost, handle, guestPath, hostPath, cancellationToken);

    internal Task FsMkdirAsync(ulong handle, string path, CancellationToken cancellationToken) =>
        InvokeHandleStringAsync(_fsMkdir, handle, path, cancellationToken);

    internal Task FsRemoveAsync(ulong handle, string path, CancellationToken cancellationToken) =>
        InvokeHandleStringAsync(_fsRemove, handle, path, cancellationToken);

    internal Task FsRemoveDirAsync(
        ulong handle,
        string path,
        CancellationToken cancellationToken
    ) => InvokeHandleStringAsync(_fsRemoveDir, handle, path, cancellationToken);

    internal Task FsCopyAsync(
        ulong handle,
        string source,
        string destination,
        CancellationToken cancellationToken
    ) => InvokeHandleTwoStringsAsync(_fsCopy, handle, source, destination, cancellationToken);

    internal Task FsRenameAsync(
        ulong handle,
        string source,
        string destination,
        CancellationToken cancellationToken
    ) => InvokeHandleTwoStringsAsync(_fsRename, handle, source, destination, cancellationToken);

    internal async Task<bool> FsExistsAsync(
        ulong handle,
        string path,
        CancellationToken cancellationToken
    ) =>
        Deserialize<ExistsResponse>(
            await InvokeHandleStringAsync(_fsExists, handle, path, cancellationToken)
                .ConfigureAwait(false)
        ).Exists;

    internal async Task<ulong> FsReadStreamAsync(
        ulong handle,
        string path,
        CancellationToken cancellationToken
    )
    {
        var json = await InvokeHandleStringAsync(_fsReadStream, handle, path, cancellationToken)
            .ConfigureAwait(false);
        return await ParseOwnedHandleAsync(json, "stream_handle", FsReadStreamCloseAsync)
            .ConfigureAwait(false);
    }

    internal async Task<byte[]?> FsReadStreamReceiveAsync(
        ulong handle,
        CancellationToken cancellationToken
    )
    {
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _fsReadStreamReceive(cancelId, handle, buffer, length),
                cancellationToken,
                bufferSize: FilesystemStreamBufferSize
            )
            .ConfigureAwait(false);
        var response = Deserialize<FsReadStreamResponse>(json);
        return response.Done
            ? null
            : Convert.FromBase64String(response.ChunkBase64 ?? string.Empty);
    }

    internal Task FsReadStreamCloseAsync(ulong handle, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Invoke((buffer, length) => _fsReadStreamClose(handle, buffer, length));
        return Task.CompletedTask;
    }

    internal async Task<ulong> FsWriteStreamAsync(
        ulong handle,
        string path,
        CancellationToken cancellationToken
    )
    {
        var json = await InvokeHandleStringAsync(_fsWriteStream, handle, path, cancellationToken)
            .ConfigureAwait(false);
        return await ParseOwnedHandleAsync(json, "stream_handle", FsWriteStreamCloseAsync)
            .ConfigureAwait(false);
    }

    internal async Task FsWriteStreamWriteAsync(
        ulong handle,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken
    )
    {
        using var encoded = new Utf8String(Convert.ToBase64String(data.Span));
        await InvokeAsync(
                (cancelId, buffer, length) =>
                    _fsWriteStreamWrite(cancelId, handle, encoded.Pointer, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    internal Task FsWriteStreamCloseAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeAsync(
            (cancelId, buffer, length) => _fsWriteStreamClose(cancelId, handle, buffer, length),
            cancellationToken,
            checkInitialCancellation: false
        );

    internal async Task<IReadOnlyList<LogEntry>> LogsAsync(
        ulong handle,
        string optionsJson,
        CancellationToken cancellationToken
    )
    {
        using var options = new Utf8String(optionsJson);
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxLogs(cancelId, handle, options.Pointer, buffer, length),
                cancellationToken,
                bufferSize: LogsBufferSize
            )
            .ConfigureAwait(false);
        return ParseLogEntries(json);
    }

    internal async Task<IReadOnlyList<LogEntry>> LogsByNameAsync(
        string name,
        string optionsJson,
        CancellationToken cancellationToken
    )
    {
        using var nativeName = new Utf8String(name);
        using var options = new Utf8String(optionsJson);
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxHandleLogs(
                        cancelId,
                        nativeName.Pointer,
                        options.Pointer,
                        buffer,
                        length
                    ),
                cancellationToken,
                bufferSize: LogsBufferSize
            )
            .ConfigureAwait(false);
        return ParseLogEntries(json);
    }

    internal async Task<ulong> LogStreamAsync(
        ulong handle,
        string optionsJson,
        CancellationToken cancellationToken
    )
    {
        using var options = new Utf8String(optionsJson);
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxLogStream(cancelId, handle, options.Pointer, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
        return await ParseOwnedHandleAsync(json, "stream_handle", LogCloseAsync)
            .ConfigureAwait(false);
    }

    internal async Task<ulong> LogStreamByNameAsync(
        string name,
        string optionsJson,
        CancellationToken cancellationToken
    )
    {
        using var nativeName = new Utf8String(name);
        using var options = new Utf8String(optionsJson);
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxHandleLogStream(
                        cancelId,
                        nativeName.Pointer,
                        options.Pointer,
                        buffer,
                        length
                    ),
                cancellationToken
            )
            .ConfigureAwait(false);
        return await ParseOwnedHandleAsync(json, "stream_handle", LogCloseAsync)
            .ConfigureAwait(false);
    }

    internal async Task<LogEntry?> LogReceiveAsync(
        ulong handle,
        CancellationToken cancellationToken
    )
    {
        var json = await InvokeAsync(
                (cancelId, buffer, length) => _logReceive(cancelId, handle, buffer, length),
                cancellationToken,
                bufferSize: LogsBufferSize
            )
            .ConfigureAwait(false);
        return ParseLogEntry(json);
    }

    internal Task LogCloseAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeSynchronousCloseAsync(_logClose, handle, cancellationToken);

    internal async Task<SandboxMetrics> MetricsAsync(
        ulong handle,
        CancellationToken cancellationToken
    ) =>
        ParseMetrics(
            await InvokeHandleAsync(_sandboxMetrics, handle, cancellationToken)
                .ConfigureAwait(false)
        );

    internal async Task<ulong> MetricsStreamAsync(
        ulong handle,
        ulong intervalMilliseconds,
        CancellationToken cancellationToken
    )
    {
        var json = await InvokeAsync(
                (cancelId, buffer, length) =>
                    _sandboxMetricsStream(cancelId, handle, intervalMilliseconds, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
        return await ParseOwnedHandleAsync(json, "stream_handle", MetricsCloseAsync)
            .ConfigureAwait(false);
    }

    internal async Task<SandboxMetrics?> MetricsReceiveAsync(
        ulong handle,
        CancellationToken cancellationToken
    )
    {
        var json = await InvokeAsync(
                (cancelId, buffer, length) => _metricsReceive(cancelId, handle, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
        return Deserialize<DoneResponse>(json).Done ? null : ParseMetrics(json);
    }

    internal Task MetricsCloseAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeSynchronousCloseAsync(_metricsClose, handle, cancellationToken);

    internal async Task<IReadOnlyDictionary<string, SandboxMetrics>> AllMetricsAsync(
        CancellationToken cancellationToken
    )
    {
        var json = await InvokeAsync(
                (cancelId, buffer, length) => _allSandboxMetrics(cancelId, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
        return Deserialize<AllMetricsResponse>(json).Sandboxes;
    }

    internal async Task<SandboxMetrics> MetricsByNameAsync(
        string name,
        CancellationToken cancellationToken
    ) =>
        ParseMetrics(
            await InvokeByNameWithResultAsync(_sandboxHandleMetrics, name, cancellationToken)
                .ConfigureAwait(false)
        );

    internal Task<VolumeInfo> CreateVolumeAsync(
        string name,
        string optionsJson,
        CancellationToken cancellationToken
    ) =>
        InvokeTwoStringsAndParseAsync<VolumeInfo>(
            _volumeCreate,
            name,
            optionsJson,
            cancellationToken
        );

    internal Task<VolumeInfo> GetVolumeAsync(string name, CancellationToken cancellationToken) =>
        InvokeStringAndParseAsync<VolumeInfo>(_volumeGet, name, cancellationToken);

    internal async Task<IReadOnlyList<VolumeInfo>> ListVolumesAsync(
        CancellationToken cancellationToken
    ) =>
        Deserialize<List<VolumeInfo>>(
            await InvokeNoArgAsync(_volumeList, cancellationToken).ConfigureAwait(false)
        );

    internal Task RemoveVolumeAsync(string name, CancellationToken cancellationToken) =>
        InvokeStringAsync(_volumeRemove, name, cancellationToken);

    internal Task<ImageInfo> GetImageAsync(string reference, CancellationToken cancellationToken) =>
        InvokeStringAndParseAsync<ImageInfo>(_imageGet, reference, cancellationToken);

    internal async Task<IReadOnlyList<ImageInfo>> ListImagesAsync(
        CancellationToken cancellationToken
    ) =>
        Deserialize<List<ImageInfo>>(
            await InvokeNoArgAsync(_imageList, cancellationToken).ConfigureAwait(false)
        );

    internal Task<ImageDetail> InspectImageAsync(
        string reference,
        CancellationToken cancellationToken
    ) => InvokeStringAndParseAsync<ImageDetail>(_imageInspect, reference, cancellationToken);

    internal Task RemoveImageAsync(
        string reference,
        bool force,
        CancellationToken cancellationToken
    ) => InvokeStringBoolAsync(_imageRemove, reference, force, cancellationToken);

    internal async Task<ImagePruneReport> PruneImagesAsync(CancellationToken cancellationToken) =>
        Deserialize<ImagePruneReport>(
            await InvokeNoArgAsync(_imagePrune, cancellationToken).ConfigureAwait(false)
        );

    internal async Task<IReadOnlyList<ImageInfo>> LoadImagesAsync(
        string inputPath,
        string tagsJson,
        CancellationToken cancellationToken
    ) =>
        Deserialize<List<ImageInfo>>(
            await InvokeTwoStringsAsync(_imageLoad, inputPath, tagsJson, cancellationToken)
                .ConfigureAwait(false)
        );

    internal Task SaveImagesAsync(
        string referencesJson,
        string outputPath,
        string format,
        CancellationToken cancellationToken
    ) => InvokeThreeStringsAsync(_imageSave, referencesJson, outputPath, format, cancellationToken);

    internal Task<SnapshotArtifact> CreateSnapshotAsync(
        string sourceSandbox,
        string optionsJson,
        CancellationToken cancellationToken
    ) =>
        InvokeTwoStringsAndParseAsync<SnapshotArtifact>(
            _snapshotCreate,
            sourceSandbox,
            optionsJson,
            cancellationToken
        );

    internal Task<SnapshotArtifact> OpenSnapshotAsync(
        string pathOrName,
        CancellationToken cancellationToken
    ) => InvokeStringAndParseAsync<SnapshotArtifact>(_snapshotOpen, pathOrName, cancellationToken);

    internal Task<SnapshotVerifyReport> VerifySnapshotAsync(
        string pathOrName,
        CancellationToken cancellationToken
    ) =>
        InvokeStringAndParseAsync<SnapshotVerifyReport>(
            _snapshotVerify,
            pathOrName,
            cancellationToken
        );

    internal Task<SnapshotInfo> GetSnapshotAsync(
        string nameOrDigest,
        CancellationToken cancellationToken
    ) => InvokeStringAndParseAsync<SnapshotInfo>(_snapshotGet, nameOrDigest, cancellationToken);

    internal async Task<IReadOnlyList<SnapshotInfo>> ListSnapshotsAsync(
        CancellationToken cancellationToken
    ) =>
        Deserialize<List<SnapshotInfo>>(
            await InvokeNoArgAsync(_snapshotList, cancellationToken).ConfigureAwait(false)
        );

    internal async Task<IReadOnlyList<SnapshotArtifact>> ListSnapshotDirectoryAsync(
        string directory,
        CancellationToken cancellationToken
    ) =>
        Deserialize<List<SnapshotArtifact>>(
            await InvokeStringAsync(_snapshotListDir, directory, cancellationToken)
                .ConfigureAwait(false)
        );

    internal Task RemoveSnapshotAsync(
        string pathOrName,
        bool force,
        CancellationToken cancellationToken
    ) => InvokeStringBoolAsync(_snapshotRemove, pathOrName, force, cancellationToken);

    internal async Task<uint> ReindexSnapshotsAsync(
        string directory,
        CancellationToken cancellationToken
    ) =>
        Deserialize<ReindexResponse>(
            await InvokeStringAsync(_snapshotReindex, directory, cancellationToken)
                .ConfigureAwait(false)
        ).Indexed;

    internal Task ExportSnapshotAsync(
        string nameOrPath,
        string outputPath,
        string optionsJson,
        CancellationToken cancellationToken
    ) =>
        InvokeThreeStringsAsync(
            _snapshotExport,
            nameOrPath,
            outputPath,
            optionsJson,
            cancellationToken
        );

    internal Task<SnapshotInfo> ImportSnapshotAsync(
        string archive,
        string destination,
        CancellationToken cancellationToken
    ) =>
        InvokeTwoStringsAndParseAsync<SnapshotInfo>(
            _snapshotImport,
            archive,
            destination,
            cancellationToken
        );

    internal Task<SnapshotArtifact> SnapshotByNameAsync(
        string sandboxName,
        string snapshotName,
        CancellationToken cancellationToken
    ) =>
        InvokeTwoStringsAndParseAsync<SnapshotArtifact>(
            _sandboxHandleSnapshot,
            sandboxName,
            snapshotName,
            cancellationToken
        );

    internal string AgentSocketPath(string name)
    {
        using var nativeName = new Utf8String(name);
        var json = Invoke((buffer, length) => _agentSocketPath(nativeName.Pointer, buffer, length));
        return Deserialize<AgentSocketPathResponse>(json).Path;
    }

    internal Task<ulong> OpenAgentSandboxAsync(string name, CancellationToken cancellationToken) =>
        OpenAgentAsync(_agentOpenSandbox, name, cancellationToken);

    internal Task<ulong> OpenAgentPathAsync(string path, CancellationToken cancellationToken) =>
        OpenAgentAsync(_agentOpenPath, path, cancellationToken);

    internal async Task<RawFrame> AgentRequestAsync(
        ulong handle,
        byte flags,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken
    )
    {
        var input = body.ToArray();
        var pinned = input.Length == 0 ? default : GCHandle.Alloc(input, GCHandleType.Pinned);
        nint output = 0;
        nuint outputLength = 0;
        uint id = 0;
        byte outputFlags = 0;
        try
        {
            await InvokeRawAsync(
                    cancelId =>
                        _agentRequest(
                            cancelId,
                            handle,
                            flags,
                            input.Length == 0 ? 0 : pinned.AddrOfPinnedObject(),
                            checked((nuint)input.Length),
                            out id,
                            out outputFlags,
                            out output,
                            out outputLength
                        ),
                    cancellationToken
                )
                .ConfigureAwait(false);
            return new RawFrame(id, outputFlags, CopyRustBytes(output, outputLength));
        }
        finally
        {
            FreeRustBytes(ref output, ref outputLength);
            if (pinned.IsAllocated)
            {
                pinned.Free();
            }
        }
    }

    internal async Task<(uint Id, ulong StreamHandle)> AgentStreamOpenAsync(
        ulong handle,
        byte flags,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken
    )
    {
        var input = body.ToArray();
        var pinned = input.Length == 0 ? default : GCHandle.Alloc(input, GCHandleType.Pinned);
        uint id = 0;
        ulong streamHandle = 0;
        try
        {
            await InvokeRawAsync(
                    cancelId =>
                        _agentStreamOpen(
                            cancelId,
                            handle,
                            flags,
                            input.Length == 0 ? 0 : pinned.AddrOfPinnedObject(),
                            checked((nuint)input.Length),
                            out id,
                            out streamHandle
                        ),
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (streamHandle == 0)
            {
                throw new InvalidDataException(
                    "The native ABI returned an invalid agent stream handle."
                );
            }

            return (id, streamHandle);
        }
        finally
        {
            if (pinned.IsAllocated)
            {
                pinned.Free();
            }
        }
    }

    internal async Task<RawFrame?> AgentStreamNextAsync(
        ulong agentHandle,
        ulong streamHandle,
        CancellationToken cancellationToken
    )
    {
        nint output = 0;
        nuint outputLength = 0;
        bool present = false;
        uint id = 0;
        byte flags = 0;
        try
        {
            await InvokeRawAsync(
                    cancelId =>
                        _agentStreamNext(
                            cancelId,
                            agentHandle,
                            streamHandle,
                            out present,
                            out id,
                            out flags,
                            out output,
                            out outputLength
                        ),
                    cancellationToken
                )
                .ConfigureAwait(false);
            return present ? new RawFrame(id, flags, CopyRustBytes(output, outputLength)) : null;
        }
        finally
        {
            FreeRustBytes(ref output, ref outputLength);
        }
    }

    internal Task AgentStreamCloseAsync(
        ulong agentHandle,
        ulong streamHandle,
        CancellationToken cancellationToken
    ) =>
        InvokeRawAsync(
            cancelId => _agentStreamClose(cancelId, agentHandle, streamHandle),
            cancellationToken,
            checkInitialCancellation: false
        );

    internal async Task AgentSendAsync(
        ulong handle,
        uint id,
        byte flags,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken
    )
    {
        var input = body.ToArray();
        var pinned = input.Length == 0 ? default : GCHandle.Alloc(input, GCHandleType.Pinned);
        try
        {
            await InvokeRawAsync(
                    cancelId =>
                        _agentSend(
                            cancelId,
                            handle,
                            id,
                            flags,
                            input.Length == 0 ? 0 : pinned.AddrOfPinnedObject(),
                            checked((nuint)input.Length)
                        ),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        finally
        {
            if (pinned.IsAllocated)
            {
                pinned.Free();
            }
        }
    }

    internal byte[] AgentReadyBytes(ulong handle)
    {
        nint output = 0;
        nuint outputLength = 0;
        try
        {
            ThrowIfError(_agentReadyBytes(handle, out output, out outputLength));
            return CopyRustBytes(output, outputLength);
        }
        finally
        {
            FreeRustBytes(ref output, ref outputLength);
        }
    }

    internal Task AgentCloseAsync(ulong handle, CancellationToken cancellationToken) =>
        InvokeRawAsync(
            cancelId => _agentClose(cancelId, handle),
            cancellationToken,
            checkInitialCancellation: false
        );

    private async Task<T> InvokeStringAndParseAsync<T>(
        StringFn call,
        string value,
        CancellationToken cancellationToken
    ) =>
        Deserialize<T>(
            await InvokeStringAsync(call, value, cancellationToken).ConfigureAwait(false)
        );

    private async Task<ulong> OpenAgentAsync(
        AgentOpenFn call,
        string value,
        CancellationToken cancellationToken
    )
    {
        using var nativeValue = new Utf8String(value);
        ulong handle = 0;
        await InvokeRawAsync(
                cancelId => call(cancelId, nativeValue.Pointer, 0, out handle),
                cancellationToken
            )
            .ConfigureAwait(false);
        return handle != 0
            ? handle
            : throw new InvalidDataException("The native ABI returned an invalid agent handle.");
    }

    private async Task<T> InvokeTwoStringsAndParseAsync<T>(
        TwoStringsFn call,
        string first,
        string second,
        CancellationToken cancellationToken
    ) =>
        Deserialize<T>(
            await InvokeTwoStringsAsync(call, first, second, cancellationToken)
                .ConfigureAwait(false)
        );

    private Task<string> InvokeNoArgAsync(
        CancellableNoArgFn call,
        CancellationToken cancellationToken
    ) =>
        InvokeAsync(
            (cancelId, buffer, length) => call(cancelId, buffer, length),
            cancellationToken
        );

    private Task<string> InvokeExecAsync(
        ExecHandleFn call,
        ulong handle,
        CancellationToken cancellationToken
    ) =>
        InvokeAsync(
            (cancelId, buffer, length) => call(cancelId, handle, buffer, length),
            cancellationToken
        );

    private Task InvokeSynchronousCloseAsync(
        StreamCloseFn call,
        ulong handle,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        Invoke((buffer, length) => call(handle, buffer, length));
        return Task.CompletedTask;
    }

    private Task<string> InvokeConsumingHandleAsync(
        SandboxHandleFn call,
        ulong handle,
        CancellationToken cancellationToken
    ) =>
        InvokeAsync(
            (cancelId, buffer, length) => call(cancelId, handle, buffer, length),
            cancellationToken,
            checkInitialCancellation: false
        );

    private async Task<string> InvokeStringAsync(
        StringFn call,
        string value,
        CancellationToken cancellationToken
    )
    {
        using var nativeValue = new Utf8String(value);
        return await InvokeAsync(
                (cancelId, buffer, length) => call(cancelId, nativeValue.Pointer, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private async Task<string> InvokeTwoStringsAsync(
        TwoStringsFn call,
        string first,
        string second,
        CancellationToken cancellationToken
    )
    {
        using var nativeFirst = new Utf8String(first);
        using var nativeSecond = new Utf8String(second);
        return await InvokeAsync(
                (cancelId, buffer, length) =>
                    call(cancelId, nativeFirst.Pointer, nativeSecond.Pointer, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private async Task<string> InvokeThreeStringsAsync(
        ThreeStringsFn call,
        string first,
        string second,
        string third,
        CancellationToken cancellationToken
    )
    {
        using var nativeFirst = new Utf8String(first);
        using var nativeSecond = new Utf8String(second);
        using var nativeThird = new Utf8String(third);
        return await InvokeAsync(
                (cancelId, buffer, length) =>
                    call(
                        cancelId,
                        nativeFirst.Pointer,
                        nativeSecond.Pointer,
                        nativeThird.Pointer,
                        buffer,
                        length
                    ),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private async Task<string> InvokeStringBoolAsync(
        StringBoolFn call,
        string value,
        bool flag,
        CancellationToken cancellationToken
    )
    {
        using var nativeValue = new Utf8String(value);
        return await InvokeAsync(
                (cancelId, buffer, length) =>
                    call(cancelId, nativeValue.Pointer, flag, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private async Task<string> InvokeHandleStringAsync(
        SandboxHandleStringFn call,
        ulong handle,
        string value,
        CancellationToken cancellationToken
    )
    {
        using var nativeValue = new Utf8String(value);
        return await InvokeAsync(
                (cancelId, buffer, length) =>
                    call(cancelId, handle, nativeValue.Pointer, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private async Task<string> InvokeHandleTwoStringsAsync(
        SandboxHandleTwoStringsFn call,
        ulong handle,
        string first,
        string second,
        CancellationToken cancellationToken
    )
    {
        using var nativeFirst = new Utf8String(first);
        using var nativeSecond = new Utf8String(second);
        return await InvokeAsync(
                (cancelId, buffer, length) =>
                    call(
                        cancelId,
                        handle,
                        nativeFirst.Pointer,
                        nativeSecond.Pointer,
                        buffer,
                        length
                    ),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private async Task<ulong> AcquireHandleAsync(
        CancellableNativeCall call,
        CancellationToken cancellationToken
    )
    {
        var json = await InvokeAsync(call, cancellationToken).ConfigureAwait(false);
        try
        {
            return Deserialize<CreateResponse>(json).Handle;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            var handle = SalvageHandle(json);
            if (handle != 0)
            {
                try
                {
                    await CloseAsync(handle, CancellationToken.None).ConfigureAwait(false);
                }
                catch (MicrosandboxException)
                {
                    // Preserve the parsing failure that made ownership unsafe.
                }
            }

            throw;
        }
    }

    private static async Task<ulong> ParseOwnedHandleAsync(
        string json,
        string propertyName,
        Func<ulong, CancellationToken, Task> closeAsync
    )
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var handle = document.RootElement.GetProperty(propertyName).GetUInt64();
            return handle != 0
                ? handle
                : throw new InvalidDataException(
                    $"The native ABI returned an invalid {propertyName}."
                );
        }
        catch (Exception exception)
            when (exception
                    is JsonException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or InvalidDataException
            )
        {
            var handle = SalvageHandle(json, propertyName);
            if (handle != 0)
            {
                try
                {
                    await closeAsync(handle, CancellationToken.None).ConfigureAwait(false);
                }
                catch (MicrosandboxException)
                {
                    // Preserve the response parsing failure that made ownership unsafe.
                }
            }

            throw;
        }
    }

    private async Task InvokeByNameAsync(
        SandboxByNameFn call,
        string name,
        CancellationToken cancellationToken
    ) => _ = await InvokeByNameWithResultAsync(call, name, cancellationToken).ConfigureAwait(false);

    private async Task<string> InvokeByNameWithResultAsync(
        SandboxByNameFn call,
        string name,
        CancellationToken cancellationToken
    )
    {
        using var nativeName = new Utf8String(name);
        return await InvokeAsync(
                (cancelId, buffer, length) => call(cancelId, nativeName.Pointer, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private async Task InvokeByNameAsync(
        SandboxByNameTimeoutFn call,
        string name,
        ulong timeoutMilliseconds,
        CancellationToken cancellationToken
    )
    {
        using var nativeName = new Utf8String(name);
        await InvokeAsync(
                (cancelId, buffer, length) =>
                    call(cancelId, nativeName.Pointer, timeoutMilliseconds, buffer, length),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private Task<string> InvokeHandleAsync(
        SandboxHandleFn call,
        ulong handle,
        CancellationToken cancellationToken
    ) =>
        InvokeAsync(
            (cancelId, buffer, length) => call(cancelId, handle, buffer, length),
            cancellationToken
        );

    private string Invoke(NativeCall call)
    {
        var buffer = new byte[BufferSize];
        var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            ThrowIfError(call(pinned.AddrOfPinnedObject(), (nuint)buffer.Length));
            return ReadBuffer(buffer);
        }
        finally
        {
            pinned.Free();
        }
    }

    private Task<string> InvokeAsync(
        CancellableNativeCall call,
        CancellationToken cancellationToken,
        bool checkInitialCancellation = true,
        int bufferSize = BufferSize
    ) =>
        Task.Run(() =>
        {
            if (checkInitialCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            var buffer = new byte[bufferSize];
            var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var cancelId = _cancelAlloc();
            using var registration = cancellationToken.Register(() => _cancelTrigger(cancelId));
            try
            {
                var errorPointer = call(
                    cancelId,
                    pinned.AddrOfPinnedObject(),
                    (nuint)buffer.Length
                );
                if (errorPointer != 0 && cancellationToken.IsCancellationRequested)
                {
                    _freeString(errorPointer);
                    throw new OperationCanceledException(cancellationToken);
                }

                ThrowIfError(errorPointer);
                return ReadBuffer(buffer);
            }
            finally
            {
                _cancelUnregister(cancelId);
                pinned.Free();
            }
        });

    private Task InvokeRawAsync(
        RawNativeCall call,
        CancellationToken cancellationToken,
        bool checkInitialCancellation = true
    ) =>
        Task.Run(() =>
        {
            if (checkInitialCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var cancelId = _cancelAlloc();
            using var registration = cancellationToken.Register(() => _cancelTrigger(cancelId));
            try
            {
                var errorPointer = call(cancelId);
                if (errorPointer != 0 && cancellationToken.IsCancellationRequested)
                {
                    _freeString(errorPointer);
                    throw new OperationCanceledException(cancellationToken);
                }

                ThrowIfError(errorPointer);
            }
            finally
            {
                _cancelUnregister(cancelId);
            }
        });

    private static byte[] CopyRustBytes(nint pointer, nuint length)
    {
        if (length == 0)
        {
            return [];
        }

        if (pointer == 0)
        {
            throw new InvalidDataException(
                "The native ABI returned a null byte pointer with a non-zero length."
            );
        }

        var result = new byte[checked((int)length)];
        Marshal.Copy(pointer, result, 0, result.Length);
        return result;
    }

    private void FreeRustBytes(ref nint pointer, ref nuint length)
    {
        if (pointer != 0)
        {
            _agentFreeBytes(pointer, length);
            pointer = 0;
            length = 0;
        }
    }

    private void ThrowIfError(nint errorPointer)
    {
        if (errorPointer == 0)
        {
            return;
        }

        try
        {
            var json = Marshal.PtrToStringUTF8(errorPointer) ?? "Native microsandbox call failed.";
            var error = JsonSerializer.Deserialize<ErrorResponse>(json, JsonDefaults.Options);
            throw new MicrosandboxException(error?.Kind ?? "internal", error?.Message ?? json);
        }
        finally
        {
            _freeString(errorPointer);
        }
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)
        ?? throw new InvalidDataException(
            $"The native ABI returned an empty {typeof(T).Name} response."
        );

    internal static SandboxMetadata ParseMetadata(string json) =>
        Deserialize<SandboxMetadataResponse>(json).ToMetadata();

    internal static string ParseVersion(string json) => Deserialize<VersionResponse>(json).Version;

    internal static void ValidateVersion(string nativeVersion, string managedVersion)
    {
        if (!string.Equals(nativeVersion, managedVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The microsandbox native library version '{nativeVersion}' does not match managed SDK version '{managedVersion}'."
            );
        }
    }

    internal static SandboxStopResult ParseStopResult(string json)
    {
        var response = Deserialize<StopResponse>(json);
        return new SandboxStopResult(
            response.Name,
            response.Status,
            response.ExitCode,
            response.Signal,
            DateTimeOffset.FromUnixTimeSeconds(response.ObservedAtUnix),
            response.Source
        );
    }

    internal static SandboxPingResult ParsePingResult(string json)
    {
        var response = Deserialize<PingResponse>(json);
        return new SandboxPingResult(
            response.Name,
            TimeSpan.FromMilliseconds(response.LatencyMilliseconds)
        );
    }

    internal static SandboxTouchResult ParseTouchResult(string json)
    {
        var response = Deserialize<TouchResponse>(json);
        return new SandboxTouchResult(response.Name, response.ActivitySequence);
    }

    internal static IReadOnlyList<LogEntry> ParseLogEntries(string json)
    {
        var entries = Deserialize<List<LogEntryWire>>(json);
        return entries
            .Select(entry => new LogEntry(
                entry.Source,
                entry.SessionId,
                DateTimeOffset.FromUnixTimeMilliseconds(entry.TimestampMs),
                Convert.FromBase64String(entry.DataBase64),
                entry.Cursor
            ))
            .ToArray();
    }

    internal static LogEntry? ParseLogEntry(string json)
    {
        if (Deserialize<DoneResponse>(json).Done)
        {
            return null;
        }

        var entry = Deserialize<LogEntryWire>(json);
        return new LogEntry(
            entry.Source,
            entry.SessionId,
            DateTimeOffset.FromUnixTimeMilliseconds(entry.TimestampMs),
            Convert.FromBase64String(entry.DataBase64),
            entry.Cursor
        );
    }

    internal static ExecEvent ParseExecEvent(string json)
    {
        var response = Deserialize<ExecEventResponse>(json);
        return response.Event switch
        {
            "started" => new ExecStartedEvent(response.Pid),
            "stdout" => new ExecStandardOutputEvent(
                Convert.FromBase64String(response.Data ?? string.Empty)
            ),
            "stderr" => new ExecStandardErrorEvent(
                Convert.FromBase64String(response.Data ?? string.Empty)
            ),
            "exited" => new ExecExitedEvent(response.Code),
            "failed" => new ExecFailedEvent(ParseExecFailure(response.Error)),
            "stdin_error" => new ExecStdinErrorEvent(ParseExecFailure(response.Error)),
            "done" => ExecDoneEvent.Instance,
            _ => throw new InvalidDataException(
                $"The native ABI returned an unknown exec event '{response.Event}'."
            ),
        };
    }

    internal static ExecResult ParseExecCollect(string json)
    {
        var response = Deserialize<ExecCollectResponse>(json);
        return new ExecResult(
            Encoding.UTF8.GetString(Convert.FromBase64String(response.StandardOutputBase64)),
            Encoding.UTF8.GetString(Convert.FromBase64String(response.StandardErrorBase64)),
            response.ExitCode
        );
    }

    internal static SshOutput ParseSshOutput(string json)
    {
        var response = Deserialize<SshOutputResponse>(json);
        return new SshOutput(
            response.Status,
            Convert.FromBase64String(response.StandardOutputBase64),
            Convert.FromBase64String(response.StandardErrorBase64)
        );
    }

    private static ExecFailure ParseExecFailure(JsonElement? error)
    {
        if (error is not { } value)
        {
            return new ExecFailure(Message: string.Empty);
        }

        try
        {
            return value.Deserialize<ExecFailure>(JsonDefaults.Options)
                ?? new ExecFailure(Message: value.GetRawText());
        }
        catch (JsonException)
        {
            return new ExecFailure(Message: value.GetRawText());
        }
    }

    internal static SandboxMetrics ParseMetrics(string json) => Deserialize<SandboxMetrics>(json);

    internal static IReadOnlyList<FilesystemEntry> ParseFilesystemEntries(string json) =>
        Deserialize<List<FilesystemEntry>>(json);

    internal static FilesystemStat ParseFilesystemStat(string json) =>
        Deserialize<FilesystemStat>(json);

    internal static ImageDetail ParseImageDetail(string json) => Deserialize<ImageDetail>(json);

    internal static VolumeInfo ParseVolumeInfo(string json) => Deserialize<VolumeInfo>(json);

    internal static SnapshotArtifact ParseSnapshotArtifact(string json) =>
        Deserialize<SnapshotArtifact>(json);

    internal static SnapshotInfo ParseSnapshotInfo(string json) => Deserialize<SnapshotInfo>(json);

    private static string ReadBuffer(byte[] buffer)
    {
        var length = Array.IndexOf(buffer, (byte)0);
        return Encoding.UTF8.GetString(buffer, 0, length < 0 ? buffer.Length : length);
    }

    internal static ulong SalvageHandle(string json) => SalvageHandle(json, "handle");

    internal static ulong SalvageHandle(string json, string propertyName)
    {
        var match = Regex.Match(
            json,
            $"\\\"{Regex.Escape(propertyName)}\\\"\\s*:\\s*(?<handle>[0-9]+)",
            RegexOptions.CultureInvariant
        );
        return match.Success && ulong.TryParse(match.Groups["handle"].Value, out var handle)
            ? handle
            : 0;
    }

    private static T GetExport<T>(nint library, string name)
        where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

    private static string GetManagedVersion()
    {
        var version =
            typeof(NativeApi)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? throw new InvalidOperationException(
                "The Microsandbox assembly does not declare an informational version."
            );
        return NormalizeManagedVersion(version);
    }

    internal static string NormalizeManagedVersion(string informationalVersion) =>
        informationalVersion.Split('+', 2)[0];

    private static IEnumerable<string> CandidatePaths(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            yield return Path.GetFullPath(explicitPath);
            yield break;
        }

        var environmentPath = Environment.GetEnvironmentVariable("MICROSANDBOX_FFI_LIBRARY");
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            yield return Path.GetFullPath(environmentPath);
        }

        var fileName =
            OperatingSystem.IsWindows() ? "microsandbox_go_ffi.dll"
            : OperatingSystem.IsMacOS() ? "libmicrosandbox_go_ffi.dylib"
            : "libmicrosandbox_go_ffi.so";
        yield return Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            RuntimeInformation.RuntimeIdentifier,
            "native",
            fileName
        );
        yield return Path.Combine(AppContext.BaseDirectory, fileName);
        yield return fileName;
    }

    private sealed record CreateResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("handle")] ulong Handle
    );

    private sealed record ExecResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("stdout")] string Stdout,
        [property: System.Text.Json.Serialization.JsonPropertyName("stderr")] string Stderr,
        [property: System.Text.Json.Serialization.JsonPropertyName("exit_code")] int? ExitCode
    );

    private sealed record WaitResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("exit_code")] int? ExitCode
    );

    private sealed record IdResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("id")] string Id
    );

    private sealed record ExecWaitResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("exit_code")] int ExitCode
    );

    private sealed record ExecCollectResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("stdout_b64")]
            string StandardOutputBase64,
        [property: System.Text.Json.Serialization.JsonPropertyName("stderr_b64")]
            string StandardErrorBase64,
        [property: System.Text.Json.Serialization.JsonPropertyName("exit_code")] int ExitCode
    );

    private sealed record SshOutputResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("status")] int Status,
        [property: System.Text.Json.Serialization.JsonPropertyName("stdout")]
            string StandardOutputBase64,
        [property: System.Text.Json.Serialization.JsonPropertyName("stderr")]
            string StandardErrorBase64
    );

    private sealed record SshAttachResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("status")] int Status
    );

    private sealed record SftpReadResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("data")] string Data
    );

    private sealed record SftpPathResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("path")] string Path
    );

    private sealed record SftpTargetResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("target")] string Target
    );

    private sealed record AgentSocketPathResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("path")] string Path
    );

    private sealed record ExecEventResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("event")] string Event,
        [property: System.Text.Json.Serialization.JsonPropertyName("pid")] uint Pid,
        [property: System.Text.Json.Serialization.JsonPropertyName("data")] string? Data,
        [property: System.Text.Json.Serialization.JsonPropertyName("code")] int Code,
        [property: System.Text.Json.Serialization.JsonPropertyName("error")] JsonElement? Error
    );

    private sealed record OwnsLifecycleResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("owns")] bool Owns
    );

    private sealed record SandboxMetadataResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("status")] SandboxStatus Status,
        [property: System.Text.Json.Serialization.JsonPropertyName("config_json")]
            string ConfigJson,
        [property: System.Text.Json.Serialization.JsonPropertyName("created_at_unix")]
            long? CreatedAtUnix,
        [property: System.Text.Json.Serialization.JsonPropertyName("updated_at_unix")]
            long? UpdatedAtUnix
    )
    {
        internal SandboxMetadata ToMetadata() =>
            new(
                Name,
                Status,
                ConfigJson,
                CreatedAtUnix is { } created ? DateTimeOffset.FromUnixTimeSeconds(created) : null,
                UpdatedAtUnix is { } updated ? DateTimeOffset.FromUnixTimeSeconds(updated) : null
            );
    }

    private sealed record StopResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("status")] SandboxStatus Status,
        [property: System.Text.Json.Serialization.JsonPropertyName("exit_code")] int? ExitCode,
        [property: System.Text.Json.Serialization.JsonPropertyName("signal")] int? Signal,
        [property: System.Text.Json.Serialization.JsonPropertyName("observed_at_unix")]
            long ObservedAtUnix,
        [property: System.Text.Json.Serialization.JsonPropertyName("source")] string? Source
    );

    private sealed record PingResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("latency_ms")]
            double LatencyMilliseconds
    );

    private sealed record TouchResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("activity_seq")]
            ulong ActivitySequence
    );

    private sealed record ErrorResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("kind")] string Kind,
        [property: System.Text.Json.Serialization.JsonPropertyName("message")] string Message
    );

    private sealed record FsReadResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("data")] string Data
    );

    private sealed record ExistsResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("exists")] bool Exists
    );

    private sealed record FsReadStreamResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("done")] bool Done,
        [property: System.Text.Json.Serialization.JsonPropertyName("chunk_b64")] string? ChunkBase64
    );

    private sealed record DoneResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("done")] bool Done
    );

    private sealed record LogEntryWire(
        [property: System.Text.Json.Serialization.JsonPropertyName("source")] LogSource Source,
        [property: System.Text.Json.Serialization.JsonPropertyName("session_id")] ulong? SessionId,
        [property: System.Text.Json.Serialization.JsonPropertyName("timestamp_ms")]
            long TimestampMs,
        [property: System.Text.Json.Serialization.JsonPropertyName("data_b64")] string DataBase64,
        [property: System.Text.Json.Serialization.JsonPropertyName("cursor")] string Cursor
    );

    private sealed record AllMetricsResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("sandboxes")]
            Dictionary<string, SandboxMetrics> Sandboxes
    );

    private sealed record ReindexResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("indexed")] uint Indexed
    );

    private sealed record VersionResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("version")] string Version
    );

    private delegate nint NativeCall(nint buffer, nuint length);
    private delegate nint CancellableNativeCall(ulong cancelId, nint buffer, nuint length);
    private delegate nint RawNativeCall(ulong cancelId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeStringFn(nint pointer);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetMsbPathFn(nint path);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ulong CancelAllocFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CancelTriggerFn(ulong cancelId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CancelUnregisterFn(ulong cancelId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxCreateFn(
        ulong cancelId,
        nint name,
        nint optionsJson,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxLookupFn(ulong cancelId, nint name, nint buffer, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxConnectFn(ulong cancelId, nint name, nint buffer, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxStartFn(
        ulong cancelId,
        nint name,
        [MarshalAs(UnmanagedType.I1)] bool detached,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxByNameFn(ulong cancelId, nint name, nint buffer, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxByNameTimeoutFn(
        ulong cancelId,
        nint name,
        ulong timeoutMilliseconds,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxListFn(ulong cancelId, nint filterJson, nint buffer, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxExecFn(
        ulong cancelId,
        ulong handle,
        nint command,
        nint optionsJson,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint ExecHandleFn(ulong cancelId, ulong execHandle, nint buffer, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint ExecIdFn(ulong execHandle, nint buffer, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint ExecSignalFn(
        ulong cancelId,
        ulong execHandle,
        int signal,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint ExecResizeFn(
        ulong cancelId,
        ulong execHandle,
        ushort rows,
        ushort columns,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint ExecStdinWriteFn(
        ulong cancelId,
        ulong execHandle,
        nint dataBase64,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxStopFn(
        ulong cancelId,
        ulong handle,
        ulong timeoutMilliseconds,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxHandleFn(ulong cancelId, ulong handle, nint buffer, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxOwnsLifecycleFn(ulong handle, nint buffer, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxCloseFn(ulong cancelId, ulong handle, nint buffer, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxHandleStringFn(
        ulong cancelId,
        ulong handle,
        nint value,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxHandleTwoStringsFn(
        ulong cancelId,
        ulong handle,
        nint first,
        nint second,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxHandleOptionsFn(
        ulong cancelId,
        ulong handle,
        nint optionsJson,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint SandboxByNameOptionsFn(
        ulong cancelId,
        nint name,
        nint optionsJson,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint StreamHandleFn(
        ulong cancelId,
        ulong streamHandle,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint StreamCloseFn(ulong streamHandle, nint buffer, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint StreamWriteFn(
        ulong cancelId,
        ulong streamHandle,
        nint dataBase64,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint MetricsStreamStartFn(
        ulong cancelId,
        ulong sandboxHandle,
        ulong intervalMilliseconds,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint CancellableNoArgFn(ulong cancelId, nint buffer, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint StringFn(ulong cancelId, nint value, nint buffer, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint TwoStringsFn(
        ulong cancelId,
        nint first,
        nint second,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint ThreeStringsFn(
        ulong cancelId,
        nint first,
        nint second,
        nint third,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint StringBoolFn(
        ulong cancelId,
        nint value,
        [MarshalAs(UnmanagedType.I1)] bool flag,
        nint buffer,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint VersionFn(nint buffer, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint AgentSocketPathFn(nint name, nint buffer, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint AgentOpenFn(
        ulong cancelId,
        nint value,
        ulong timeoutMilliseconds,
        out ulong handle
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint AgentRequestFn(
        ulong cancelId,
        ulong agentHandle,
        byte flags,
        nint body,
        nuint bodyLength,
        out uint id,
        out byte outputFlags,
        out nint outputBody,
        out nuint outputBodyLength
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint AgentStreamOpenFn(
        ulong cancelId,
        ulong agentHandle,
        byte flags,
        nint body,
        nuint bodyLength,
        out uint id,
        out ulong streamHandle
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint AgentStreamNextFn(
        ulong cancelId,
        ulong agentHandle,
        ulong streamHandle,
        [MarshalAs(UnmanagedType.I1)] out bool present,
        out uint id,
        out byte flags,
        out nint outputBody,
        out nuint outputBodyLength
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint AgentStreamCloseFn(ulong cancelId, ulong agentHandle, ulong streamHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint AgentSendFn(
        ulong cancelId,
        ulong agentHandle,
        uint id,
        byte flags,
        nint body,
        nuint bodyLength
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint AgentReadyBytesFn(
        ulong agentHandle,
        out nint outputBody,
        out nuint outputBodyLength
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint AgentCloseFn(ulong cancelId, ulong agentHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void AgentFreeBytesFn(nint pointer, nuint length);

    private sealed class Utf8String : IDisposable
    {
        internal Utf8String(string value)
        {
            Pointer = Marshal.StringToCoTaskMemUTF8(value);
        }

        internal nint Pointer { get; }

        public void Dispose() => Marshal.FreeCoTaskMem(Pointer);
    }
}
