using System.Collections.Concurrent;

namespace upc_r1;

/// <summary>
/// Owns the lifetime of one GetCredentials request until its overlapped result
/// has been published. 
/// </summary>
internal sealed class CredentialOperation
{
    private static readonly ConcurrentDictionary<IntPtr, CredentialOperation> Active = new();

    private readonly IntPtr _output;
    private readonly IntPtr _overlapped;
    private readonly string _accountId;
    private readonly string _name;
    private int _completed;

    private CredentialOperation(IntPtr output, IntPtr overlapped, string accountId, string name)
    {
        _output = output;
        _overlapped = overlapped;
        _accountId = accountId;
        _name = name;
    }

    public static bool Start(IntPtr output, IntPtr overlapped, UPC_Json.Account account)
    {
        if (overlapped == IntPtr.Zero)
            return false;

        var operation = new CredentialOperation(output, overlapped, account.AccountId, account.Name);
        if (!Active.TryAdd(overlapped, operation))
        {
            Log.Warning("[{Function}] overlapped request is already active",
                "UPLAY_USER_GetCredentials");
            return false;
        }

        try
        {
            ThreadPool.QueueUserWorkItem(static state => ((CredentialOperation)state!).Complete(), operation);
            return true;
        }
        catch (Exception exception)
        {
            Active.TryRemove(overlapped, out _);
            Log.Warning("[{Function}] could not queue credential callback ({ExceptionType})",
                "UPLAY_USER_GetCredentials", exception.GetType().Name);
            return false;
        }
    }

    private void Complete()
    {
        try
        {
            // This probe deliberately writes no credential fields.  It exists only
            // for debugger-assisted consumer tracing; the default remains failure
            // until the native output layout is confirmed.
            var probeOk = string.Equals(
                Environment.GetEnvironmentVariable("UPC_R1_CREDENTIAL_PROBE_OK"),
                "1", StringComparison.Ordinal);

            Log.Information("[{Function}] callback completing {Result}; output layout is unconfirmed for {AccountId} ({Name})",
                "UPLAY_USER_GetCredentials",
                probeOk ? "Ok (probe, no output write)" : "Failed",
                _accountId,
                _name);
            CompleteOnce(probeOk ? UPLAY_OverlappedResult.Ok : UPLAY_OverlappedResult.Failed);
        }
        catch (Exception exception)
        {
            Log.Warning("[{Function}] credential callback failed ({ExceptionType})",
                "UPLAY_USER_GetCredentials", exception.GetType().Name);
            CompleteOnce(UPLAY_OverlappedResult.Failed);
        }
        finally
        {
            Active.TryRemove(_overlapped, out _);
        }
    }

    private void CompleteOnce(UPLAY_OverlappedResult result)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
            return;
        _ = _output;
        Basics.WriteOverlappedResult(_overlapped, true, result);
    }
}
