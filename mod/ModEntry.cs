using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MobileCasher {
public sealed class ModEntry : IMod {
    internal const string ModId = "mobilecasher";
    Bridge bridge;
    public void Init(ModManifest manifest) { }
    public void OnEnable() {
        var go = new GameObject("MobileCasher");
        UnityEngine.Object.DontDestroyOnLoad(go);
        bridge = go.AddComponent<Bridge>();
    }
    public void OnDisable() {
        if (bridge != null) {
            bridge.Shutdown();
            UnityEngine.Object.Destroy(bridge.gameObject);
        }
    }
}

// Scope the customer to the sale, including inner cash changes and the original mod's postfix.
// A later balance-only notification must never borrow the last customer's identity.
[HarmonyPatch]
internal static class SaleScope {
    internal sealed class Payer {
        public string Name;
        public Sprite Sprite;
    }
    [ThreadStatic] internal static Payer Current;
    internal struct ScopeState { public Payer Previous; public int? CashBefore; }
    static IEnumerable<MethodBase> TargetMethods() {
        yield return AccessTools.Method(typeof(PlayerStore), "SellItemFromTable");
        yield return AccessTools.Method(typeof(PlayerStore), "SellAllItemFromTable");
        yield return AccessTools.Method(typeof(PlayerStore), "SellItem", new[]{typeof(GameItem)});
        yield return AccessTools.Method(typeof(NegociationUIManager), "SellMultiple", new[]{typeof(List<GameItem>),typeof(bool)});
        yield return AccessTools.Method(typeof(NegociationUIManager), "OnOfferAcceptClick");
        yield return AccessTools.Method(typeof(AppraisalServiceHelper), "GiveAppraisalFee", new[]{typeof(int),typeof(int),typeof(int)});
    }
    [HarmonyPriority(Priority.First)]
    static void Prefix(MethodBase __originalMethod, out ScopeState __state) {
        __state = new ScopeState { Previous = Current };
        var client = PlayerStore.Instance?.currentClientInstance?.GetClientBlueprint();
        if (client != null)
            Current = new Payer { Name = client.displayName, Sprite = StoreClientMono.Instance?.image?.sprite };
        if (__originalMethod.Name == "SellItem" && __state.Previous == null && PlayerStore.Instance != null) {
            __state.CashBefore = PlayerStore.Instance.playerCash;
            Bridge.PrimeSingleSale();
        }
    }
    static void Postfix(ScopeState __state) {
        if (__state.CashBefore.HasValue) Bridge.FlushSingleSale(__state.CashBefore.Value);
    }
    static void Finalizer(ScopeState __state) { Current = __state.Previous; }
}

public sealed class Bridge : MonoBehaviour {
    internal static Bridge Current { get; private set; }
    static Bridge active;
    readonly Harmony voice = new Harmony("mobilecasher.phone-audio");
    LanServer server;
    Texture2D connectionQr;
    string localUrl;
    MethodInfo parser;
    PropertyInfo voiceInstance;
    MethodInfo prime, poll;
    PropertyInfo platform, speed, volume, gap;
    LoadedMod dependency;
    float nextCheck;
    bool failed;
    string status = "等待前置模组 announce_the_transaction";
    internal Texture2D QrTexture => connectionQr;
    internal string ConnectionUrl => localUrl;
    internal string ConnectionStatus => server != null && server.Connected ? "手机已连接" : status;
    void Awake() { Current = this; }
    // Workshop assemblies load after local mods. Resolve the enabled instance on the first game frame.
    void Update() {
        if (server == null && !failed && Time.unscaledTime >= nextCheck) {
            nextCheck = Time.unscaledTime + 1;
            var dep = ModLoader.Mods.FirstOrDefault(m => m.IsEnabled && m.Manifest.ID == "announce_the_transaction");
            if (dep != null) try { Bind(dep); } catch (Exception e) {
                Shutdown(); failed = true; status = "手机收款启动失败：" + e.GetBaseException().Message;
                Debug.LogError("[MobileCasher] " + status);
            }
        }
        if (server != null && !dependency.IsEnabled) { Shutdown(); status = "等待前置模组 announce_the_transaction"; }
    }
    void Bind(LoadedMod dep) {
        var player = dep.Assembly.GetType("AnnounceTheTransaction.AnnouncementPlayer", true);
        var settings = dep.Assembly.GetType("AnnounceTheTransaction.ModSettings", true);
        voiceInstance = player.GetProperty("Instance");
        prime = AccessTools.Method(player, "EnsureBaselineBeforeTransaction");
        poll = player.GetMethod("PollCashNow");
        parser = dep.Assembly.GetType("AnnounceTheTransaction.AlipayNumberParser", true).GetMethod("BuildSoundSequence");
        platform = settings.GetProperty("Platform"); speed = settings.GetProperty("Speed");
        volume = settings.GetProperty("Volume"); gap = settings.GetProperty("GapCompensationSec");
        if (parser == null || platform == null || speed == null || volume == null || gap == null ||
            voiceInstance == null || prime == null || poll == null)
            throw new InvalidOperationException("需要兼容 Announce the Transaction 1.1.1 的版本");
        server = new LanServer();
        dependency = dep;
        active = this;
        voice.Patch(player.GetMethod("AnnounceAmount"), prefix: new HarmonyMethod(typeof(Bridge), nameof(RouteVoice)));
        var instance = player.GetProperty("Instance").GetValue(null, null);
        if (instance != null) player.GetMethod("StopAllAnnouncements").Invoke(instance, null);
        RefreshConnection();
        Debug.Log("[MobileCasher] " + status);
    }
    static bool RouteVoice(long amount, bool isTest) {
        var host = active;
        if (host == null) return true;
        try {
            if (amount > 0 && host.dependency.IsEnabled) host.Send(amount, isTest);
        } catch (Exception e) { Debug.LogError("[MobileCasher] " + e.GetBaseException().Message); }
        return false; // Replaces only the dependency's voice. Other game audio stays under game control.
    }
    internal static void PrimeSingleSale() {
        var host = active;
        if (host == null) return;
        try {
            var instance = host.voiceInstance.GetValue(null,null);
            var store = PlayerStore.Instance;
            if (instance != null && store != null) host.prime.Invoke(instance,new object[]{store,store.playerCash});
        } catch (Exception e) { Debug.LogWarning("[MobileCasher] " + e.GetBaseException().Message); }
    }
    internal static void FlushSingleSale(int cashBefore) {
        var host = active;
        if (host == null) return;
        try {
            var instance = host.voiceInstance.GetValue(null,null);
            if (instance != null) host.poll.Invoke(instance,new object[]{"SellItem",true,cashBefore});
        } catch (Exception e) { Debug.LogWarning("[MobileCasher] " + e.GetBaseException().Message); }
    }
    [Serializable] class Receipt {
        public string platform, name, portrait, time;
        public long amount;
        public string[] sounds;
        public float speed, volume, gap;
    }
    void Send(long amount, bool test) {
        object[] args = { amount, platform.GetValue(null,null), false };
        var sounds = (List<string>)parser.Invoke(null,args);
        var payer = test ? null : SaleScope.Current;
        string portrait = null;
        if (payer?.Sprite != null) try { portrait = Portrait.Encode(payer.Sprite); }
        catch (Exception e) { Debug.LogWarning("[MobileCasher] Portrait: " + e.Message); }
        var json = JsonUtility.ToJson(new Receipt {
            platform = (bool)args[2] ? "wechat" : "alipay", amount = amount,
            name = test ? "播报测试" : payer?.Name ?? "店铺入账", portrait = portrait,
            time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), sounds = sounds.ToArray(),
            speed = (float)speed.GetValue(null,null), volume = (float)volume.GetValue(null,null),
            gap = (float)gap.GetValue(null,null)
        });
        server.Publish(json);
    }
    internal void RefreshConnection() {
        if (server == null) return;
        if (connectionQr != null) UnityEngine.Object.Destroy(connectionQr);
        connectionQr = null;
        localUrl = null;
        try {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderByDescending(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .ThenByDescending(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet);
            string address = adapters.SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !a.Address.ToString().StartsWith("169.254.") && !a.Address.ToString().StartsWith("198.18."))
                .Select(a => a.Address.ToString()).FirstOrDefault();
            if (address == null) {
                status = "未找到局域网 IPv4 地址";
                return;
            }
            localUrl = "http://" + address + ":" + server.Port + "/";
            connectionQr = LocalQr.Create(localUrl);
            status = "手机连接同一局域网后扫码";
        } catch (Exception e) {
            status = "生成连接二维码失败：" + e.Message;
            Debug.LogWarning("[MobileCasher] " + status);
        }
    }
    internal void Shutdown() {
        if (active == this) { active = null; voice.UnpatchAll(voice.Id); }
        if (connectionQr != null) UnityEngine.Object.Destroy(connectionQr);
        connectionQr = null; localUrl = null;
        server?.Dispose(); server = null;
    }
    void OnDestroy() {
        if (Current == this) Current = null;
        Shutdown();
    }
}
}
