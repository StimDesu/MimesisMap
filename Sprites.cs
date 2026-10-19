using UnityEngine;

namespace MimesisMap
{
    internal static class Sprites
    {
        private static Sprite _circle, _ring, _frame;

        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeCircle(256, 0f));
        public static Sprite Ring => _ring != null ? _ring : (_ring = MakeCircle(256, 0.035f));
        public static Sprite Frame => _frame != null ? _frame : (_frame = MakeFrame());

        private static Sprite MakeCircle(int size, float ringWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            float rw = ringWidth * size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float outer = Mathf.Clamp01(r - d);              // antialiased outer edge
                    float a = outer;
                    if (rw > 0f) a *= Mathf.Clamp01(d - (r - rw));    // ring: cut inner part
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            Object.DontDestroyOnLoad(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            Object.DontDestroyOnLoad(s);
            return s;
        }

        private static Sprite MakeFrame()
        {
            const int size = 32, b = 3;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < b || y < b || x >= size - b || y >= size - b;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(edge ? 255 : 0));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            Object.DontDestroyOnLoad(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b + 1, b + 1, b + 1, b + 1));
            Object.DontDestroyOnLoad(s);
            return s;
        }
    }
}
