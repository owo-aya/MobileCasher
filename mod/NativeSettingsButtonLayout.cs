using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MobileCasher {
[HarmonyPatch(typeof(ModUIManager), "ShowMod")]
internal static class NativeSettingsButtonLayout {
    static void Postfix(LoadedMod mod, CustomUIWindow ___handle) {
        if (mod?.Manifest.ID != ModEntry.ModId || ___handle == null) return;
        var settings = ___handle.Get("settings");
        if (settings == null || !settings.IsAlive) return;
        settings.SetFixedWidth(42).SetPreferredSize(42, 42);
        var buttonLayout = settings.root.GetComponent<LayoutElement>();
        buttonLayout.minHeight = 42;
        buttonLayout.flexibleHeight = 0;
        if (settings.image != null) settings.image.preserveAspect = true;

        var author = ___handle.Get("detail_author");
        if (author == null || !author.IsAlive) return;
        var details = author.root.transform.parent.gameObject;
        var textLayout = details.GetComponent<LayoutElement>();
        if (textLayout == null) textLayout = details.AddComponent<LayoutElement>();
        textLayout.minWidth = 0;
        textLayout.preferredWidth = 0;
        textLayout.flexibleWidth = 1;
    }
}
}
