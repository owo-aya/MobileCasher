using System;
using UnityEngine;

namespace MobileCasher {
internal static class Portrait {
    // The game textures need not be CPU-readable. Read the sprite region, not the entire atlas.
    public static string Encode(Sprite sprite) {
        Rect rect = sprite.textureRect;
        var texture = sprite.texture;
        var target = RenderTexture.GetTemporary(128, Mathf.Clamp(Mathf.RoundToInt(128 * rect.height / rect.width), 1, 256), 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        Texture2D sample = null, head = null;
        try {
            Graphics.Blit(texture, target, new Vector2(rect.width / texture.width, rect.height / texture.height),
                new Vector2(rect.x / texture.width, rect.y / texture.height));
            RenderTexture.active = target;
            sample = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            sample.ReadPixels(new Rect(0,0,target.width,target.height),0,0);
            sample.Apply();
            var pixels = sample.GetPixels32();
            int left = target.width, right = -1, top = -1, bottom = target.height;
            for (int y=0;y<target.height;y++) for (int x=0;x<target.width;x++) {
                if (pixels[y*target.width+x].a < 40) continue;
                left=Math.Min(left,x); right=Math.Max(right,x);
                top=Math.Max(top,y); bottom=Math.Min(bottom,y);
            }
            if (right < left) return null;
            int size = Mathf.Clamp(Mathf.RoundToInt((top-bottom+1)*0.72f),1,Math.Min(target.width,target.height));
            int x0 = Mathf.Clamp((left+right-size+1)/2,0,target.width-size);
            int y0 = Mathf.Clamp(top-size+1+size/16,0,target.height-size);
            head = new Texture2D(size,size,TextureFormat.RGBA32,false);
            head.SetPixels(sample.GetPixels(x0,y0,size,size)); head.Apply();
            return "data:image/png;base64," + Convert.ToBase64String(head.EncodeToPNG());
        } finally {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            if (sample != null) UnityEngine.Object.Destroy(sample);
            if (head != null) UnityEngine.Object.Destroy(head);
        }
    }
}
}
