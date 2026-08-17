using System.Collections.Concurrent;
using System.Text;

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
            if (_output == IntPtr.Zero)
                throw new InvalidOperationException("credential output buffer is null");

            var username = Encoding.UTF8.GetBytes(_name);
            Marshal.Copy(username, 0, _output, username.Length);
            Marshal.WriteByte(_output, username.Length, 0);

            Log.Information("[{Function}] callback completing Ok with confirmed username buffer for {AccountId} ({Name})",
                "UPLAY_USER_GetCredentials",
                _accountId,
                _name);
            CompleteOnce(UPLAY_OverlappedResult.Ok);
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
