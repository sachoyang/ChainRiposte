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
        private static Sprite _slash;
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
            int key = RatioKey(innerRatio);
            if (!Annuli.TryGetValue(key, out Sprite sprite) || sprite == null)
            {
                sprite = CreateRing(RingResolution, key / 100f);
                Annuli[key] = sprite;
            }

            return sprite;
        }

        /// <summary>
        /// <see cref="Annulus"/>가 실제로 구워 주는 비율. 캐시가 0.01 단위로 반올림하므로,
        /// <b>그림의 두께를 계산에 쓰는 쪽</b>은 원하는 값이 아니라 이 값을 써야 보이는 것과 판정이 어긋나지 않는다.
        /// </summary>
        public static float QuantizeRatio(float innerRatio) => RatioKey(innerRatio) / 100f;

        private static int RatioKey(float innerRatio) =>
            Mathf.Clamp(Mathf.RoundToInt(innerRatio * 100f), 0, 99);

        /// <summary>
        /// 검기 띠 — <b>가운데가 굵고 양 끝이 뾰족하게 모이는 눈(렌즈) 모양</b>.
        ///
        /// <para>사각형을 늘려 쓰면 끝이 일자로 뚝 끊겨 "그어진 자국"이 아니라 "막대"로 보인다.
        /// 프로파일을 <c>(1 − x²)^p</c> 로 잡는 것이 요점인데, 이 곡선은 끝점에서 <b>기울기가 유한</b>해
        /// 뾰족하게 모인다. 원/타원(<c>√(1 − x²)</c>)을 쓰면 끝에서 접선이 수직이라 오히려 뭉툭해진다.</para>
        ///
        /// <para>가로로 누운 그림이다 — 방향은 <c>SlashView</c>가 회전으로 맞춘다.</para>
        /// </summary>
        public static Sprite Slash
        {
            get
            {
                if (_slash == null)
                    _slash = CreateSlash(width: 256, height: 64, tipSharpness: 1.25f);
                return _slash;
            }
        }

        private static Sprite CreateSlash(int width, int height, float tipSharpness)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[width * height];
            float halfHeight = (height - 1) * 0.5f;

            for (int x = 0; x < width; x++)
            {
                // 가로 위치를 -1..1 로. 이 자리에서의 반두께가 곧 렌즈의 윤곽이다.
                float nx = (x / (float)(width - 1)) * 2f - 1f;
                float profile = Mathf.Pow(Mathf.Max(0f, 1f - nx * nx), tipSharpness);
                float reach = profile * halfHeight;

                for (int y = 0; y < height; y++)
                {
                    float dy = Mathf.Abs(y - halfHeight);
                    // 경계를 1픽셀 폭으로 부드럽게 — 얇아지는 끝에서 계단이 제일 잘 보인다
                    float alpha = Mathf.Clamp01(reach - dy);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false);

            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
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

        /// <summary>
        /// <b>왼쪽 위·오른쪽 아래 모서리를 대각선으로 잘라 낸</b> 평행사변형 — 엔딩 영상의 마스크다.
        /// 화면을 비스듬히 가로지르는 유리 조각처럼 보이게 하는 것이 목적이고, 덤으로
        /// <b>오른쪽 아래에 박힌 워터마크가 잘려 나간다.</b>
        ///
        /// <para>자르는 길이는 <paramref name="cutRatio"/> × <b>가로</b>이고, 세로로도 <b>같은 픽셀</b>만큼
        /// 자른다 — 그래야 가로·세로 화면에서 기울기(45°)가 같아 보인다. 비율로 자르면 세로 화면에서
        /// 훨씬 눕고 가로에서 훨씬 서서, 같은 연출이 다른 모양이 된다.</para>
        ///
        /// <para>마스크로 쓰는 그림이라 <b>비율(aspect)마다 다른 텍스처</b>가 필요하다 — 늘려 쓰면
        /// 잘린 각도까지 같이 늘어난다.</para>
        /// </summary>
        /// <param name="aspect">가로 ÷ 세로. 영상 클립의 비율을 그대로 넣는다.</param>
        /// <param name="cutRatio">가로의 몇 할을 자를지(0.2 = 20%). 0이면 그냥 사각형.</param>
        public static Sprite SlantedScreen(float aspect, float cutRatio)
        {
            const int width = 512;
            int height = Mathf.Clamp(Mathf.RoundToInt(width / Mathf.Max(0.05f, aspect)), 16, 2048);

            // 자를 길이는 가로 기준. 양쪽 잘림이 서로 만나면 도형이 끊기므로 가로·세로의 절반으로 묶는다.
            float cut = Mathf.Min(width * Mathf.Clamp01(cutRatio), width * 0.5f, height * 0.5f);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 왼쪽 위 잘림: x + (높이−y) 가 작을수록 모서리에 가깝다. 오른쪽 아래는 그 반대.
                    float topLeft = x + (height - 1 - y) - cut;
                    float bottomRight = (width - 1 - x) + y - cut;

                    // 경계를 1픽셀로 부드럽게 — 대각선은 계단이 가장 잘 보이는 모양이다
                    float alpha = Mathf.Clamp01(topLeft) * Mathf.Clamp01(bottomRight);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false);

            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
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
