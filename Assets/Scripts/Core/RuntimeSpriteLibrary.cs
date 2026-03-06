using UnityEngine;

namespace Snow2
{
    public static class RuntimeSpriteLibrary
    {
        private static Sprite _block;
        private static Sprite _circle;

        // 原型阶段：优先从 Resources 加载素材；必要时允许运行时生成（例如圆形雪球）。
        public static Sprite WhiteSprite
        {
            get
            {
                if (_block != null)
                {
                    return _block;
                }

                _block = Resources.Load<Sprite>("Block16");
                return _block;
            }
        }

        public static Sprite CircleSprite
        {
            get
            {
                if (_circle != null)
                {
                    return _circle;
                }

                // 生成一个透明背景的白色圆形 Sprite（用于雪球外观）。
                // 为避免额外资源文件，本项目在运行时生成。
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                var cx = (size - 1) * 0.5f;
                var cy = (size - 1) * 0.5f;
                var radius = (size - 2) * 0.5f;
                var edgeSoftness = 1.25f; // 边缘柔化（抗锯齿）

                var pixels = new Color32[size * size];
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var dx = x - cx;
                        var dy = y - cy;
                        var d = Mathf.Sqrt(dx * dx + dy * dy);
                        var a = Mathf.Clamp01((radius - d) / edgeSoftness);
                        var idx = y * size + x;
                        pixels[idx] = new Color(1f, 1f, 1f, a);
                    }
                }

                tex.SetPixels32(pixels);
                tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

                _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit: 64f);
                return _circle;
            }
        }
    }
}
