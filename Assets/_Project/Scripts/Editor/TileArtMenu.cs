using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 타일 그림의 <b>여백을 잘라</b> 모든 타일이 셀을 같은 비율로 채우게 맞추는 툴.
    ///
    /// <para><b>왜 필요한가</b>: 보드는 그림의 <c>rect</c>를 기준으로 셀에 맞춘다
    /// (<c>TileView.ScaleToFit</c>). 그런데 그림마다 투명 여백이 제각각이라 —
    /// 쥐는 여백이 거의 없고 해골은 사방이 비어 있다 — 같은 규칙으로 맞춰도
    /// <b>쥐는 칸을 꽉 채우고 해골은 안쪽에 작게</b> 그려졌다.</para>
    ///
    /// <para>여백을 잘라 두면 <c>rect</c> = 보이는 그림이 되어, 그때부터는 크기 규칙이 정직해진다.
    /// 스프라이트 이름과 GUID는 그대로 두므로 <b>씬·에셋의 배선은 안 끊긴다.</b></para>
    ///
    /// <para>비율은 유지된다 — 가로로 넓은 그림은 넓은 채로, 긴 그림은 긴 채로 셀에 들어간다.
    /// 억지로 정사각형에 늘리면 도트가 뭉개진다.</para>
    /// </summary>
    public static class TileArtMenu
    {
        private const string TileFolder = "Assets/_Project/DotImgs/MonsterTile";

        /// <summary>이 값보다 옅은 픽셀은 없는 것으로 본다(도트 아트의 반투명 외곽 대비).</summary>
        private const byte AlphaThreshold = 8;

        [MenuItem("Tools/ChainRiposte/Art/Trim Tile Sprites To Content (여백 잘라 크기 통일)")]
        private static void TrimAll()
        {
            var report = new List<string>();
            var skipped = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TileFolder }))
                Trim(AssetDatabase.GUIDToAssetPath(guid), report, skipped);

            AssetDatabase.Refresh();
            Debug.Log($"[TileArt] 여백을 자른 그림 {report.Count}개 / 이미 딱 맞는 것 {skipped.Count}개\n"
                      + string.Join("\n", report.ToArray())
                      + (skipped.Count > 0 ? "\n(그대로: " + string.Join(", ", skipped.ToArray()) + ")" : string.Empty));
        }

        private static void Trim(string path, List<string> report, List<string> skipped)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            // ⚠ Single 모드는 조각의 rect 를 못 고친다. 억지로 Multiple 로 바꾸면 스프라이트의 GUID 가
            // 새로 생겨 <b>씬·에셋의 배선이 끊긴다</b> — 손대지 않고 건너뛴다.
            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                skipped.Add($"{System.IO.Path.GetFileName(path)}(Single 모드)");
                return;
            }

            // 픽셀은 <b>파일에서 직접</b> 읽는다. 예전에는 isReadable 을 켜고 SaveAndReimport 했는데,
            // 그 재임포트가 슬라이스 정보를 날려 4조각짜리 시트가 한 장으로 뭉개졌다.
            // 임포트 설정을 건드리지 않는 것이 유일하게 안전한 길이다.
            Texture2D texture = LoadFromFile(path);
            if (texture == null)
                return;

            try
            {
                Color32[] pixels = texture.GetPixels32();

                var factory = new SpriteDataProviderFactories();
                factory.Init();
                ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                if (provider == null)
                    return;

                provider.InitSpriteEditorDataProvider();
                SpriteRect[] rects = provider.GetSpriteRects();
                bool changed = false;

                foreach (SpriteRect sprite in rects)
                {
                    if (!TryFindContent(pixels, texture.width, sprite.rect, out Rect trimmed))
                    {
                        skipped.Add($"{sprite.name}(빈 그림)");
                        continue;
                    }

                    if (trimmed == sprite.rect)
                    {
                        skipped.Add(sprite.name);
                        continue;
                    }

                    report.Add($"  {sprite.name}: {sprite.rect.width}x{sprite.rect.height} → {trimmed.width}x{trimmed.height}");
                    sprite.rect = trimmed;
                    // 피벗은 가운데로 — 보드가 셀 한가운데에 놓으므로 그림도 가운데를 기준으로 잡아야 한다.
                    sprite.alignment = SpriteAlignment.Center;
                    sprite.pivot = new Vector2(0.5f, 0.5f);
                    changed = true;
                }

                if (!changed)
                    return;

                // 조각 <b>수</b>는 절대 안 바꾼다 — 이름과 GUID 를 그대로 두어야 배선이 살아 있다.
                provider.SetSpriteRects(rects);
                provider.Apply();
                importer.SaveAndReimport();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// 임포트 설정을 건드리지 않고 픽셀을 읽는다 — PNG 파일을 그대로 디코드해 임시 텍스처로 만든다.
        /// (<c>AssetDatabase</c>가 준 텍스처는 <c>isReadable</c>이 꺼져 있으면 못 읽는다.)
        /// </summary>
        private static Texture2D LoadFromFile(string path)
        {
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            if (texture.LoadImage(bytes))
                return texture;

            Object.DestroyImmediate(texture);
            Debug.LogWarning($"[TileArt] {path} 를 읽지 못했습니다 (PNG가 아닐 수 있습니다).");
            return null;
        }

        /// <summary>
        /// 그 조각 안에서 <b>실제로 보이는 픽셀</b>의 사각형을 찾는다. 전부 투명이면 false.
        /// </summary>
        private static bool TryFindContent(Color32[] pixels, int textureWidth, Rect source, out Rect content)
        {
            int x0 = Mathf.RoundToInt(source.x);
            int y0 = Mathf.RoundToInt(source.y);
            int width = Mathf.RoundToInt(source.width);
            int height = Mathf.RoundToInt(source.height);

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[(y0 + y) * textureWidth + x0 + x].a <= AlphaThreshold)
                        continue;

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX)
            {
                content = source;
                return false;
            }

            content = new Rect(x0 + minX, y0 + minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }
    }
}
