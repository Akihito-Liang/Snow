using UnityEngine;

namespace Snow2
{
    public static class RuntimeSpriteLibrary
    {
        private static Sprite _block;
        private static Sprite _circle;
        private static Sprite _triangle;
        private static Sprite _heart;

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

        public static Sprite TriangleSprite
        {
            get
            {
                if (_triangle != null)
                {
                    return _triangle;
                }

                // 生成一个透明背景的白色三角形 Sprite（用于药水/道具占位外观）。
                // 为避免额外资源文件，本项目在运行时生成。
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                // 等腰三角形顶点（纹理像素坐标）
                var a = new Vector2(size * 0.5f, size - 4f);
                var b = new Vector2(6f, 8f);
                var c = new Vector2(size - 6f, 8f);

                var pixels = new Color32[size * size];
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        var inside = PointInTriangle(p, a, b, c);
                        var idx = y * size + x;
                        pixels[idx] = inside ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0f);
                    }
                }

                tex.SetPixels32(pixels);
                tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

                _triangle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit: 64f);
                return _triangle;
            }
        }

        public static Sprite HeartSprite
        {
            get
            {
                if (_heart != null)
                {
                    return _heart;
                }

                // 生成一个透明背景的白色爱心 Sprite（用于 HP UI）。
                // 为避免额外资源文件，本项目在运行时生成。
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                // 使用经典心形隐式函数：(x^2 + y^2 - 1)^3 - x^2 y^3 <= 0
                // 通过 2x2 supersampling 做简单抗锯齿。
                var pixels = new Color32[size * size];
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        float coverage = 0f;
                        for (var sy = 0; sy < 2; sy++)
                        {
                            for (var sx = 0; sx < 2; sx++)
                            {
                                var fx = (x + (sx + 0.25f) * 0.5f) / (size - 1f);
                                var fy = (y + (sy + 0.25f) * 0.5f) / (size - 1f);

                                // 映射到 [-1.25, 1.25]，并稍微把心形往上移一点，让底部更尖。
                                var xx = (fx - 0.5f) * 2.5f;
                                var yy = (fy - 0.42f) * 2.5f;

                                var a = xx * xx + yy * yy - 1f;
                                var f = a * a * a - (xx * xx) * (yy * yy * yy);
                                if (f <= 0f)
                                {
                                    coverage += 1f;
                                }
                            }
                        }
                        coverage *= 0.25f;

                        var idx = y * size + x;
                        pixels[idx] = new Color(1f, 1f, 1f, Mathf.Clamp01(coverage));
                    }
                }

                tex.SetPixels32(pixels);
                tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

                _heart = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit: 64f);
                return _heart;
            }
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            // 叉乘同向法判断点在三角形内（含边界）
            var ab = b - a;
            var bc = c - b;
            var ca = a - c;

            var ap = p - a;
            var bp = p - b;
            var cp = p - c;

            var c1 = Cross(ab, ap);
            var c2 = Cross(bc, bp);
            var c3 = Cross(ca, cp);

            var hasNeg = (c1 < 0f) || (c2 < 0f) || (c3 < 0f);
            var hasPos = (c1 > 0f) || (c2 > 0f) || (c3 > 0f);
            return !(hasNeg && hasPos);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}
