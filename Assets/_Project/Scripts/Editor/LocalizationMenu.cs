using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ChainRiposte.Game.Localization;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 구글 시트 ↔ CSV 파이프라인과 현지화 점검 도구.
    /// 실행: <c>Tools ▸ ChainRiposte ▸ Localization ▸ ...</c>
    ///
    /// <b>런타임은 시트를 치지 않는다.</b> 여기서 시트를 CSV로 구워 git에 커밋하고,
    /// 게임은 <c>Resources/Localization.csv</c> 만 읽는다 — 오프라인·WebGL(CORS)·시트 오조작에도 텍스트가 안전하다.
    /// </summary>
    public static class LocalizationMenu
    {
        private const string CsvDirectory = "Assets/_Project/Data/Resources";
        private const string SheetIdPrefsKey = "ChainRiposte.Localization.SheetId";
        private const string GidPrefsKey = "ChainRiposte.Localization.Gid";
        private const string KeysOutputPath = "Assets/_Project/Scripts/Game/Localization/LocKeys.cs";

        private static string CsvPath => $"{CsvDirectory}/{Loc.CsvResourceName}.csv";

        // ── 시트 동기화 ────────────────────────────────────────────────

        [MenuItem("Tools/ChainRiposte/Localization/Set Google Sheet (시트 지정)")]
        private static void SetSheet()
        {
            string current = EditorPrefs.GetString(SheetIdPrefsKey, string.Empty);
            string input = EditorInputDialog.Show(
                "구글 시트 지정",
                "시트 편집 URL 또는 시트 ID를 붙여넣으세요.\n" +
                "시트는 '링크가 있는 모든 사용자 · 뷰어'로 공유되어 있어야 합니다.",
                current);

            if (input == null)
                return;

            (string sheetId, string gid) = ParseSheetUrl(input);
            if (string.IsNullOrEmpty(sheetId))
            {
                EditorUtility.DisplayDialog("시트 지정", "시트 ID를 읽지 못했습니다. 편집 URL 전체를 붙여넣어 보세요.", "확인");
                return;
            }

            EditorPrefs.SetString(SheetIdPrefsKey, sheetId);
            EditorPrefs.SetString(GidPrefsKey, gid ?? string.Empty);
            Debug.Log($"[Loc] 시트 지정 완료. id={sheetId}" + (string.IsNullOrEmpty(gid) ? "" : $", gid={gid}"));
        }

        [MenuItem("Tools/ChainRiposte/Localization/Sync From Google Sheet (시트 → CSV)")]
        private static void SyncFromSheet()
        {
            string sheetId = EditorPrefs.GetString(SheetIdPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(sheetId))
            {
                EditorUtility.DisplayDialog("시트 동기화", "먼저 'Set Google Sheet' 로 시트를 지정하세요.", "확인");
                return;
            }

            string gid = EditorPrefs.GetString(GidPrefsKey, string.Empty);
            string url = $"https://docs.google.com/spreadsheets/d/{sheetId}/export?format=csv" +
                         (string.IsNullOrEmpty(gid) ? string.Empty : $"&gid={gid}");

            if (!TryDownload(url, out string csv))
                return;

            // 공유가 안 걸려 있으면 구글이 CSV 대신 로그인 HTML을 준다 — 멀쩡한 CSV를 덮어쓰기 전에 막는다.
            if (!Validate(csv, out string reason, out int rowCount, out List<string> languages))
            {
                EditorUtility.DisplayDialog(
                    "시트 동기화 실패",
                    $"받은 내용이 올바른 현지화 CSV가 아닙니다.\n\n{reason}\n\n" +
                    "기존 CSV는 그대로 두었습니다. 시트 공유 설정이 '링크가 있는 모든 사용자 · 뷰어' 인지 확인하세요.",
                    "확인");
                return;
            }

            Directory.CreateDirectory(CsvDirectory);
            File.WriteAllText(CsvPath, csv, new UTF8Encoding(false)); // BOM 없는 UTF-8
            AssetDatabase.Refresh();
            Loc.Reload();

            Debug.Log($"[Loc] 시트 → CSV 동기화 완료: {rowCount}개 키 / 언어 {string.Join(", ", languages)}\n{CsvPath}");
        }

        [MenuItem("Tools/ChainRiposte/Localization/Open Sheet In Browser (시트 열기)")]
        private static void OpenSheet()
        {
            string sheetId = EditorPrefs.GetString(SheetIdPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(sheetId))
            {
                EditorUtility.DisplayDialog("시트 열기", "먼저 'Set Google Sheet' 로 시트를 지정하세요.", "확인");
                return;
            }

            Application.OpenURL($"https://docs.google.com/spreadsheets/d/{sheetId}/edit");
        }

        [MenuItem("Tools/ChainRiposte/Localization/Create Starter CSV (시작용 CSV 생성)")]
        private static void CreateStarterCsv()
        {
            if (File.Exists(CsvPath) &&
                !EditorUtility.DisplayDialog("CSV 생성", $"{CsvPath} 가 이미 있습니다. 덮어쓸까요?", "덮어쓰기", "취소"))
                return;

            Directory.CreateDirectory(CsvDirectory);
            File.WriteAllText(CsvPath, StarterCsv(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Loc.Reload();
            Debug.Log($"[Loc] 시작용 CSV 생성: {CsvPath}\n이 내용을 구글 시트에 붙여넣고 이후로는 시트에서 관리하세요.");
        }

        // ── 점검 도구 ──────────────────────────────────────────────────

        [MenuItem("Tools/ChainRiposte/Localization/Reload CSV %#l")] // Ctrl+Shift+L
        private static void Reload()
        {
            Loc.Reload();
            Debug.Log($"[Loc] 리로드 완료. 지원 언어: {string.Join(", ", Loc.SupportedLanguages)}");
        }

        [MenuItem("Tools/ChainRiposte/Localization/Find Missing Keys In Scene (누락 키 검사)")]
        private static void FindMissingKeys()
        {
            Loc.EnsureInit();
            var missing = new List<string>();

#if UNITY_2023_1_OR_NEWER
            LocalizedText[] binders = Object.FindObjectsByType<LocalizedText>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            LocalizedText[] binders = Object.FindObjectsOfType<LocalizedText>(true);
#endif

            foreach (LocalizedText binder in binders)
            {
                string key = binder.Key;
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogWarning($"[Loc] 키가 비어 있음: {Path(binder.transform)}", binder);
                    continue;
                }

                if (Loc.HasKey(key))
                    continue;

                missing.Add(key);
                Debug.LogWarning($"[Loc] CSV에 없는 키 '{key}': {Path(binder.transform)}", binder);
            }

            Debug.Log(missing.Count == 0
                ? $"[Loc] 누락 키 없음 — 바인더 {binders.Length}개 확인."
                : $"[Loc] 누락 {missing.Count}건:\n{string.Join("\n", missing)}");
        }

        [MenuItem("Tools/ChainRiposte/Localization/Generate Key Constants (키 상수 생성)")]
        private static void GenerateKeyConstants()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(CsvPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("키 상수 생성", $"{CsvPath} 를 찾지 못했습니다.", "확인");
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("// 자동 생성 파일 — 직접 수정하지 말 것.");
            builder.AppendLine("// Tools ▸ ChainRiposte ▸ Localization ▸ Generate Key Constants");
            builder.AppendLine();
            builder.AppendLine("namespace ChainRiposte.Game.Localization");
            builder.AppendLine("{");
            builder.AppendLine("    /// <summary>Localization.csv 의 키 상수. 오타가 컴파일 에러로 잡힌다.</summary>");
            builder.AppendLine("    public static class LocKeys");
            builder.AppendLine("    {");

            int count = 0;
            foreach (Dictionary<string, string> row in CsvReader.ReadString(asset.text))
            {
                if (!row.TryGetValue(Loc.KeyColumn, out string key) || string.IsNullOrWhiteSpace(key))
                    continue;

                string identifier = Regex.Replace(key, @"[^\w]", "_");
                if (char.IsDigit(identifier[0]))
                    identifier = "_" + identifier;

                builder.AppendLine($"        public const string {identifier} = \"{key}\";");
                count++;
            }

            builder.AppendLine("    }");
            builder.AppendLine("}");

            File.WriteAllText(KeysOutputPath, builder.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log($"[Loc] 키 상수 {count}개 생성: {KeysOutputPath}");
        }

        // ── 한글 폰트 ──────────────────────────────────────────────────

        [MenuItem("Tools/ChainRiposte/Localization/Create TMP Font From Selected TTF (한글 폰트)")]
        private static void CreateTmpFont()
        {
            var font = Selection.activeObject as Font;
            if (font == null)
            {
                EditorUtility.DisplayDialog(
                    "폰트를 먼저 고르세요",
                    "프로젝트 창에서 .ttf / .otf 폰트를 선택한 뒤 다시 실행하세요.\n\n" +
                    "한글은 글자 수가 많아 Static 아틀라스에 다 담기지 않습니다. " +
                    "이 메뉴는 Dynamic 아틀라스로 만들어 쓰이는 글리프만 실행 중에 채웁니다.\n\n" +
                    "배포 가능한 폰트(나눔고딕·Pretendard 등 OFL)를 쓰세요. " +
                    "Windows 기본 폰트는 게임에 포함해 배포할 수 없습니다.",
                    "확인");
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(font);
            string outputPath = System.IO.Path.ChangeExtension(sourcePath, null) + " SDF.asset";

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError($"[Loc] '{font.name}' 으로 TMP 폰트 에셋을 만들지 못했습니다.");
                return;
            }

            fontAsset.name = System.IO.Path.GetFileNameWithoutExtension(outputPath);
            AssetDatabase.CreateAsset(fontAsset, outputPath);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[Loc] TMP 폰트 에셋 생성: {outputPath}\n" +
                "Project Settings ▸ TextMeshPro ▸ Settings 의 Default Font Asset 으로 지정하면 기존 UI까지 한 번에 적용됩니다. " +
                "또는 Fallback Font Assets 에 추가하세요.", fontAsset);
            Selection.activeObject = fontAsset;
        }

        // ── 내부 ───────────────────────────────────────────────────────

        /// <summary>편집 URL / 내보내기 URL / 순수 ID 어느 것을 넣어도 받아낸다.</summary>
        private static (string sheetId, string gid) ParseSheetUrl(string input)
        {
            input = input.Trim();

            Match idMatch = Regex.Match(input, @"/spreadsheets/d/([a-zA-Z0-9-_]+)");
            string sheetId = idMatch.Success ? idMatch.Groups[1].Value : null;

            // URL이 아니면 ID를 직접 넣은 것으로 본다
            if (sheetId == null && Regex.IsMatch(input, @"^[a-zA-Z0-9-_]{20,}$"))
                sheetId = input;

            Match gidMatch = Regex.Match(input, @"[#&?]gid=([0-9]+)");
            return (sheetId, gidMatch.Success ? gidMatch.Groups[1].Value : null);
        }

        private static bool TryDownload(string url, out string content)
        {
            content = null;
            using UnityWebRequest request = UnityWebRequest.Get(url);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            try
            {
                while (!operation.isDone)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("시트 동기화", url, request.downloadProgress))
                    {
                        request.Abort();
                        return false;
                    }

                    System.Threading.Thread.Sleep(30);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Loc] 시트 다운로드 실패: {request.error}\n{url}");
                return false;
            }

            content = request.downloadHandler.text;
            return true;
        }

        /// <summary>받은 내용이 진짜 현지화 CSV인지 확인한다 (로그인 HTML·빈 시트 방지).</summary>
        private static bool Validate(string csv, out string reason, out int rowCount, out List<string> languages)
        {
            rowCount = 0;
            languages = new List<string>();

            if (string.IsNullOrWhiteSpace(csv))
            {
                reason = "내용이 비어 있습니다.";
                return false;
            }

            if (csv.TrimStart().StartsWith("<"))
            {
                reason = "CSV가 아니라 HTML을 받았습니다 (공유 설정이 비공개일 때 나타납니다).";
                return false;
            }

            List<Dictionary<string, string>> rows = CsvReader.ReadString(csv);
            if (rows.Count == 0)
            {
                reason = "데이터 행이 없습니다.";
                return false;
            }

            if (!rows[0].ContainsKey(Loc.KeyColumn))
            {
                reason = $"첫 컬럼 이름이 '{Loc.KeyColumn}' 이어야 합니다.";
                return false;
            }

            foreach (string column in rows[0].Keys)
            {
                if (column != Loc.KeyColumn && !string.IsNullOrWhiteSpace(column) &&
                    System.Enum.TryParse(column, out SystemLanguage _))
                    languages.Add(column);
            }

            if (languages.Count == 0)
            {
                reason = "SystemLanguage 이름과 일치하는 언어 컬럼이 하나도 없습니다 (예: Korean, English).";
                return false;
            }

            foreach (Dictionary<string, string> row in rows)
            {
                if (row.TryGetValue(Loc.KeyColumn, out string key) && !string.IsNullOrWhiteSpace(key))
                    rowCount++;
            }

            reason = null;
            return rowCount > 0;
        }

        private static string Path(Transform transform)
        {
            var builder = new StringBuilder(transform.name);
            while (transform.parent != null)
            {
                transform = transform.parent;
                builder.Insert(0, transform.name + "/");
            }

            return builder.ToString();
        }

        /// <summary>
        /// 게임에서 실제로 쓰는 키 전부. 이걸 구글 시트에 붙여넣고 이후로는 시트에서 관리한다.
        /// 문구를 새로 추가할 때는 여기가 아니라 <b>시트</b>에 넣을 것 — 이 목록은 최초 부팅용이다.
        /// </summary>
        private static string StarterCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Key,Korean,English");

            // 타이틀
            Row(builder, "title.continue", "이어하기", "CONTINUE");
            Row(builder, "title.newgame", "새 게임", "NEW GAME");
            Row(builder, "title.options", "옵션", "OPTIONS");
            Row(builder, "title.quit", "나가기", "QUIT");
            Row(builder, "title.newgame.confirm",
                "새로 시작하면 지금까지의 진행도가 모두 지워집니다.\\n계속할까요?",
                "Starting a new game erases all progress.\\nContinue?");

            // 옵션
            Row(builder, "options.title", "옵션", "OPTIONS");
            Row(builder, "options.bgm", "배경음", "MUSIC");
            Row(builder, "options.sfx", "효과음", "SFX");
            Row(builder, "options.orientation", "화면 방향", "SCREEN");
            Row(builder, "options.orientation.auto", "자동", "AUTO");
            Row(builder, "options.orientation.portrait", "세로 고정", "PORTRAIT");
            Row(builder, "options.orientation.landscape", "가로 고정", "LANDSCAPE");
            Row(builder, "options.language", "언어", "LANGUAGE");
            Row(builder, "options.reset", "진행도 초기화", "RESET PROGRESS");
            Row(builder, "options.reset.confirm",
                "저장된 진행도를 모두 지웁니다.\\n되돌릴 수 없습니다. 계속할까요?",
                "This erases all saved progress.\\nThis cannot be undone.");

            // 월드맵
            Row(builder, "map.start", "시작", "START");
            Row(builder, "map.title", "스테이지 {0}", "STAGE {0}");
            Row(builder, "map.title.clear", "스테이지 {0}  - 클리어", "STAGE {0}  - CLEAR");
            Row(builder, "map.title.locked", "스테이지 {0}  - 잠김", "STAGE {0}  - LOCKED");
            Row(builder, "map.locked.body", "잠김\\n이전 스테이지를 먼저 클리어하세요", "LOCKED\\nCLEAR THE PREVIOUS STAGE FIRST");
            Row(builder, "map.info",
                "월드 {0}   보드 {1}x{2}   턴 {3}\\n보스  {4}\\n위험  {5}",
                "WORLD {0}   BOARD {1}x{2}   TURNS {3}\\nBOSS  {4}\\nHAZARD  {5}");
            Row(builder, "map.unknown", "???", "???");
            Row(builder, "map.hazard.none", "없음", "NONE");

            // 기믹 이름 (GDD 3.6)
            Row(builder, "gimmick.corruption", "전염", "CORRUPTION");
            Row(builder, "gimmick.timebomb", "시한폭탄", "TIME BOMB");
            Row(builder, "gimmick.chains", "사슬 결박", "CHAINS");

            // 퍼즐 HUD
            Row(builder, "puzzle.hp", "체력 {0}/{1}", "HP {0}/{1}");
            Row(builder, "puzzle.turns", "턴 {0}", "TURNS {0}");
            Row(builder, "puzzle.souls", "Lv {0}   영혼석 {1}/{2}   포인트 {3}", "Lv {0}   Souls {1}/{2}   Points {3}");
            Row(builder, "puzzle.stats",
                "공격 {0:0}   방어 {1:0}   패링 {2:0.00}초",
                "ATK {0:0}   DEF {1:0}   PARRY {2:0.00}s");
            Row(builder, "puzzle.alloc.attack", "+공격\\nLv {0}", "+ATK\\nLv {0}");
            Row(builder, "puzzle.alloc.defense", "+방어\\nLv {0}", "+DEF\\nLv {0}");
            Row(builder, "puzzle.alloc.parry", "+패링\\nLv {0}", "+PARRY\\nLv {0}");
            Row(builder, "puzzle.alloc.attack.max", "+공격\\n최대", "+ATK\\nMAX");
            Row(builder, "puzzle.alloc.defense.max", "+방어\\n최대", "+DEF\\nMAX");
            Row(builder, "puzzle.alloc.parry.max", "+패링\\n최대", "+PARRY\\nMAX");
            Row(builder, "puzzle.banner.victory", "스테이지 클리어", "STAGE CLEAR");
            Row(builder, "puzzle.banner.defeat", "패배", "DEFEAT");
            Row(builder, "puzzle.banner.combat", "보스!", "BOSS!");
            Row(builder, "puzzle.banner.noMoves", "둘 수 있는 수 없음 — 섞는 중", "NO MOVES — SHUFFLING");
            Row(builder, "puzzle.countdown", "{0}초|{1}턴", "{0}s|{1}t");

            // 전투
            Row(builder, "combat.boss", "보스", "BOSS");
            Row(builder, "combat.hp", "체력 {0}/{1}", "HP {0}/{1}");
            Row(builder, "combat.posture", "체간", "POSTURE");
            Row(builder, "combat.parry", "패링", "PARRY");
            Row(builder, "combat.attack", "공격", "ATTACK");
            Row(builder, "combat.execute", "인살!!", "EXECUTE!!");
            Row(builder, "combat.intro", "보스 전투", "BOSS BATTLE");
            Row(builder, "combat.popup.parry", "패링!", "PARRY!");

            // 결과
            Row(builder, "result.victory", "스테이지 클리어", "STAGE CLEAR");
            Row(builder, "result.defeat", "패배", "DEFEAT");
            Row(builder, "result.restart", "다시 시작", "RESTART");
            Row(builder, "result.map", "지도", "MAP");

            // 공통
            Row(builder, "common.yes", "예", "YES");
            Row(builder, "common.no", "아니오", "NO");
            Row(builder, "common.back", "뒤로", "BACK");

            return builder.ToString();
        }

        /// <summary>콤마·큰따옴표가 든 값을 CSV 규격대로 감싼다.</summary>
        private static void Row(StringBuilder builder, string key, string korean, string english) =>
            builder.AppendLine($"{key},{Escape(korean)},{Escape(english)}");

        private static string Escape(string value) =>
            value.Contains(",") || value.Contains("\"")
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
    }
}
