using System.Collections.Concurrent;
using System.Text;

namespace upc_r1;

/// <summary>
/// Owns the lifetime of one GetCredentials request until its overlapped result
/// has been published. 
/// </summary>
internal sealed class CredentialOperation
{
    private const int UsernameOffset = 0x000;
    private const int UsernameCapacity = 0x100;
    private const int PasswordOffset = 0x100;
    private const int PasswordCapacity = 0x40;
    private const int AccountIdOffset = 0x140;
    private const int AccountIdCapacity = 0x40;
    private const int CredentialBufferSize = 0x180;

    private static readonly ConcurrentDictionary<IntPtr, CredentialOperation> Active = new();

    private readonly IntPtr _output;
    private readonly IntPtr _overlapped;
    private readonly string _accountId;
    private readonly string _name;
    private readonly string _password;
    private int _completed;

    private CredentialOperation(IntPtr output, IntPtr overlapped, string accountId, string name,
        string password)
    {
        _output = output;
        _overlapped = overlapped;
        _accountId = accountId;
        _name = name;
        _password = password;
    }

    public static bool Start(IntPtr output, IntPtr overlapped, UPC_Json.Account account)
    {
        if (overlapped == IntPtr.Zero)
            return false;

        var operation = new CredentialOperation(
            output, overlapped, account.AccountId, account.Name, account.Password);
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

            Marshal.Copy(new byte[CredentialBufferSize], 0, _output, CredentialBufferSize);
            WriteStringField(_output, UsernameOffset, UsernameCapacity, _name);
            WriteStringField(_output, PasswordOffset, PasswordCapacity, _password);
            WriteStringField(_output, AccountIdOffset, AccountIdCapacity, _accountId);

            Log.Information("[{Function}] callback completing Ok with native 0x180-byte credential buffer for {AccountId} ({Name})",
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

    private static void WriteStringField(IntPtr output, int offset, int capacity, string value)
    {
        var encoded = Encoding.UTF8.GetBytes(value);
        var count = Math.Min(encoded.Length, capacity - 1);
        Marshal.Copy(encoded, 0, IntPtr.Add(output, offset), count);
    }

    private void CompleteOnce(UPLAY_OverlappedResult result)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
            return;
        _ = _output;
        Basics.WriteOverlappedResult(_overlapped, true, result);
    }
}
