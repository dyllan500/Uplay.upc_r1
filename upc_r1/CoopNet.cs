using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace upc_r1;

public static class CoopNet
{
    // UDP port for the LAN broker
    private const int BrokerPort = 47100;
    // Message tags
    private const byte MSG_ANNOUNCE = 0x01;   // [tag][accountLen:u16][accountUtf8]
    private const byte MSG_SESSION  = 0x02;   // [tag][accountLen:u16][account][id:u64][blobLen:u32][blob]

    private static Socket? _sock;
    private static volatile bool _running;
    private static string _myAccount = "";
    private static readonly UPLAY_EventType InviteAccepted = UPLAY_EventType.UPLAY_Event_FriendsGameInviteAccepted;
    private static readonly ConcurrentDictionary<string, IPEndPoint> _peers = new();
    private static readonly ConcurrentQueue<Pending> _events = new();
    private static readonly ConcurrentDictionary<ulong, byte> _invitedSessions = new();
    private static readonly ConcurrentDictionary<ulong, byte> _publishedSessions = new();
    private static volatile byte[]? _lastSessionPkt;
    private static volatile bool _isHost;
    private static volatile bool _pinnedPeer;
    public static bool IsHost => _isHost;
    
    public static void MarkLocalAsHost()
    {
        if (_isHost || _pinnedPeer) return;
        _isHost = true;
        Serilog.Log.Information("[CoopNet] local player opened the invite overlay — HOSTING this session");
    }

    private sealed class Pending
    {
        public UPLAY_EventType Type = InviteAccepted;
        public ulong Id;
        public byte[] Blob = [];
        public string From = "";
    }

    private static void EnqueueSimpleEvent(UPLAY_EventType type) =>
        _events.Enqueue(new Pending { Type = type });

    public static bool Started { get; private set; }

    public static void Start(string accountId)
    {
        if (Started) return;
        _myAccount = accountId ?? "";

        switch (UPC_Json.Instance.Coop.IsHost)
        {
            case true:
                _isHost = true;
                Serilog.Log.Information("[CoopNet] upc.json Coop.IsHost=true — pinned HOST");
                break;
            case false:
                _pinnedPeer = true;
                Serilog.Log.Information("[CoopNet] upc.json Coop.IsHost=false — pinned PEER");
                break;
            default:
                Serilog.Log.Information("[CoopNet] Coop.IsHost unset — auto: whoever opens the invite overlay hosts");
                break;
        }

        try
        {
            _sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _sock.EnableBroadcast = true;
            _sock.Bind(new IPEndPoint(IPAddress.Any, BrokerPort));
            _running = true;
            Started = true;
            new Thread(RxLoop) { IsBackground = true, Name = "CoopNet.Rx" }.Start();
            new Thread(AnnounceLoop) { IsBackground = true, Name = "CoopNet.Announce" }.Start();
            Serilog.Log.Information("[CoopNet] started on :{Port} as account {Acct}", BrokerPort, _myAccount);

            EnqueueSimpleEvent(UPLAY_EventType.UPLAY_Event_FriendsFriendListUpdated);
            EnqueueSimpleEvent(UPLAY_EventType.UPLAY_Event_PartyMemberListChanged);
        }
        catch (Exception e)
        {
            Serilog.Log.Error("[CoopNet] start failed: {Msg}", e.Message);
        }
    }

    public static void Stop()
    {
        _running = false;
        try { _sock?.Close(); } catch { }
        Started = false;
    }

    public static void PublishSession(ulong id, byte[] blob)
    {
        if (!Started || blob is null || blob.Length == 0) return;

        if (!_isHost) return;

        if (!_publishedSessions.TryAdd(id, 0))
            return;

        var acct = Encoding.UTF8.GetBytes(_myAccount);
        var pkt = new byte[1 + 2 + acct.Length + 8 + 4 + blob.Length];
        int p = 0;
        pkt[p++] = MSG_SESSION;
        pkt[p++] = (byte)(acct.Length & 0xff); pkt[p++] = (byte)(acct.Length >> 8);
        Array.Copy(acct, 0, pkt, p, acct.Length); p += acct.Length;
        BitConverter.TryWriteBytes(pkt.AsSpan(p), id); p += 8;
        BitConverter.TryWriteBytes(pkt.AsSpan(p), (uint)blob.Length); p += 4;
        Array.Copy(blob, 0, pkt, p, blob.Length);
        _lastSessionPkt = pkt;
        Broadcast(pkt);
        Serilog.Log.Information("[CoopNet] published session id=0x{Id:x} blob={Len}B to {N} peer(s)", id, blob.Length, _peers.Count);
    }

    public static bool TryWriteNextEvent(IntPtr outEvent)
    {
        if (!_events.TryDequeue(out var ev) || outEvent == IntPtr.Zero) return false;

        int ptr = IntPtr.Size;

        if (ev.Type != InviteAccepted)
        {
            Marshal.WriteInt32(outEvent, 0x00, (int)ev.Type);
            Marshal.WriteIntPtr(outEvent, ptr, IntPtr.Zero);
            Serilog.Log.Information("[CoopNet] delivered {Type}({Val})", ev.Type, (int)ev.Type);
            return true;
        }

        IntPtr blobPtr = Marshal.AllocHGlobal(ev.Blob.Length);
        Marshal.Copy(ev.Blob, 0, blobPtr, ev.Blob.Length);

        IntPtr gsPtr = Marshal.AllocHGlobal(8 + ptr + 8);
        Marshal.WriteInt64(gsPtr, 0x00, (long)ev.Id);
        Marshal.WriteIntPtr(gsPtr, 8, blobPtr);
        Marshal.WriteInt32(gsPtr, 8 + ptr, ev.Blob.Length);

        IntPtr acctPtr = Marshal.StringToHGlobalAnsi(ev.From);
        IntPtr giaPtr = Marshal.AllocHGlobal(ptr * 2);
        Marshal.WriteIntPtr(giaPtr, 0x00, gsPtr);
        Marshal.WriteIntPtr(giaPtr, ptr, acctPtr);

        Marshal.WriteInt32(outEvent, 0x00, (int)InviteAccepted);
        Marshal.WriteIntPtr(outEvent, ptr, giaPtr);

        Serilog.Log.Information("[CoopNet] delivered FriendsGameInviteAccepted(10002) id=0x{Id:x} from {From}", ev.Id, ev.From);
        return true;
    }

    public static List<string> GetPeerAccounts() => new(_peers.Keys);

    private static void Broadcast(byte[] pkt)
    {
        try
        {
            _sock!.SendTo(pkt, new IPEndPoint(IPAddress.Broadcast, BrokerPort));
            foreach (var ep in _peers.Values) { try { _sock!.SendTo(pkt, ep); } catch { } }
        }
        catch (Exception e) { Serilog.Log.Warning("[CoopNet] broadcast failed: {Msg}", e.Message); }
    }

    private static void AnnounceLoop()
    {
        var acct = Encoding.UTF8.GetBytes(_myAccount);
        var pkt = new byte[1 + 2 + acct.Length];
        pkt[0] = MSG_ANNOUNCE;
        pkt[1] = (byte)(acct.Length & 0xff); pkt[2] = (byte)(acct.Length >> 8);
        Array.Copy(acct, 0, pkt, 3, acct.Length);
        while (_running)
        {
            try { _sock!.SendTo(pkt, new IPEndPoint(IPAddress.Broadcast, BrokerPort)); } catch { }
            Thread.Sleep(3000);
        }
    }

    private static void RxLoop()
    {
        var buf = new byte[65536];
        var from = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            int n;
            try { n = _sock!.ReceiveFrom(buf, ref from); }
            catch { if (!_running) break; continue; }
            if (n < 3) continue;
            var ep = (IPEndPoint)from;
            try { Handle(buf, n, ep); } catch (Exception e) { Serilog.Log.Warning("[CoopNet] rx handle: {Msg}", e.Message); }
        }
    }

    private static void Handle(byte[] buf, int n, IPEndPoint ep)
    {
        int p = 0;
        byte tag = buf[p++];
        int acctLen = buf[p++] | (buf[p++] << 8);
        if (acctLen < 0 || p + acctLen > n) return;
        string acct = Encoding.UTF8.GetString(buf, p, acctLen); p += acctLen;
        if (acct == _myAccount) return;                 // ignore our own packets

        bool isNewPeer = !_peers.ContainsKey(acct);
        _peers[acct] = ep;                              // discovery (any message = presence)
        if (isNewPeer)
        {
            Serilog.Log.Information("[CoopNet] discovered new peer {Acct} @ {Ep}", acct, ep);
            EnqueueSimpleEvent(UPLAY_EventType.UPLAY_Event_FriendsFriendListUpdated);
            EnqueueSimpleEvent(UPLAY_EventType.UPLAY_Event_PartyMemberListChanged);
            var pkt = _lastSessionPkt;
            if (pkt != null)
            {
                try { _sock!.SendTo(pkt, ep); } catch { }
                Serilog.Log.Information("[CoopNet] sent current session to new peer {Acct}", acct);
            }
        }

        if (tag == MSG_SESSION)
        {
            if (p + 12 > n) return;
            ulong id = BitConverter.ToUInt64(buf, p); p += 8;
            uint blobLen = BitConverter.ToUInt32(buf, p); p += 4;
            if (blobLen == 0 || p + blobLen > n) return;

            if (_isHost)
                return;

            if (_publishedSessions.ContainsKey(id))
                return;

            if (!_invitedSessions.TryAdd(id, 0))
                return;

            var blob = new byte[blobLen];
            Array.Copy(buf, p, blob, 0, (int)blobLen);
            _events.Enqueue(new Pending { Id = id, Blob = blob, From = acct });
            Serilog.Log.Information("[CoopNet] rx SESSION id=0x{Id:x} blob={Len}B from {Acct} — queued invite (first time)", id, blobLen, acct);
        }
        else if (tag == MSG_ANNOUNCE)
        {
            Serilog.Log.Verbose("[CoopNet] rx ANNOUNCE from {Acct} @ {Ep}", acct, ep);
        }
    }
}
