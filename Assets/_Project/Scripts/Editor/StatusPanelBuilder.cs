using ChainRiposte.Game.Config;
using ChainRiposte.Game.Map;
using ChainRiposte.Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 월드맵에 <b>현재 상태 창</b>을 얹는 툴 — 캐릭터 얼굴 버튼(좌하단)을 누르면 열린다.
    ///
    /// <para>퍼즐에는 HUD가 늘 떠 있지만 지도에는 없어서, 다음 판에 들어가기 전에
    /// "내가 얼마나 강한가 · 무슨 기억을 가졌나"를 확인할 방법이 없었다.</para>
    ///
    /// <para><b>비파괴</b>다 — 지도 캔버스 안에 <c>StatusTab</c> 하나만 만들거나 다시 만들고
    /// 나머지 UI(정보 패널·START·배경 띠)는 건드리지 않는다.</para>
    /// </summary>
    public static class StatusPanelBuilder
    {
        private const string RootName = "StatusTab";

        [MenuItem("Tools/ChainRiposte/Add Status Panel To StageSelect")]
        private static void Build()
        {
            var map = Object.FindFirstObjectByType<StageSelectController>(FindObjectsInactive.Include);
            if (map == null)
            {
                EditorUtility.DisplayDialog("현재 상태 창 추가",
                    "이 씬에서 StageSelectController 를 찾지 못했습니다. StageSelect 씬을 열고 다시 실행하세요.", "확인");
                return;
            }

            Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("현재 상태 창 추가", "지도 씬에 Canvas 가 없습니다.", "확인");
                return;
            }

            Transform parent = canvas.transform;
            Transform existing = parent.Find(RootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            RectTransform root = EditorUiFactory.NewRect(RootName, parent);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Add Status Panel");
            Stretch(root);

            // ── 얼굴 버튼: 좌하단. 지도의 START·정보 패널은 오른쪽/아래 중앙이라 겹치지 않는다.
            Button face = EditorUiFactory.Button(
                root, "FaceButton", new Vector2(40f, 40f), Vector2.zero, Vector2.zero, new Vector2(160f, 160f),
                new Color(0.16f, 0.15f, 0.2f), string.Empty, 1f, out Image faceBg, out TextMeshProUGUI faceLabel);
            faceLabel.gameObject.SetActive(false); // 얼굴만 보이면 된다 — 글자는 필요 없다

            RectTransform faceIconRect = EditorUiFactory.NewRect("Face", face.transform);
            faceIconRect.anchorMin = Vector2.zero;
            faceIconRect.anchorMax = Vector2.one;
            faceIconRect.offsetMin = new Vector2(8f, 8f);
            faceIconRect.offsetMax = new Vector2(-8f, -8f);
            var faceIcon = faceIconRect.gameObject.AddComponent<Image>();
            faceIcon.sprite = EditorUiFactory.Square;
            faceIcon.color = new Color(0.75f, 0.72f, 0.6f); // 초상은 런타임에 고른 캐릭터로 채워진다
            faceIcon.raycastTarget = false;
            // 칸은 정사각형인데 캐릭터 그림은 아니다 — 늘려 채우면 얼굴이 찌그러진다.
            faceIcon.preserveAspect = true;
            face.targetGraphic = faceBg;

            // ── 패널: 화면 전체 딤 + 카드. 딤이 raycast를 먹어 지도 클릭이 통과하지 않는다
            //    (창이 열린 채로 뒤의 노드를 눌러 판에 들어가 버리면 안 된다).
            RectTransform panel = EditorUiFactory.NewRect("Panel", root);
            Stretch(panel);
            var dim = panel.gameObject.AddComponent<Image>();
            dim.sprite = EditorUiFactory.Square;
            dim.color = new Color(0f, 0f, 0f, 0.78f);

            RectTransform card = EditorUiFactory.NewRect("Card", panel);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(900f, 1150f);
            var cardBg = card.gameObject.AddComponent<Image>();
            cardBg.sprite = EditorUiFactory.Square;
            cardBg.color = new Color(0.07f, 0.06f, 0.1f, 0.96f);

            var top = new Vector2(0.5f, 1f);
            TMP_Text title = EditorUiFactory.Text(card, "Title", new Vector2(0f, -50f), top, 64f,
                TextAlignmentOptions.Center, new Vector2(860f, 90f), FontStyles.Bold);
            title.color = new Color(0.95f, 0.83f, 0.35f);

            RectTransform portraitRect = EditorUiFactory.NewRect("Portrait", card);
            portraitRect.anchorMin = portraitRect.anchorMax = portraitRect.pivot = top;
            portraitRect.anchoredPosition = new Vector2(0f, -160f);
            portraitRect.sizeDelta = new Vector2(220f, 220f);
            var portrait = portraitRect.gameObject.AddComponent<Image>();
            portrait.sprite = EditorUiFactory.Square;
            portrait.color = new Color(0.75f, 0.72f, 0.6f);
            portrait.raycastTarget = false;
            portrait.preserveAspect = true;

            TMP_Text nameText = EditorUiFactory.Text(card, "Name", new Vector2(0f, -400f), top, 48f,
                TextAlignmentOptions.Center, new Vector2(860f, 70f), FontStyles.Bold);

            TMP_Text hp = Line(card, "Hp", -480f);
            TMP_Text souls = Line(card, "Souls", -540f);
            TMP_Text stats = Line(card, "Stats", -600f);
            TMP_Text chain = Line(card, "Chain", -660f);

            TMP_Text memoryHeader = EditorUiFactory.Text(card, "MemoryHeader", new Vector2(0f, -740f), top, 40f,
                TextAlignmentOptions.Center, new Vector2(860f, 60f), FontStyles.Bold);
            memoryHeader.color = new Color(0.75f, 0.88f, 1f);

            // 기억 아이콘 줄 — 판 안의 것과 같은 컴포넌트다. GameManager 가 없으면 저장된 런을 읽는다.
            MemoryStrip strip = MemoryStripBuilder.CreateStrip(
                card, manager: null, anchor: top, position: new Vector2(0f, -800f),
                highlightGained: false, withLabel: false);

            TMP_Text memoryList = EditorUiFactory.Text(card, "MemoryList", new Vector2(0f, -900f), top, 34f,
                TextAlignmentOptions.Top, new Vector2(820f, 200f));

            Button close = EditorUiFactory.Button(
                card, "CloseButton", new Vector2(0f, 50f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(420f, 120f), new Color(0.18f, 0.17f, 0.22f), "CLOSE", 44f,
                out _, out TextMeshProUGUI closeLabel);
            EditorUiFactory.Localize(closeLabel, "common.back");

            var script = root.gameObject.AddComponent<StatusPanel>();
            var so = new SerializedObject(script);
            so.FindProperty("statsConfig").objectReferenceValue = FindStatsConfig();
            so.FindProperty("faceButton").objectReferenceValue = face;
            so.FindProperty("faceImage").objectReferenceValue = faceIcon;
            so.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("portraitImage").objectReferenceValue = portrait;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("hpText").objectReferenceValue = hp;
            so.FindProperty("soulsText").objectReferenceValue = souls;
            so.FindProperty("statsText").objectReferenceValue = stats;
            so.FindProperty("chainText").objectReferenceValue = chain;
            so.FindProperty("memoryHeaderText").objectReferenceValue = memoryHeader;
            so.FindProperty("memoryListText").objectReferenceValue = memoryList;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false); // 지도에 들어오면 닫힌 상태 — 길이 먼저 보여야 한다

            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            Debug.Log("[StatusPanelBuilder] 현재 상태 창을 얹었습니다. " +
                      "얼굴·초상 그림은 고른 캐릭터의 초상으로 런타임에 채워지고, 배치는 씬에서 조절하세요. " +
                      $"기억 아이콘 원본은 {RootName}/Panel/Card/MemoryStrip/Icons/IconTemplate 입니다.", strip);
        }

        private static TMP_Text Line(Transform card, string name, float y) =>
            EditorUiFactory.Text(card, name, new Vector2(0f, y), new Vector2(0.5f, 1f), 36f,
                TextAlignmentOptions.Center, new Vector2(860f, 56f));

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>공용 밸런스 에셋을 프로젝트에서 찾아 꽂는다 — 하나뿐이라 이름을 물어볼 이유가 없다.</summary>
        private static PlayerStatsConfigSO FindStatsConfig()
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(PlayerStatsConfigSO)}"))
            {
                var config = AssetDatabase.LoadAssetAtPath<PlayerStatsConfigSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (config != null)
                    return config;
            }

            Debug.LogWarning("[StatusPanelBuilder] PlayerStatsConfigSO 를 못 찾았습니다 — 인스펙터에서 직접 꽂아 주세요.");
            return null;
        }
    }
}
