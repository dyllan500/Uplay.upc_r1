using DllShared;
using Shared;

namespace upc_r1.Exports;

public static class Main
{
    public static uint ProductId = 0;

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_GetLastError", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_GetLastError(IntPtr OutErrorString)
    {
        Log.Verbose("[{Function}] {OutErrorString}", nameof(UPLAY_GetLastError), OutErrorString);
        Marshal.WriteIntPtr(OutErrorString, Marshal.StringToHGlobalAnsi(string.Empty));
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_HasOverlappedOperationCompleted", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_HasOverlappedOperationCompleted(IntPtr Overlapped)
    {
        if (Overlapped == IntPtr.Zero)
            return false;
        var lapped = Marshal.PtrToStructure<UPLAY_Overlapped>(Overlapped);
        Log.Verbose("[{Function}] overlapped={Overlapped} completed={Completed}",
            nameof(UPLAY_HasOverlappedOperationCompleted), Overlapped, lapped.Completed);
        return lapped.Completed;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_GetOverlappedOperationResult", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_GetOverlappedOperationResult(IntPtr Overlapped, IntPtr OutResult)
    {
        var lapped = Marshal.PtrToStructure<UPLAY_Overlapped>(Overlapped);
        Marshal.WriteInt32(OutResult, (int)lapped.Result);
        Log.Information("[{Function}] overlapped={Overlapped} result={Result} out={OutResult}",
            nameof(UPLAY_GetOverlappedOperationResult), Overlapped, lapped.Result, OutResult);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_PeekNextEvent", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_PeekNextEvent(IntPtr OutEvent)
    {
        Log.Verbose("[{Function}] {OutEvent}", nameof(UPLAY_PeekNextEvent), OutEvent);
        OutEvent = IntPtr.Zero;
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_GetNextEvent", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_GetNextEvent(IntPtr OutEvent)
    {
        Log.Verbose("[{Function}] {OutEvent}", nameof(UPLAY_GetNextEvent), OutEvent);
        return upc_r1.CoopNet.TryWriteNextEvent(OutEvent);   // co-op: deliver queued invite
    }

    private static bool _initialised;

    /// <summary>
    /// Shared bring-up for every entry point a title might use to start the
    /// SDK. Idempotent: whichever of Init/Start/Startup a title calls first
    /// wins, and later calls only refresh the product id.
    /// </summary>
    private static void EnsureInitialised(string via, uint? uplayId)
    {
        if (uplayId is not null)
            ProductId = uplayId.Value;

        if (_initialised)
            return;
        _initialised = true;

        if (UPC_Json.Instance.UseDebug)
        {
            MainLogger.LevelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Verbose;
            MainLogger.FileLevelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Verbose;
        }
        MainLogger.FileName = Path.Combine(AOTHelper.CurrentPath, "upc_r1.log");
        MainLogger.CreateNew();
        Log.Information("[{Function}] emulator init (ProductId={ProductId})", via, ProductId);

        upc_r1.CoopNet.Start(UPC_Json.Instance.Account.AccountId);   // co-op LAN broker
        LoadDll.PluginPath = "r1";
        LoadDll.LoadPlugins();
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_Init", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_Init()
    {
        uint.TryParse(UPC_Json.Instance.Others.ApplicationId, out uint appId);
        EnsureInitialised(nameof(UPLAY_Init), appId != 0 ? appId : null);
        Log.Verbose("[{Function}]", nameof(UPLAY_Init));
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_Start", CallConvs = [typeof(CallConvCdecl)])]
    public static int UPLAY_Start(uint UplayId, uint Flags)
    {
        EnsureInitialised(nameof(UPLAY_Start), UplayId);
        Log.Information("[{Function}] {UplayId} {Flags}", nameof(UPLAY_Start), UplayId, Flags);
        return (int)UplayStartResult.Ok;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_Startup", CallConvs = [typeof(CallConvCdecl)])]
    public static int UPLAY_Startup(uint UplayId, uint GameVersion, IntPtr LanguageCountryCodeUtf8)
    {
        EnsureInitialised(nameof(UPLAY_Startup), UplayId);
        Log.Verbose("[{Function}] {UplayId} {GameVersion} {LanguageCountryCodeUtf8}", nameof(UPLAY_Startup), UplayId, GameVersion, LanguageCountryCodeUtf8);
        return (int)UplayStartResult.Ok;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_Update", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_Update()
    {
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_Quit", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_Quit()
    {
        Log.Verbose("[{Function}]", nameof(UPLAY_Quit));
        LoadDll.FreePlugins();
        MainLogger.Close();
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_SetLanguage", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_SetLanguage(IntPtr LanguageCountryCode)
    {
        Log.Verbose("[{Function}] {LanguageCountryCode}", nameof(UPLAY_SetLanguage), LanguageCountryCode);
        string? langCode = Marshal.PtrToStringUTF8(LanguageCountryCode);
        if (!string.IsNullOrEmpty(langCode))
            UPC_Json.Instance.Account.Country = langCode;
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_SetGameSession", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_SetGameSession(IntPtr GameSessionIdentifier, IntPtr SessionData, uint Flags)
    {
        Log.Verbose("[{Function}] {GameSessionIdentifier} {SessionData} {Flags}", nameof(UPLAY_SetLanguage), GameSessionIdentifier, SessionData, Flags);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_ClearGameSession", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_ClearGameSession()
    {
        Log.Verbose("[{Function}]", nameof(UPLAY_ClearGameSession));
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_PRESENCE_SetPresence", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_PRESENCE_SetPresence(uint presenceId, IntPtr tokens)
    {
        Log.Verbose("[{Function}] {presenceId} {tokens}", nameof(UPLAY_PRESENCE_SetPresence), presenceId, tokens);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_Release", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_Release(IntPtr list)
    {
        Log.Verbose("[{Function}] {list}", nameof(UPLAY_Release), list);
        if (list == IntPtr.Zero)
            return true;

        FreeList(list);
        return true;
    }
}
