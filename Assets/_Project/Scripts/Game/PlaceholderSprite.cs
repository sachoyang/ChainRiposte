using UnityEngine;

namespace ChainRiposte.Game
{
    /// <summary>에셋 없이 쓰는 단색 플레이스홀더 스프라이트. 에셋 단계에서 실제 스프라이트로 교체된다.</summary>
    public static class PlaceholderSprite
    {
        private const int RingResolution = 256;

        private static Sprite _square;
        private static Sprite _ring;

        /// <summary>
        /// 속이 빈 원(테두리). 전투의 패링 타이밍 원에 쓴다.
        /// 인스펙터에서 실제 아트로 갈아 끼울 수 있도록 여기서는 최소한의 절차적 생성만 한다.
        /// </summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring == null)
                    _ring = CreateRing(RingResolution, thicknessRatio: 0.06f);
                return _ring;
            }
        }

        private static Sprite CreateRing(int size, float thicknessRatio)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float center = (size - 1) * 0.5f;
            float outer = center;
            float inner = outer * (1f - thicknessRatio * 2f);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // 테두리 안쪽/바깥쪽 경계를 1픽셀 폭으로 부드럽게 — 계단현상 방지
                    float alpha = Mathf.Clamp01(outer - distance) * Mathf.Clamp01(distance - inner);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false);

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static Sprite Square
        {
            get
            {
                if (_square == null)
                {
                    Texture2D tex = Texture2D.whiteTexture; // 4x4 흰색
                    _square = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f), pixelsPerUnit: tex.width);
                }

                return _square;
            }
        }
    }
}
