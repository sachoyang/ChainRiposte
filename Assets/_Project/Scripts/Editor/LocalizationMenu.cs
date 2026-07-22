using System.Collections.Generic;
using System.IO;
using ChainRiposte.Game.Localization;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 문구 테이블 생성/갱신과 한글 TMP 폰트 에셋 만들기.
    /// 실행: <c>Tools ▸ ChainRiposte ▸ Localization ▸ ...</c>
    /// </summary>
    public static class LocalizationMenu
    {
        private const string TableDirectory = "Assets/_Project/Data/Resources";
        private static string TablePath => $"{TableDirectory}/{Loc.ResourcePath}.asset";

        [MenuItem("Tools/ChainRiposte/Localization/Create or Update Table (문구 테이블)")]
        private static void CreateOrUpdateTable()
        {
            if (!Directory.Exists(TableDirectory))
            {
                Directory.CreateDirectory(TableDirectory);
                AssetDatabase.Refresh();
            }

            var table = AssetDatabase.LoadAssetAtPath<LocalizationTableSO>(TablePath);
            bool created = table == null;
            if (created)
            {
                table = ScriptableObject.CreateInstance<LocalizationTableSO>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            // 이미 있는 키는 건드리지 않는다 — 사용자가 고친 문구를 덮어쓰면 안 된다.
            var merged = new List<LocalizationTableSO.Entry>(table.EntriesEditorOnly);
            var existing = new HashSet<string>();
            foreach (LocalizationTableSO.Entry entry in merged)
                existing.Add(entry.key);

            int added = 0;
            foreach (LocalizationTableSO.Entry entry in DefaultEntries())
            {
                if (existing.Contains(entry.key))
                    continue;
                merged.Add(entry);
                added++;
            }

            table.SetEntriesEditorOnly(merged.ToArray());
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Localization] {(created ? "테이블 생성" : "테이블 갱신")} — 키 {added}개 추가, 총 {merged.Count}개. ({TablePath})", table);
            Selection.activeObject = table;
        }

        [MenuItem("Tools/ChainRiposte/Localization/Create TMP Font From Selected TTF (한글 폰트)")]
        private static void CreateTmpFont()
        {
            var font = Selection.activeObject as Font;
            if (font == null)
            {
                EditorUtility.DisplayDialog(
                    "폰트를 먼저 고르세요",
                    "프로젝트 창에서 .ttf / .otf 폰트를 선택한 뒤 다시 실행하세요.\n\n" +
                    "한글을 쓰려면 배포 가능한 폰트(나눔고딕·Pretendard 등 OFL)를 프로젝트에 넣고 쓰세요. " +
                    "Windows 기본 폰트(맑은 고딕 등)는 게임에 포함해 배포할 수 없습니다.",
                    "확인");
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(font);
            string outputPath = Path.ChangeExtension(sourcePath, null) + " SDF.asset";

            // 동적(Dynamic) 아틀라스 — 쓰이는 글리프만 실행 중에 채워 넣으므로 한글 전체를 미리 굽지 않아도 된다.
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError($"[Localization] '{font.name}' 으로 TMP 폰트 에셋을 만들지 못했습니다.");
                return;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(outputPath);
            AssetDatabase.CreateAsset(fontAsset, outputPath);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[Localization] TMP 폰트 에셋 생성: {outputPath}\n" +
                "TMP 텍스트의 Font Asset 슬롯에 꽂거나, TMP Settings의 기본 폰트로 지정하세요.", fontAsset);
            Selection.activeObject = fontAsset;
        }

        /// <summary>기본 문구 — 키가 없을 때만 채워진다.</summary>
        private static IEnumerable<LocalizationTableSO.Entry> DefaultEntries()
        {
            yield return Entry("title.continue", "이어하기", "CONTINUE");
            yield return Entry("title.newgame", "새 게임", "NEW GAME");
            yield return Entry("title.options", "옵션", "OPTIONS");
            yield return Entry("title.quit", "나가기", "QUIT");
            yield return Entry("title.newgame.confirm",
                "새로 시작하면 지금까지의 진행도가 모두 지워집니다. 계속할까요?",
                "Starting a new game erases all progress. Continue?");

            yield return Entry("options.title", "옵션", "OPTIONS");
            yield return Entry("options.bgm", "배경음", "MUSIC");
            yield return Entry("options.sfx", "효과음", "SFX");
            yield return Entry("options.orientation", "화면 방향", "SCREEN");
            yield return Entry("options.orientation.auto", "자동", "AUTO");
            yield return Entry("options.orientation.portrait", "세로 고정", "PORTRAIT");
            yield return Entry("options.orientation.landscape", "가로 고정", "LANDSCAPE");
            yield return Entry("options.language", "언어", "LANGUAGE");
            yield return Entry("options.reset", "진행도 초기화", "RESET PROGRESS");
            yield return Entry("options.reset.confirm",
                "저장된 진행도를 모두 지웁니다. 되돌릴 수 없습니다. 계속할까요?",
                "This erases all saved progress and cannot be undone. Continue?");

            yield return Entry("common.yes", "예", "YES");
            yield return Entry("common.no", "아니오", "NO");
            yield return Entry("common.back", "뒤로", "BACK");
        }

        private static LocalizationTableSO.Entry Entry(string key, string korean, string english) =>
            new() { key = key, korean = korean, english = english };
    }
}
