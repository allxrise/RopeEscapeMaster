using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RopeEscape.Utils
{
    /// <summary>
    /// Loads and caches original APK UI sprites from Assets/Resources/UI/*.png.
    /// Works seamlessly whether Unity imports the PNGs as Texture2D or Sprite.
    /// </summary>
    public static class UISpriteLoader
    {
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
        private static Sprite _fallbackWhiteSprite;

        public static Sprite LoadSprite(string name, Vector4 border = default)
        {
            if (string.IsNullOrEmpty(name))
                return GetFallbackSprite();

            if (_spriteCache.TryGetValue(name, out Sprite cached) && cached != null)
                return cached;

            // 1. Try loading directly as Sprite from Resources/UI/
            Sprite sprite = Resources.Load<Sprite>($"UI/{name}");
            if (sprite != null)
            {
                _spriteCache[name] = sprite;
                return sprite;
            }

            // 2. Try loading as Texture2D from Resources/UI/
            Texture2D tex = Resources.Load<Texture2D>($"UI/{name}");
            if (tex == null)
            {
                // 3. Fallback: load raw PNG bytes from Assets/Resources/UI/ in Editor/Standalone if not yet indexed
                string diskPath = Path.Combine(Application.dataPath, "Resources", "UI", $"{name}.png");
                if (File.Exists(diskPath))
                {
                    byte[] bytes = File.ReadAllBytes(diskPath);
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = name };
                    tex.LoadImage(bytes);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                }
            }

            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                sprite = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    border);
                sprite.name = name;
                _spriteCache[name] = sprite;
                return sprite;
            }

            return GetFallbackSprite();
        }

        public static Sprite GetFallbackSprite()
        {
            if (_fallbackWhiteSprite != null)
                return _fallbackWhiteSprite;

            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] cols = new Color[16];
            for (int i = 0; i < 16; i++) cols[i] = Color.white;
            tex.SetPixels(cols);
            tex.Apply();
            _fallbackWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            return _fallbackWhiteSprite;
        }
    }
}
