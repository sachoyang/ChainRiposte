using System.Collections;
using System.Collections.Generic;
using System.Text;
using ChainRiposte.Game.Localization;
using TMPro;
using UnityEngine;

namespace ChainRiposte.Game.Flow
{
    /// <summary>
    /// 게임이 <b>런타임에 굽느라 끊기는 것들</b>을 시작할 때 몰아서 구워 둔다.
    ///
    /// <para>여기 있는 것들은 전부 "언젠가는 어차피 만들어지는 것"이다. 미리 굽는다고 메모리가
    /// 더 드는 게 아니라 <b>만들어지는 시점만</b> 옮기는 것이다 — 플레이 도중이 아니라 로딩 중으로.</para>
    ///
    /// <para><b>빗나가도 무해하다</b>는 것이 이 클래스의 성질이다. 캐시를 데워 두는 일이라
    /// 잘못 짚으면 그냥 도움이 안 될 뿐 고장 나지 않는다. 그래서 아래 수치들이
    /// 원본과 느슨하게 묶여 있어도 된다 — 어긋나면 성능이 예전으로 돌아갈 뿐이다.</para>
    /// </summary>
    public static class Prewarmer
    {
        /// <summary>
        /// 한 프레임에 아틀라스로 밀어 넣을 글자 수. 통째로 넣으면 그 프레임이 통째로 멎는다.
        ///
        /// <para>지금 구울 글자는 <b>440자 안팎</b>이다(CSV 고유 글자 343 — 그중 한글 274 — 에
        /// <see cref="AlwaysNeeded"/>를 더한 값). 48이면 열 번 남짓으로 쪼개진다.</para>
        ///
        /// <para>이 값은 <b>총 시간이 아니라 한 프레임의 무게</b>를 정한다. 굽는 동안 인트로 로고가
        /// 돌고 있으므로, 크게 잡아 빨리 끝내면 그만큼 로고가 툭툭 끊긴다.</para>
        /// </summary>
        private const int GlyphBatch = 48;

        /// <summary>포맷 인자({0})가 만들어 내는 글자들 — CSV 본문에는 안 보이지만 화면에는 나온다.</summary>
        private const string AlwaysNeeded =
            "0123456789" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
            "abcdefghijklmnopqrstuvwxyz" +
            " .,:;!?'\"()[]{}<>/\\|-+*=%#@&~^_`" +
            "×÷…—–·「」『』〈〉";

        public static bool Done { get; private set; }

        /// <summary>
        /// 전부 굽는다. <paramref name="onProgress"/>는 0~1 진행률과 지금 하는 일의 현지화 키를 받는다.
        /// </summary>
        public static IEnumerator Run(System.Action<float, string> onProgress)
        {
            if (Done)
            {
                onProgress?.Invoke(1f, "loading.done");
                yield break;
            }

            // ── 1) 문구 표 파싱 ──
            onProgress?.Invoke(0f, "loading.text");
            Loc.EnsureInit();
            yield return null;

            // ── 2) 폰트 글리프 ──
            yield return BakeGlyphs(onProgress);

            // ── 3) 노트 원 ──
            yield return BakeRings(onProgress);

            Done = true;
            onProgress?.Invoke(1f, "loading.done");
        }

        /// <summary>
        /// CSV에 실린 모든 글자를 아틀라스에 밀어 넣는다.
        ///
        /// <para>폰트가 <b>동적 아틀라스</b>이고 빌드할 때 비워져 나가므로, 기기에서는 글자가
        /// 처음 뜨는 순간마다 SDF를 굽는다. 한글은 글자 수가 많아 화면을 넘길 때마다 걸린다.</para>
        ///
        /// <para>필요한 글자를 <b>정확히</b> 알 수 있는 것은 화면 글씨의 원천이
        /// <c>Localization.csv</c> 한 장뿐이기 때문이다. 원천을 하나로 묶어 둔 값이 여기서 나온다.</para>
        /// </summary>
        private static IEnumerator BakeGlyphs(System.Action<float, string> onProgress)
        {
            onProgress?.Invoke(0.05f, "loading.font");

            string characters = CollectCharacters();
            List<TMP_FontAsset> fonts = CollectFonts();

            if (characters.Length == 0 || fonts.Count == 0)
            {
                Debug.LogWarning("[Prewarm] 구울 글자나 폰트를 못 찾았습니다 — 건너뜁니다.");
                yield break;
            }

            int total = Mathf.CeilToInt(characters.Length / (float)GlyphBatch) * fonts.Count;
            int done = 0;

            foreach (TMP_FontAsset font in fonts)
            {
                for (int i = 0; i < characters.Length; i += GlyphBatch)
                {
                    int length = Mathf.Min(GlyphBatch, characters.Length - i);
                    font.TryAddCharacters(characters.Substring(i, length), out string _);

                    done++;
                    onProgress?.Invoke(Mathf.Lerp(0.05f, 0.85f, done / (float)total), "loading.font");
                    yield return null;
                }
            }
        }

        /// <summary>CSV의 모든 문구 + 항상 필요한 글자를 <b>중복 없이</b> 모은다.</summary>
        private static string CollectCharacters()
        {
            var seen = new HashSet<char>();
            var builder = new StringBuilder();

            void Take(string source)
            {
                if (string.IsNullOrEmpty(source))
                    return;

                foreach (char c in source)
                {
                    // 줄바꿈·탭은 글리프가 아니다. CSV의 "\n"은 두 글자(\ 와 n)로 들어 있는데
                    // 그 둘은 AlwaysNeeded에 이미 있으므로 따로 챙길 것이 없다.
                    if (char.IsControl(c) || !seen.Add(c))
                        continue;

                    builder.Append(c);
                }
            }

            Take(AlwaysNeeded);
            foreach (string value in Loc.AllDisplayValues())
                Take(value);

            return builder.ToString();
        }

        /// <summary>
        /// 기본 폰트 + 폴백 전부. 폴백까지 굽는 이유는 기본 폰트에 없는 글자가
        /// <b>폴백에서 처음 발견될 때</b> 똑같이 끊기기 때문이다.
        /// </summary>
        private static List<TMP_FontAsset> CollectFonts()
        {
            var fonts = new List<TMP_FontAsset>();

            void Add(TMP_FontAsset font)
            {
                if (font != null && !fonts.Contains(font))
                    fonts.Add(font);
            }

            Add(TMP_Settings.defaultFontAsset);

            if (TMP_Settings.fallbackFontAssets != null)
                foreach (TMP_FontAsset font in TMP_Settings.fallbackFontAssets)
                    Add(font);

            if (TMP_Settings.defaultFontAsset != null &&
                TMP_Settings.defaultFontAsset.fallbackFontAssetTable != null)
            {
                foreach (TMP_FontAsset font in TMP_Settings.defaultFontAsset.fallbackFontAssetTable)
                    Add(font);
            }

            return fonts;
        }

        /// <summary>
        /// 전투 노트 원. <see cref="PlaceholderSprite.Annulus"/>가 비율마다 256×256 텍스처를
        /// CPU 루프로 굽는데, 그 일이 <b>전투 첫 2초에 몰린다</b>.
        ///
        /// <para>비율 범위는 <c>CombatScreen.ApplyRingGeometry</c>에서 온다:
        /// <c>r / (r + 두께)</c>이고 r은 1(몸)에서 <c>maxVisibleScale</c>(화면 밖)까지다.
        /// 두께는 판정 폭에서 나오므로 결국 <b>0.85~0.95 언저리</b>의 좁은 구간에만 있다.</para>
        ///
        /// <para>여기 적힌 범위가 실제와 어긋나도 <b>고장은 안 난다</b> — 안 쓰이는 그림을 몇 장
        /// 구웠거나, 못 구운 것을 전투 중에 굽게 될 뿐이다. 그래서 굳이 CombatScreen에서
        /// 값을 끌어오는 배선을 만들지 않았다(그 배선이 오히려 깨지기 쉽다).</para>
        /// </summary>
        private static IEnumerator BakeRings(System.Action<float, string> onProgress)
        {
            onProgress?.Invoke(0.85f, "loading.combat");

            const float min = 0.84f;
            const float max = 0.96f;
            const float step = 0.01f;

            int total = Mathf.RoundToInt((max - min) / step) + 1;

            for (int i = 0; i < total; i++)
            {
                PlaceholderSprite.Annulus(min + i * step);
                onProgress?.Invoke(Mathf.Lerp(0.85f, 1f, (i + 1) / (float)total), "loading.combat");
                yield return null;
            }
        }
    }
}
