using ChainRiposte.Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 퍼즐 화면 <b>아래쪽</b>에 체력 게이지 + 보스 시계를 얹는 툴.
    ///
    /// <para><b>비파괴</b>다 — <c>PuzzleHud</c> 안에 <c>BottomBar</c> 하나만 만들거나 다시 만들고,
    /// 다른 화면과 기존 HUD 텍스트는 건드리지 않는다. (<c>Build Main Scene UI</c>는 화면을 자식째
    /// 갈아엎으므로 손으로 꽂은 UI가 날아간다 — 그래서 전용 메뉴로 얹는다.)</para>
    ///
    /// <para>왜 필요한가: 퍼즐의 체력이 <b>숫자로만</b> 좌상단에 있어서, 성난 몬스터에게 맞아도
    /// 눈에 들어오지 않았다. 전투 화면과 <b>같은 모양의 게이지</b>를 같은 자리(아래쪽)에 둬서
    /// 퍼즐과 전투가 하나의 체력을 공유한다는 것이 읽히게 한다.</para>
    /// </summary>
    public static class PuzzleBottomBarBuilder
    {
        private const string BarName = "BottomBar";

        [MenuItem("Tools/ChainRiposte/Add Puzzle Bottom Bar To Main")]
        private static void Build()
        {
            var hud = Object.FindFirstObjectByType<PuzzleHud>(FindObjectsInactive.Include);
            if (hud == null)
            {
                EditorUtility.DisplayDialog("퍼즐 하단 바 추가",
                    "이 씬에서 PuzzleHud 를 찾지 못했습니다. Main 씬을 열고 다시 실행하세요.", "확인");
                return;
            }

            Transform canvas = hud.transform.childCount > 0 ? hud.transform.GetChild(0) : hud.transform;

            // 같은 이름의 것만 갈아 끼운다 — 나머지 HUD 자식은 손대지 않는다.
            Transform existing = canvas.Find(BarName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            RectTransform bar = EditorUiFactory.NewRect(BarName, canvas);
            Undo.RegisterCreatedObjectUndo(bar.gameObject, "Add Puzzle Bottom Bar");
            bar.anchorMin = new Vector2(0.5f, 0f);
            bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(1000f, 340f);

            // 보스 시계 — 아래쪽 빈 공간의 주인공이라 크게. 색은 PuzzleHud 가 경고 상태에 따라 바꾼다.
            TMP_Text timer = EditorUiFactory.Text(bar, "BossTimerText", new Vector2(0f, 250f),
                new Vector2(0.5f, 0f), 92f, TextAlignmentOptions.Center, new Vector2(1000f, 120f), FontStyles.Bold);

            // 체력 게이지 — 전투 화면(PlayerHpBar)과 같은 크기·색으로 맞춘다.
            Image hpFill = EditorUiFactory.Bar(bar, "PlayerHpBar", new Vector2(0f, 120f),
                new Vector2(0.5f, 0f), new Vector2(900f, 40f), new Color(0.25f, 0.62f, 0.30f));
            TMP_Text hpText = EditorUiFactory.Text(bar, "PlayerHpText", new Vector2(0f, 170f),
                new Vector2(0.5f, 0f), 40f, TextAlignmentOptions.Center, new Vector2(1000f, 60f));

            // 세로 화면은 아래가 넉넉하고, 가로 화면은 아래가 좁다 — 가로에서는 붙이고 줄인다.
            var bottom = new Vector2(0.5f, 0f);
            EditorUiFactory.Orient(timer, bottom, bottom, new Vector2(0f, 170f), new Vector2(1000f, 100f));
            EditorUiFactory.Orient(hpFill.transform.parent, bottom, bottom, new Vector2(0f, 60f), new Vector2(1200f, 32f));
            EditorUiFactory.Orient(hpText, bottom, bottom, new Vector2(0f, 100f), new Vector2(1000f, 60f));

            var so = new SerializedObject(hud);
            so.FindProperty("playerHpFill").objectReferenceValue = hpFill;
            so.FindProperty("playerHpText").objectReferenceValue = hpText;
            so.FindProperty("bossTimerText").objectReferenceValue = timer;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);

            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            Debug.Log("[PuzzleBottomBarBuilder] 퍼즐 하단 바를 얹고 PuzzleHud 에 배선했습니다. " +
                      "위치·크기는 씬에서 자유롭게 조절하세요.", hud);
        }
    }
}
