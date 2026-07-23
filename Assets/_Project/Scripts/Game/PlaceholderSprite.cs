using System.Collections.Generic;
using UnityEngine;

namespace ChainRiposte.Game
{
    /// <summary>에셋 없이 쓰는 단색 플레이스홀더 스프라이트. 에셋 단계에서 실제 스프라이트로 교체된다.</summary>
    public static class PlaceholderSprite
    {
        private const int RingResolution = 256;

        private static Sprite _square;
        private static Sprite _ring;
        // 두께가 다른 띠를 매 프레임 새로 굽지 않도록 비율별로 캐시한다.
        private static readonly Dictionary<int, Sprite> Annuli = new();

        /// <summary>
        /// 속이 빈 원(테두리). 다가오는 노트 원에 쓴다.
        /// 인스펙터에서 실제 아트로 갈아 끼울 수 있도록 여기서는 최소한의 절차적 생성만 한다.
        /// </summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring == null)
                    _ring = CreateRing(RingResolution, innerRatio: 0.88f);
                return _ring;
            }
        }

        /// <summary>
        /// 속이 빈 <b>띠</b>. 안쪽 반지름 비율(0~1)로 두께를 정한다 — 작을수록 두껍다.
        /// 패링 가능 구간처럼 "여기부터 여기까지"를 통째로 칠해야 할 때 쓴다.
        /// </summary>
        public static Sprite Annulus(float innerRatio)
        {
            // 0.01 단위로 반올림해 캐시 — 스탯이 바뀔 때만 새로 굽는다.
            int key = Mathf.Clamp(Mathf.RoundToInt(innerRatio * 100f), 0, 99);
            if (!Annuli.TryGetValue(key, out Sprite sprite) || sprite == null)
            {
                sprite = CreateRing(RingResolution, key / 100f);
                Annuli[key] = sprite;
            }

            return sprite;
        }

        /// <param name="innerRatio">안쪽 구멍의 반지름 / 바깥 반지름. 0이면 꽉 찬 원.</param>
        private static Sprite CreateRing(int size, float innerRatio)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float center = (size - 1) * 0.5f;
            float outer = center;
            float inner = outer * Mathf.Clamp01(innerRatio);
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
