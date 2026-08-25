namespace upc_r1.Exports;

public class Friends
{
    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_AddPlayedWith", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_AddPlayedWith(IntPtr DescriptionUtf8, IntPtr AccountIdListUtf8, uint AccountIdListLength)
    {
        Log.Information("[{Function}] {DescriptionUtf8} {AccountIdListUtf8} {AccountIdListLength}", nameof(UPLAY_FRIENDS_AddPlayedWith), Marshal.PtrToStringAnsi(DescriptionUtf8), AccountIdListUtf8, AccountIdListLength);
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_AddToBlackList", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_AddToBlackList(IntPtr AccountIdUtf8, IntPtr Overlapped)
    {
        Log.Information("[{Function}] {AccountIdUtf8} {Overlapped}", nameof(UPLAY_FRIENDS_AddPlayedWith), Marshal.PtrToStringAnsi(AccountIdUtf8), Overlapped);
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_DisableFriendMenuItem", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_DisableFriendMenuItem(uint Id)
    {
        Log.Information("[{Function}] {Id}", nameof(UPLAY_FRIENDS_DisableFriendMenuItem), Id);
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_EnableFriendMenuItem", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_EnableFriendMenuItem(uint Id, uint MenuItemMode, uint Filter)
    {
        Log.Information("[{Function}] {Id} {MenuItemMode} {Filter}", nameof(UPLAY_FRIENDS_EnableFriendMenuItem), Id, MenuItemMode, Filter);
        return false;
    }

    /// <summary>
    /// Backing memory for the friend list handed to the game. The caller keeps
    /// the pointers until it asks again, so the previous call's block is only
    /// released when a new one replaces it.
    /// </summary>
    private static IntPtr _friendArray = IntPtr.Zero;
    private static IntPtr[] _friendEntries = [];

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_GetFriendList", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_GetFriendList(uint FriendListFilter, IntPtr OutFriendList)
    {
        Log.Information("[{Function}] {FriendListFilter} {OutFriendList}", nameof(UPLAY_FRIENDS_GetFriendList), FriendListFilter, OutFriendList);
        if (OutFriendList == IntPtr.Zero)
            return true;

        List<UPLAY_FRIEND_Friend> friends = [];
        foreach (var acc in upc_r1.CoopNet.GetPeerAccounts())
        {
            IntPtr presPtr = Marshal.AllocHGlobal(Marshal.SizeOf<UPLAY_PRESENCE_Presence>());
            Marshal.StructureToPtr(new UPLAY_PRESENCE_Presence
            {
                status = UPLAY_PRESENCE_Status.InGame,
                richPresenceUtf8 = "",
                state = 0,
                GameSessionPtr = IntPtr.Zero
            }, presPtr, false);
            friends.Add(new UPLAY_FRIEND_Friend
            {
                accountIdUtf8 = acc,
                nickUtf8 = "Player",
                relationship = Uplay.Uplaydll.Relationship.Friend,
                avatarId = 0,
                PresencePtr = presPtr,
                isBlacklisted = false
            });
        }
        ReleaseFriendList();
        if (friends.Count > 0)
        {
            int stride = Marshal.SizeOf<UPLAY_FRIEND_Friend>();
            _friendEntries = new IntPtr[friends.Count];
            for (int i = 0; i < friends.Count; i++)
            {
                _friendEntries[i] = Marshal.AllocHGlobal(stride);
                Marshal.StructureToPtr(friends[i], _friendEntries[i], false);
            }
            _friendArray = Marshal.AllocHGlobal(IntPtr.Size * friends.Count);
            for (int i = 0; i < friends.Count; i++)
                Marshal.WriteIntPtr(_friendArray, i * IntPtr.Size, _friendEntries[i]);
        }

        Marshal.WriteInt32(OutFriendList, 0, friends.Count);
        Marshal.WriteIntPtr(OutFriendList, 4, _friendArray);
        Log.Information("[{Function}] returned {N} friend(s) (count@+0, array@+4={Array})",
            nameof(UPLAY_FRIENDS_GetFriendList), friends.Count, _friendArray);
        return true;
    }

    /// <summary>Frees the block handed out by the previous GetFriendList call.</summary>
    private static void ReleaseFriendList()
    {
        foreach (IntPtr entry in _friendEntries)
            if (entry != IntPtr.Zero)
                Marshal.FreeHGlobal(entry);
        _friendEntries = [];
        if (_friendArray != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_friendArray);
            _friendArray = IntPtr.Zero;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_Init", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_Init(uint Flags)
    {
        Log.Information("[{Function}] {Flags}", nameof(UPLAY_FRIENDS_Init), Flags);
        return true;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_InviteToGame", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_InviteToGame(IntPtr AccountIdUtf8, IntPtr Overlapped)
    {
        Log.Information("[{Function}] {AccountIdUtf8} {Overlapped}", nameof(UPLAY_FRIENDS_GetFriendList), Marshal.PtrToStringAnsi(AccountIdUtf8), Overlapped);
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_IsBlackListed", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_IsBlackListed(IntPtr AccountIdUtf8)
    {
        Log.Information("[{Function}] {AccountIdUtf8}", nameof(UPLAY_FRIENDS_IsBlackListed), Marshal.PtrToStringAnsi(AccountIdUtf8));
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_IsFriend", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_IsFriend(IntPtr AccountIdUtf8)
    {
        string? acc = Marshal.PtrToStringAnsi(AccountIdUtf8);
        Log.Information("[{Function}] {AccountIdUtf8}", nameof(UPLAY_FRIENDS_IsFriend), acc);
        return acc != null && upc_r1.CoopNet.GetPeerAccounts().Contains(acc);
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_RemoveFriendship", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_RemoveFriendship(IntPtr AccountIdUtf8, IntPtr Overlapped)
    {
        Log.Information("[{Function}] {AccountIdUtf8} {Overlapped}", nameof(UPLAY_FRIENDS_RemoveFriendship), Marshal.PtrToStringAnsi(AccountIdUtf8), Overlapped);
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_RemoveFromBlackList", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_RemoveFromBlackList(IntPtr AccountIdUtf8, IntPtr Overlapped)
    {
        Log.Information("[{Function}] {AccountIdUtf8} {Overlapped}", nameof(UPLAY_FRIENDS_RemoveFromBlackList), Marshal.PtrToStringAnsi(AccountIdUtf8), Overlapped);
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_RequestFriendship", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_RequestFriendship(IntPtr SearchStringUtf8, IntPtr Overlapped)
    {
        Log.Information("[{Function}] {SearchStringUtf8} {Overlapped}", nameof(UPLAY_FRIENDS_RequestFriendship), Marshal.PtrToStringAnsi(SearchStringUtf8), Overlapped);
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_RespondToGameInvite", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_RespondToGameInvite(uint InvitationId, IntPtr Accept)
    {
        Log.Information("[{Function}] {InvitationId} {Accept}", nameof(UPLAY_FRIENDS_RespondToGameInvite), InvitationId, Accept);
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_ShowFriendSelectionUI", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_ShowFriendSelectionUI(IntPtr AccountIdFilterListUtf8, uint AccountIdFilterListLength, IntPtr Overlapped, IntPtr OutResult)
    {
        Log.Information("[{Function}] {AccountIdFilterListUtf8} {AccountIdFilterListLength} {Overlapped} {OutResult}", nameof(UPLAY_FRIENDS_ShowFriendSelectionUI), AccountIdFilterListUtf8, AccountIdFilterListLength, Overlapped, OutResult);
        return false;
    }

    [UnmanagedCallersOnly(EntryPoint = "UPLAY_FRIENDS_ShowInviteFriendsToGameUI", CallConvs = [typeof(CallConvCdecl)])]
    public static bool UPLAY_FRIENDS_ShowInviteFriendsToGameUI(IntPtr AccountIdFilterListUtf8, uint AccountIdFilterListLength)
    {
        Log.Information("[{Function}] {AccountIdFilterListUtf8} {AccountIdFilterListLength}", nameof(UPLAY_FRIENDS_ShowFriendSelectionUI), AccountIdFilterListUtf8, AccountIdFilterListLength);
        return false;
    }
}
