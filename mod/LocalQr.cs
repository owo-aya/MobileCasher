using Net.Codecrete.QrCodeGenerator;
using UnityEngine;

namespace MobileCasher {
internal static class LocalQr {
    internal static Texture2D Create(string url) {
        var code = QrCode.EncodeText(url, QrCode.Ecc.Medium);
        int size = code.Size + 8;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
            bool dark = code.GetModule(x - 4, size - 1 - y - 4);
            pixels[y * size + x] = dark ? new Color32(0,0,0,255) : new Color32(255,255,255,255);
        }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }
}
}
