using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MobileCasher {
[HarmonyPatch(typeof(ModConfig), nameof(ModConfig.HasSettings))]
internal static class NativeConnectionEntry {
    static void Postfix(string modId, ref bool __result) {
        if (modId == ModEntry.ModId) __result = true;
    }
}

[HarmonyPatch(typeof(ModConfigWindow), nameof(ModConfigWindow.Open))]
internal static class NativeConnectionSection {
    static bool Prefix(LoadedMod mod, ref CustomUIWindow __result) {
        if (mod?.Manifest.ID != ModEntry.ModId) return true;
        var manager = CustomUIManager.Instance;
        if (manager == null) return false;
        bool inStore = StoreUIManager.Instance != null;
        var b = manager.CreateWindow("mod_config:" + ModEntry.ModId, mod.Manifest.Name,
            inStore ? "meta" : "menu", manager.optionUIPrefab).SetCloseOnEscape(!inStore);
        b.BeginScrollFlexible();
        b.BeginRow(4, TextAnchor.MiddleLeft);
        b.AddHeader("局域网连接");
        b.End();
        b.AddSeparator(null, 2);
        b.AddSpace(12);
        b.BeginRow(20, TextAnchor.MiddleLeft);
        var view = b.CurrentContainer.gameObject.AddComponent<NativeConnectionView>();
        b.AddImage((Sprite)null, 222, 222, "phone_qr");
        var image = b.Last;
        image.SetFixedWidth(222).SetImageColor(Color.white);
        image.image.type = Image.Type.Simple;
        image.image.preserveAspect = true;
        image.image.raycastTarget = false;
        image.image.material = null;
        b.BeginColumn(8, TextAnchor.MiddleLeft);
        b.AddLabel("", "phone_connection_status");
        var status = b.Last;
        b.AddLabel("", "phone_connection_url");
        var address = b.Last;
        b.AddButton("刷新连接", () => Bridge.Current?.RefreshConnection(), "phone_refresh");
        b.Last.SetFixedWidth(220);
        b.End();
        b.End();
        view.Bind(image, status, address);
        b.End();
        __result = b.Show();
        return false;
    }
}

public sealed class NativeConnectionView : MonoBehaviour {
    CustomUIElement qrElement, statusElement, addressElement;
    Texture2D lastTexture;
    Sprite qrSprite;
    string lastStatus, lastAddress;

    internal void Bind(CustomUIElement image, CustomUIElement status, CustomUIElement address) {
        qrElement = image;
        statusElement = status;
        addressElement = address;
        Update();
    }
    void Update() {
        if (qrElement == null) return;
        var bridge = Bridge.Current;
        var texture = bridge?.QrTexture;
        if (!ReferenceEquals(lastTexture, texture)) {
            if (qrSprite != null) UnityEngine.Object.Destroy(qrSprite);
            lastTexture = texture;
            qrSprite = texture == null ? null : Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100);
            qrElement.SetSprite(qrSprite);
        }
        qrElement.SetVisible(texture != null);
        string status = bridge?.ConnectionStatus ?? "手机收款未启用";
        if (status != lastStatus) { statusElement.SetText(status); lastStatus = status; }
        string address = bridge?.ConnectionUrl ?? "";
        if (address != lastAddress) { addressElement.SetText(address); lastAddress = address; }
    }
    void OnDestroy() {
        if (qrSprite != null) UnityEngine.Object.Destroy(qrSprite);
    }
}
}
