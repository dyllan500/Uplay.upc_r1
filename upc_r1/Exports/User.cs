using System.Collections.Generic;

namespace upc_r1.Exports;

internal class User
{
    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_ClearGameSession", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_ClearGameSession()
    {
        Log.Information(nameof(UPLAY_USER_ClearGameSession));
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_ConsumeItem", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_ConsumeItem(IntPtr aTransactionIdUtf8, uint aUplayId, uint aQuantity, IntPtr aSignatureUtf8, IntPtr aOverlapped, IntPtr aOutResult)
    {
        Log.Information(nameof(UPLAY_USER_ConsumeItem), [aTransactionIdUtf8, aUplayId, aQuantity, aSignatureUtf8, aOverlapped, aOutResult]);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetAccountId", CallConvs = [typeof(CallConvCdecl)])]
    public static IntPtr UPLAY_USER_GetAccountId(IntPtr aOutAccountId)
    {
        Log.Information(nameof(UPLAY_USER_GetAccountId), [aOutAccountId]);
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetAccountIdUtf8", CallConvs = [typeof(CallConvCdecl)])]
    public static IntPtr UPLAY_USER_GetAccountIdUtf8()
    {
        Log.Information(nameof(UPLAY_USER_GetAccountIdUtf8));
        return Marshal.StringToHGlobalAnsi(UPC_Json.Instance.Account.AccountId);
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetCPUScore", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_GetCPUScore(IntPtr aOutCpuScore)
    {
        Log.Information(nameof(UPLAY_USER_GetCPUScore), [aOutCpuScore]);
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetCdKeyUtf8", CallConvs = [typeof(CallConvCdecl)])]
    public static IntPtr UPLAY_USER_GetCdKeyUtf8(uint aUplayId)
    {
        Log.Information(nameof(UPLAY_USER_GetCdKeyUtf8), [aUplayId]);
        string defaultKey = "1111-2222 -3333-4444";
        var list = UPC_Json.Instance.CDKeys.Where(x => x.ProductId == aUplayId);
        if (list.Count() == 1)
        {
            defaultKey = list.ToList()[0].Key;
        }
        return Marshal.StringToHGlobalAnsi(defaultKey);
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetCdKeys", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_GetCdKeys(IntPtr aOutCdKeyList, IntPtr aOverlapped)
    {
        Log.Information(nameof(UPLAY_USER_GetCdKeys), [aOutCdKeyList, aOverlapped]);
        var uplayKeys = UPC_Json.Instance.CDKeys;
        int count = uplayKeys.Count;

        List<UplayKey> keys = [.. UPC_Json.Instance.CDKeys.Select(x => new UplayKey() { CdKey = Marshal.StringToHGlobalAnsi(x.Key) })];

        WriteOutList(aOutCdKeyList, keys);

        Basics.WriteOverlappedResult(aOverlapped, true, aOverlapped != IntPtr.Zero ? UPLAY_OverlappedResult.Ok : UPLAY_OverlappedResult.Failed);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetConsumableItems", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_GetConsumableItems(IntPtr aOutConsumableItemsList)
    {
        Log.Information(nameof(UPLAY_USER_GetConsumableItems), [aOutConsumableItemsList]);
        if (aOutConsumableItemsList != IntPtr.Zero)
        {
            Marshal.WriteInt32(aOutConsumableItemsList, 0, 0);   // count = 0
            Marshal.WriteInt64(aOutConsumableItemsList, 4, 0);   // items = NULL (packed at +4)
        }
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetCredentials", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_GetCredentials(IntPtr aOutUserCredentials, IntPtr aOverlapped)
    {
        Log.Information(nameof(UPLAY_USER_GetCredentials), [aOutUserCredentials, aOverlapped]);

        if (!TryLoadCredentialAccount(out var account))
        {
            CompleteCredentialRequest(aOverlapped, UPLAY_OverlappedResult.Failed);
            return false;
        }

        if (aOutUserCredentials == IntPtr.Zero)
        {
            Log.Warning("[{Function}] credential output buffer is null",
                nameof(UPLAY_USER_GetCredentials));
            CompleteCredentialRequest(aOverlapped, UPLAY_OverlappedResult.Failed);
            return false;
        }

        Log.Information("[{Function}] validated local account {AccountId} ({Name}); credential layout remains unconfirmed",
            nameof(UPLAY_USER_GetCredentials), account.AccountId, account.Name);

        if (!CredentialOperation.Start(aOutUserCredentials, aOverlapped, account))
        {
            CompleteCredentialRequest(aOverlapped, UPLAY_OverlappedResult.Failed);
            return false;
        }

        return true;
    }

    private static bool TryLoadCredentialAccount(out UPC_Json.Account account)
    {
        account = null!;

        try
        {
            var candidate = UPC_Json.Instance.Account;
            if (candidate is null ||
                string.IsNullOrWhiteSpace(candidate.AccountId) ||
                candidate.AccountId.Contains('\0') ||
                string.IsNullOrWhiteSpace(candidate.Name) ||
                candidate.Name.Contains('\0') ||
                string.IsNullOrEmpty(candidate.Password) ||
                candidate.Password.Contains('\0'))
            {
                Log.Warning("[{Function}] local account is missing a required non-secret field",
                    nameof(UPLAY_USER_GetCredentials));
                return false;
            }

            account = candidate;
            return true;
        }
        catch (Exception exception)
        {
            Log.Warning("[{Function}] local account could not be loaded ({ExceptionType})",
                nameof(UPLAY_USER_GetCredentials), exception.GetType().Name);
            return false;
        }
    }

    private static void CompleteCredentialRequest(IntPtr aOverlapped, UPLAY_OverlappedResult result)
    {
        if (aOverlapped != IntPtr.Zero)
            Basics.WriteOverlappedResult(aOverlapped, true, result);
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetEmail", CallConvs = [typeof(CallConvCdecl)])]
    public static IntPtr UPLAY_USER_GetEmail(IntPtr aOutEmail)
    {
        Log.Information(nameof(UPLAY_USER_GetEmail), [aOutEmail]);
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetEmailUtf8", CallConvs = [typeof(CallConvCdecl)])]
    public static IntPtr UPLAY_USER_GetEmailUtf8()
    {
        Log.Information(nameof(UPLAY_USER_GetEmailUtf8), []);
        return Marshal.StringToHGlobalAnsi(UPC_Json.Instance.Account.Email);
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetGPUScore", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_GetGPUScore(IntPtr aOutGpuScore)
    {
        Log.Information(nameof(UPLAY_USER_GetGPUScore), [aOutGpuScore]);
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetGPUScoreConfidenceLevel", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_GetGPUScoreConfidenceLevel(IntPtr aOutConfidenceLevel)
    {
        Log.Information(nameof(UPLAY_USER_GetGPUScoreConfidenceLevel), [aOutConfidenceLevel]);
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetNameUtf8", CallConvs = [typeof(CallConvCdecl)])]
    public static IntPtr UPLAY_USER_GetNameUtf8()
    {
        Log.Information(nameof(UPLAY_USER_GetNameUtf8), []);
        return Marshal.StringToHGlobalAnsi(UPC_Json.Instance.Account.Name);
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetPassword", CallConvs = [typeof(CallConvCdecl)])]
    public static IntPtr UPLAY_USER_GetPassword(IntPtr aOutPassword)
    {
        Log.Information(nameof(UPLAY_USER_GetPassword), [aOutPassword]);
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetPasswordUtf8", CallConvs = [typeof(CallConvCdecl)])]
    public static IntPtr UPLAY_USER_GetPasswordUtf8()
    {
        Log.Information(nameof(UPLAY_USER_GetPasswordUtf8), []);
        return Marshal.StringToHGlobalAnsi(UPC_Json.Instance.Account.Password);
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetProfile", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_GetProfile(IntPtr aAccountIdUtf8, IntPtr aOverlapped, IntPtr aOutProfile)
    {
        Log.Information(nameof(UPLAY_USER_GetProfile), [aAccountIdUtf8, aOverlapped, aOutProfile]);
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetTicketUtf8", CallConvs = [typeof(CallConvCdecl)])]
    public static IntPtr UPLAY_USER_GetTicketUtf8()
    {
        Log.Information(nameof(UPLAY_USER_GetTicketUtf8), []);
        return Marshal.StringToHGlobalAnsi(UPC_Json.Instance.Account.Ticket);
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetUsername", CallConvs = [typeof(CallConvCdecl)])]
    public static IntPtr UPLAY_USER_GetUsername(IntPtr aOutUsername)
    {
        Log.Information(nameof(UPLAY_USER_GetUsername), [aOutUsername]);
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_GetUsernameUtf8", CallConvs = [typeof(CallConvCdecl)])]
    public static IntPtr UPLAY_USER_GetUsernameUtf8()
    {
        Log.Information(nameof(UPLAY_USER_GetUsernameUtf8), []);
        return Marshal.StringToHGlobalAnsi(UPC_Json.Instance.Account.Name);
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_IsConnected", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_IsConnected()
    {
        Log.Information(nameof(UPLAY_USER_IsConnected));
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_IsInOfflineMode", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_IsInOfflineMode()
    {
        Log.Information(nameof(UPLAY_USER_IsInOfflineMode), []);
        return UPC_Json.Instance.Others.OfflineMode;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_IsOwned", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_IsOwned(uint aUplayId)
    {
        Log.Information(nameof(UPLAY_USER_IsOwned), [aUplayId]);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_ReleaseCdKeyList", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_ReleaseCdKeyList(IntPtr aCdKeyList)
    {
        Log.Information(nameof(UPLAY_USER_ReleaseCdKeyList), [aCdKeyList]);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_ReleaseConsumeItemResult", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_ReleaseConsumeItemResult(IntPtr aConsumeItemResult)
    {
        Log.Information(nameof(UPLAY_USER_ReleaseConsumeItemResult), [aConsumeItemResult]);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_ReleaseProfile", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_ReleaseProfile(IntPtr aOutProfile)
    {
        Log.Information(nameof(UPLAY_USER_ReleaseProfile), [aOutProfile]);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_USER_SetGameSession", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_USER_SetGameSession(ulong GameSessionIdentifier, IntPtr SessionData, uint Flags)
    {
        Log.Information(nameof(UPLAY_USER_SetGameSession), [GameSessionIdentifier, SessionData, Flags]);
        UPLAY_DataBlob blob = Marshal.PtrToStructure<UPLAY_DataBlob>(SessionData);
        if (blob.data != IntPtr.Zero && blob.numBytes > 0 && blob.numBytes < (1 << 20))
        {
            byte[] bytes = new byte[blob.numBytes];
            Marshal.Copy(blob.data, bytes, 0, (int)blob.numBytes);
            upc_r1.CoopNet.PublishSession(GameSessionIdentifier, bytes);   // co-op: broadcast session over LAN
        }
        return true; 
    }
}
