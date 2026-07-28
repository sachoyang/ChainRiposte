using ChainRiposte.Game.Combat;
using ChainRiposte.Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// Main 씬의 UI 3종(퍼즐 HUD / 전투 화면 / 결과 화면)을 씬에 <b>실물 오브젝트</b>로 생성하고
    /// 각 컨트롤러의 참조를 자동 배선하는 에디터 툴. 이후 씬 뷰에서 자유롭게 편집·에셋 교체.
    /// (보드 타일/셀은 데이터로 개수가 정해지므로 런타임 생성 유지 — 여기서 다루지 않음.)
    ///
    /// 실행: <c>Tools ▸ ChainRiposte ▸ Build Main Scene UI</c> (Main.unity 를 연 상태에서).
    /// 다시 실행하면 각 화면의 기존 자식을 지우고 새로 깐다.
    /// </summary>
    public static class MainSceneBuilder
    {
        private static readonly Color PanelButtonColor = new(0.22f, 0.20f, 0.26f, 0.95f);
        private static readonly Color AccentButtonColor = new(0.55f, 0.16f, 0.18f, 1f);

        [MenuItem("Tools/ChainRiposte/Build Main Scene UI")]
        private static void Build()
        {
            var hud = Object.FindFirstObjectByType<PuzzleHud>();
            var combat = Object.FindFirstObjectByType<CombatScreen>();
            var result = Object.FindFirstObjectByType<ResultScreen>();

            if (hud == null && combat == null && result == null)
            {
                EditorUtility.DisplayDialog("Main UI 생성",
                    "씬에서 PuzzleHud / CombatScreen / ResultScreen 을 찾지 못했습니다.\n" +
                    "Main.unity 를 연 상태에서 실행하세요.", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog("Main UI 생성",
                    "각 UI 화면(HUD/전투/결과)의 기존 자식을 지우고 새로 생성합니다.\n계속할까요?",
                    "생성", "취소"))
                return;

            if (hud != null) BuildHud(hud);
            if (combat != null) BuildCombat(combat);
            if (result != null) BuildResult(result);

            // 보스 돌입 준비 화면 — 컴포넌트가 없으면 GameManager 옆에 만들어 붙인다
            var intermission = Object.FindFirstObjectByType<IntermissionScreen>();
            if (intermission == null)
            {
                var go = new GameObject("IntermissionScreen");
                Undo.RegisterCreatedObjectUndo(go, "Build Main UI");
                intermission = go.AddComponent<IntermissionScreen>();

                var manager = Object.FindFirstObjectByType<ChainRiposte.Game.GameManager>();
                if (manager != null)
                {
                    var link = new SerializedObject(intermission);
                    Set(link, "gameManager", manager);
                    link.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            BuildIntermission(intermission);

            GameObject anchor = hud != null ? hud.gameObject : combat != null ? combat.gameObject : result.gameObject;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(anchor.scene);
            Debug.Log("[MainSceneBuilder] Main UI 생성 완료. 씬에서 편집하세요. " +
                      "(TMP가 처음이면 Window ▸ TextMeshPro ▸ Import TMP Essential Resources 필요)");
        }

        // ── 퍼즐 HUD ──

        private static void BuildHud(PuzzleHud hud)
        {
            Transform root = PrepareCanvas(hud.gameObject, 0);

            TMP_Text hp = EditorUiFactory.Text(root, "HpText", new Vector2(40f, -40f), new Vector2(0f, 1f), 52f, TextAlignmentOptions.TopLeft, new Vector2(1000f, 60f));
            TMP_Text souls = EditorUiFactory.Text(root, "SoulsText", new Vector2(40f, -110f), new Vector2(0f, 1f), 44f, TextAlignmentOptions.TopLeft, new Vector2(1000f, 60f));
            TMP_Text turns = EditorUiFactory.Text(root, "TurnsText", new Vector2(40f, -170f), new Vector2(0f, 1f), 44f, TextAlignmentOptions.TopLeft, new Vector2(1000f, 60f));
            TMP_Text stats = EditorUiFactory.Text(root, "StatsText", new Vector2(40f, -230f), new Vector2(0f, 1f), 44f, TextAlignmentOptions.TopLeft, new Vector2(1000f, 60f));
            TMP_Text banner = EditorUiFactory.Text(root, "Banner", Vector2.zero, new Vector2(0.5f, 0.5f), 130f, TextAlignmentOptions.Center, new Vector2(1000f, 240f), FontStyles.Bold);
            banner.color = new Color(0.85f, 0.2f, 0.25f);

            // 스탯 분배 버튼은 여기 없다 — 준비 화면(BuildIntermission)으로 옮겼다.
            var so = new SerializedObject(hud);
            Set(so, "hpText", hp);
            Set(so, "soulsText", souls);
            Set(so, "turnsText", turns);
            Set(so, "statsText", stats);
            Set(so, "bannerText", banner);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);
        }

        // ── 전투 화면 ──

        private static void BuildCombat(CombatScreen screen)
        {
            Transform canvas = PrepareCanvas(screen.gameObject, 10);
            var bossColor = new Color(0.62f, 0.08f, 0.12f);

            Image rootImg = EditorUiFactory.Stretch(canvas, "Root", new Color(0.07f, 0.06f, 0.09f, 1f), raycast: true);
            RectTransform root = rootImg.rectTransform;

            TMP_Text bossName = EditorUiFactory.Text(root, "BossName", new Vector2(0f, -90f), new Vector2(0.5f, 1f), 60f, TextAlignmentOptions.Center, new Vector2(1000f, 140f), FontStyles.Bold);
            Image bossHp = EditorUiFactory.Bar(root, "BossHpBar", new Vector2(0f, -170f), new Vector2(0.5f, 1f), new Vector2(900f, 26f), new Color(0.55f, 0.10f, 0.12f));
            Image posture = EditorUiFactory.Bar(root, "PostureBar", new Vector2(0f, -225f), new Vector2(0.5f, 1f), new Vector2(900f, 46f), new Color(0.95f, 0.62f, 0.12f));
            TMP_Text postureLabel = EditorUiFactory.Text(root, "PostureLabel", new Vector2(0f, -280f), new Vector2(0.5f, 1f), 32f, TextAlignmentOptions.Center, new Vector2(1000f, 140f));
            EditorUiFactory.Localize(postureLabel, "combat.posture");
            postureLabel.color = new Color(0.95f, 0.62f, 0.12f);

            // 포켓몬식 대치 — 보스는 오른쪽 위, 플레이어는 왼쪽 아래.
            // 공격은 보스에서 나와 '나에게' 도달하므로 패링 원은 플레이어 자리로 모인다.
            var bossHome = new Vector2(250f, 420f);
            var playerHome = new Vector2(-230f, -180f);

            // 패링 가능 구간 — 플레이어를 감싸는 연한 회색 원. 노트 원과 같은 두께다.
            // 흰 원이 여기에 조금이라도 겹치면 패링이다. 두께는 런타임에 판정 폭으로 다시 그려진다.
            RectTransform band = EditorUiFactory.NewRect("ParryBand", root);
            band.anchorMin = band.anchorMax = new Vector2(0.5f, 0.5f);
            band.anchoredPosition = playerHome;
            band.sizeDelta = new Vector2(300f, 300f); // 플레이어 본체와 같은 크기 = 스케일 1이 타격 지점
            var bandImg = band.gameObject.AddComponent<Image>();
            bandImg.sprite = ChainRiposte.Game.PlaceholderSprite.Annulus(0.89f); // 노트 원과 같은 두께
            bandImg.color = new Color(1f, 1f, 1f, 0.15f);
            bandImg.raycastTarget = false;

            // 다가오는 노트 원의 복제 원본 — 개수가 채보로 정해지므로 CombatScreen이 필요한 만큼 복제한다
            RectTransform ring = EditorUiFactory.NewRect("NoteRingTemplate", root);
            ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
            ring.anchoredPosition = playerHome;
            ring.sizeDelta = new Vector2(300f, 300f);
            var ringImg = ring.gameObject.AddComponent<Image>();
            ringImg.sprite = ChainRiposte.Game.PlaceholderSprite.Ring;
            ringImg.color = Color.white;
            ringImg.raycastTarget = false;
            ring.gameObject.SetActive(false);

            RectTransform bossBody = EditorUiFactory.NewRect("BossBody", root);
            bossBody.anchorMin = bossBody.anchorMax = new Vector2(0.5f, 0.5f);
            bossBody.anchoredPosition = bossHome;
            bossBody.sizeDelta = new Vector2(340f, 340f);
            var bossBodyImg = bossBody.gameObject.AddComponent<Image>();
            bossBodyImg.sprite = EditorUiFactory.Square;
            bossBodyImg.color = bossColor;
            bossBodyImg.raycastTarget = false;

            RectTransform playerBody = EditorUiFactory.NewRect("PlayerBody", root);
            playerBody.anchorMin = playerBody.anchorMax = new Vector2(0.5f, 0.5f);
            playerBody.anchoredPosition = playerHome;
            playerBody.sizeDelta = new Vector2(300f, 300f);
            var playerBodyImg = playerBody.gameObject.AddComponent<Image>();
            playerBodyImg.sprite = EditorUiFactory.Square;
            playerBodyImg.color = new Color(0.35f, 0.55f, 0.75f);
            playerBodyImg.raycastTarget = false;

            TMP_Text execute = EditorUiFactory.Text(root, "ExecuteMark", new Vector2(0f, 470f), new Vector2(0.5f, 0.5f), 96f, TextAlignmentOptions.Center, new Vector2(1000f, 140f), FontStyles.Bold);
            EditorUiFactory.Localize(execute, "combat.execute");
            TMP_Text popup = EditorUiFactory.Text(root, "Popup", new Vector2(0f, 560f), new Vector2(0.5f, 0.5f), 72f, TextAlignmentOptions.Center, new Vector2(1000f, 140f), FontStyles.Bold);

            Image playerHp = EditorUiFactory.Bar(root, "PlayerHpBar", new Vector2(0f, 430f), new Vector2(0.5f, 0f), new Vector2(900f, 40f), new Color(0.25f, 0.62f, 0.30f));
            TMP_Text playerHpText = EditorUiFactory.Text(root, "PlayerHpText", new Vector2(0f, 490f), new Vector2(0.5f, 0f), 40f, TextAlignmentOptions.Center, new Vector2(1000f, 140f));

            Button parry = EditorUiFactory.Button(root, "ParryButton", new Vector2(-270f, 60f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(440f, 300f), Color.white, "PARRY", 52f, out Image parryImg, out TextMeshProUGUI parryLabel);
            Button attack = EditorUiFactory.Button(root, "AttackButton", new Vector2(270f, 60f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(440f, 300f), Color.white, "ATTACK", 52f, out Image attackImg, out TextMeshProUGUI attackLabel);
            EditorUiFactory.Localize(parryLabel, "combat.parry");
            EditorUiFactory.Localize(attackLabel, "combat.attack");

            Image flash = EditorUiFactory.Stretch(root, "FlashOverlay", Color.clear, raycast: false);

            // ── 가로 배치 (GDD §9.3): 2버튼은 양쪽 사이드, 보스/게이지는 위로 당긴다 ──
            var top = new Vector2(0.5f, 1f);
            var center = new Vector2(0.5f, 0.5f);
            var bottom = new Vector2(0.5f, 0f);
            EditorUiFactory.Orient(bossName, top, top, new Vector2(0f, -50f), new Vector2(1000f, 140f));
            // 게이지는 Bar()가 돌려주는 '채움' 이미지가 아니라 부모(막대 본체)를 옮겨야 한다
            EditorUiFactory.Orient(bossHp.transform.parent, top, top, new Vector2(0f, -100f), new Vector2(1400f, 26f));
            EditorUiFactory.Orient(posture.transform.parent, top, top, new Vector2(0f, -140f), new Vector2(1400f, 46f));
            EditorUiFactory.Orient(postureLabel, top, top, new Vector2(0f, -185f), new Vector2(1000f, 140f));
            // 원과 띠는 플레이어 본체와 정확히 같은 자리·크기여야 한다 (스케일 1 = 타격 지점)
            EditorUiFactory.Orient(ring, center, center, new Vector2(-400f, -110f), new Vector2(260f, 260f));
            EditorUiFactory.Orient(band, center, center, new Vector2(-400f, -110f), new Vector2(260f, 260f));
            // 가로에서는 좌우가 넓으니 대치 간격을 더 벌린다
            EditorUiFactory.Orient(bossBody, center, center, new Vector2(430f, 190f), new Vector2(300f, 300f));
            EditorUiFactory.Orient(playerBody, center, center, new Vector2(-400f, -110f), new Vector2(260f, 260f));
            EditorUiFactory.Orient(execute, center, center, new Vector2(0f, 250f), new Vector2(1000f, 140f));
            EditorUiFactory.Orient(popup, center, center, new Vector2(0f, 330f), new Vector2(1000f, 140f));
            EditorUiFactory.Orient(playerHp.transform.parent, bottom, bottom, new Vector2(0f, 120f), new Vector2(1000f, 36f));
            EditorUiFactory.Orient(playerHpText, bottom, bottom, new Vector2(0f, 165f), new Vector2(1000f, 140f));
            EditorUiFactory.Orient(parry, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(60f, -120f), new Vector2(340f, 420f));
            EditorUiFactory.Orient(attack, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-60f, -120f), new Vector2(340f, 420f));

            TMP_Text intro = EditorUiFactory.Text(root, "Intro", Vector2.zero, new Vector2(0.5f, 0.5f), 130f, TextAlignmentOptions.Center, new Vector2(1000f, 240f), FontStyles.Bold);
            EditorUiFactory.Localize(intro, "combat.intro");

            var so = new SerializedObject(screen);
            Set(so, "root", root);
            Set(so, "bossNameText", bossName);
            Set(so, "bossHpFill", bossHp);
            Set(so, "postureFill", posture);
            Set(so, "playerHpFill", playerHp);
            Set(so, "playerHpText", playerHpText);
            Set(so, "bossBody", bossBody);
            Set(so, "bossBodyImage", bossBodyImg);
            Set(so, "playerBody", playerBody);
            Set(so, "playerBodyImage", playerBodyImg);
            Set(so, "noteRingTemplate", ring);
            Set(so, "parryBand", band);
            Set(so, "parryBandImage", bandImg);
            Set(so, "popupText", popup);
            Set(so, "executeText", execute);
            Set(so, "flashOverlay", flash);
            Set(so, "introText", intro);
            Set(so, "parryButton", parry);
            Set(so, "attackButton", attack);
            Set(so, "parryButtonImage", parryImg);
            Set(so, "attackButtonImage", attackImg);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(screen);
        }

        // ── 결과 화면 ──

        private static void BuildResult(ResultScreen result)
        {
            Transform canvas = PrepareCanvas(result.gameObject, 20);

            Image dim = EditorUiFactory.Stretch(canvas, "Root", new Color(0f, 0f, 0f, 0.72f), raycast: true);
            Transform panel = dim.transform;

            TMP_Text title = EditorUiFactory.Text(panel, "Title", new Vector2(0f, 160f), new Vector2(0.5f, 0.5f), 120f, TextAlignmentOptions.Center, new Vector2(1000f, 240f), FontStyles.Bold);
            Button restart = EditorUiFactory.Button(panel, "RestartButton", new Vector2(0f, -120f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(460f, 160f), PanelButtonColor, "RESTART", 56f, out _, out TextMeshProUGUI restartLabel);
            Button map = EditorUiFactory.Button(panel, "MapButton", new Vector2(0f, -320f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(460f, 160f), PanelButtonColor, "MAP", 56f, out _, out TextMeshProUGUI mapLabel);
            EditorUiFactory.Localize(restartLabel, "result.restart");
            EditorUiFactory.Localize(mapLabel, "result.map");

            var so = new SerializedObject(result);
            Set(so, "panelRoot", dim.gameObject);
            Set(so, "titleText", title);
            Set(so, "restartButton", restart);
            Set(so, "mapButton", map);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(result);
        }

        // ── 보스 돌입 준비 ──

        /// <summary>
        /// 화면 전체를 살짝 덮는 어둠 + 아래쪽 띠. 어둠은 "지금은 퍼즐이 아니라 준비 시간"임을
        /// 한눈에 알리는 장치이고, 퍼즐판이 비쳐 보일 정도로만 깐다(다음 판을 눈으로 재야 하므로).
        /// 스탯 분배 버튼 3개는 FIGHT 아래에 붙는다 — 퍼즐 화면에는 더 이상 두지 않는다.
        /// </summary>
        /// <summary>
        /// 준비 화면 위쪽에서 다가오는 보스 그림자. 띠(Band)의 형제로 두어 <b>띠 위 화면 상단</b>에 자리잡게 한다 —
        /// 띠 자식으로 두면 어두운 띠에 묻힌다. 그림은 런타임에 채워지므로 여기선 자리·크기만 잡는다.
        /// </summary>
        private static BossShadow BuildBossShadow(RectTransform root)
        {
            RectTransform rect = EditorUiFactory.NewRect("BossShadow", root);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f); // 화면 상단 중앙
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -40f);
            rect.sizeDelta = new Vector2(520f, 520f);
            rect.SetAsFirstSibling(); // 띠보다 뒤에 그려 실루엣이 UI를 가리지 않게

            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = new Color(0f, 0f, 0f, 0f); // 컨트롤러가 그림자 색으로 올린다

            BossShadow shadow = rect.gameObject.AddComponent<BossShadow>();
            var so = new SerializedObject(shadow);
            Set(so, "image", image);
            so.ApplyModifiedPropertiesWithoutUndo();

            rect.gameObject.SetActive(false); // 보스 그림이 없으면 꺼진 채로
            return shadow;
        }

        private static void BuildIntermission(IntermissionScreen screen)
        {
            Transform canvas = PrepareCanvas(screen.gameObject, 15);

            // 화면 전체 루트 — 이 페이즈 동안 퍼즐 입력도 같이 막는다
            Image dim = EditorUiFactory.Stretch(canvas, "Root", new Color(0f, 0f, 0f, 0.45f), raycast: true);
            RectTransform root = dim.rectTransform;

            RectTransform panel = EditorUiFactory.NewRect("Band", root);
            panel.anchorMin = new Vector2(0f, 0.5f);
            panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = new Vector2(0f, -60f);
            panel.sizeDelta = new Vector2(0f, 860f);
            var backdrop = panel.gameObject.AddComponent<Image>();
            backdrop.sprite = EditorUiFactory.Square;
            // 완전 불투명이 아니다 — 뒤 퍼즐판이 어렴풋이 비쳐야 '같은 판 위의 상점'으로 읽힌다
            backdrop.color = new Color(0.06f, 0.05f, 0.09f, 0.88f);

            // 보스 그림자 — 띠 위쪽 바깥에서 다가온다. 그림은 IntermissionScreen이 이 판 보스로 채운다.
            BossShadow bossShadow = BuildBossShadow(root);

            TMP_Text title = EditorUiFactory.Text(
                panel, "Title", new Vector2(0f, 370f), new Vector2(0.5f, 0.5f), 72f,
                TextAlignmentOptions.Center, new Vector2(1000f, 100f), FontStyles.Bold);
            title.color = new Color(0.95f, 0.83f, 0.35f);

            TMP_Text warning = EditorUiFactory.Text(
                panel, "Warning", new Vector2(0f, 300f), new Vector2(0.5f, 0.5f), 36f,
                TextAlignmentOptions.Center, new Vector2(1000f, 90f));
            warning.color = new Color(0.92f, 0.72f, 0.70f);

            // 현황 줄 — 딤에 가려 HUD가 안 읽힌다. 성장을 정하는 화면이니 지갑과 현재 수치를 여기 다시 적는다.
            TMP_Text hp = StatusLine(panel, "HpText", 240f);
            TMP_Text souls = StatusLine(panel, "SoulsText", 198f);
            TMP_Text stats = StatusLine(panel, "StatsText", 156f);

            TMP_Text points = EditorUiFactory.Text(
                panel, "Points", new Vector2(0f, 106f), new Vector2(0.5f, 0.5f), 42f,
                TextAlignmentOptions.Center, new Vector2(1000f, 70f), FontStyles.Bold);
            points.color = new Color(0.95f, 0.83f, 0.35f);

            // 업그레이드 NPC — 성녀(왼쪽, 공/방)와 대장장이(오른쪽, 판정).
            // 성녀 그림은 고른 캐릭터가 런타임에 채우므로 여기서는 비워 둔다.
            Image saint = NpcSlot(panel, "SaintNpc", new Vector2(-400f, -30f), out TextMeshProUGUI saintLabel,
                out NpcReaction saintReaction, "intermission.npc.saint", new Color(1f, 0.95f, 0.75f, 1f));
            Image blacksmith = NpcSlot(panel, "BlacksmithNpc", new Vector2(400f, -30f), out TextMeshProUGUI smithLabel,
                out NpcReaction smithReaction, "intermission.npc.blacksmith", new Color(1f, 0.72f, 0.35f, 1f));

            Button fight = EditorUiFactory.Button(
                panel, "FightButton", new Vector2(0f, -210f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(520f, 120f), AccentButtonColor, "intermission.fight", 52f,
                out _, out TextMeshProUGUI fightLabel);
            EditorUiFactory.Localize(fightLabel, "intermission.fight");

            // 분배 버튼 3개는 FIGHT '아래'
            BuildStatAllocation(panel, -355f);

            // 가로에서는 화면이 낮으므로 띠도 낮게 (세부 배치는 씬에서 드래그로 잡는다)
            EditorUiFactory.Orient(panel, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 720f));

            var so = new SerializedObject(screen);
            Set(so, "panelRoot", root.gameObject);
            Set(so, "dimOverlay", dim);
            Set(so, "bossShadow", bossShadow);
            Set(so, "titleText", title);
            Set(so, "warningText", warning);
            Set(so, "pointsText", points);
            Set(so, "hpText", hp);
            Set(so, "soulsText", souls);
            Set(so, "statsText", stats);
            Set(so, "fightButton", fight);
            Set(so, "saintImage", saint);
            Set(so, "saintLabel", saintLabel);
            Set(so, "saintReaction", saintReaction);
            Set(so, "blacksmithImage", blacksmith);
            Set(so, "blacksmithLabel", smithLabel);
            Set(so, "blacksmithReaction", smithReaction);

            // 캐릭터와 무관한 그림은 여기서 기본값을 깔아 준다 — 씬에서 바꾸면 그게 이긴다.
            Set(so, "fallbackSaintSprite", DotSprite("darksouls_saint"));
            so.ApplyModifiedPropertiesWithoutUndo();

            Sprite smithSprite = DotSprite("blacksmith");
            if (smithSprite != null)
            {
                blacksmith.sprite = smithSprite;
                blacksmith.color = Color.white;
            }

            EditorUtility.SetDirty(screen);
        }

        /// <summary>DotImgs의 시트에서 가장 큰 조각(=본체)을 가져온다. 없으면 null.</summary>
        private static Sprite DotSprite(string textureName)
        {
            Sprite best = null;
            float bestArea = 0f;

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath($"Assets/_Project/DotImgs/{textureName}.png"))
            {
                if (asset is not Sprite sprite)
                    continue;

                float area = sprite.rect.width * sprite.rect.height;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = sprite;
                }
            }

            if (best == null)
                Debug.LogWarning($"[MainSceneBuilder] '{textureName}.png' 에서 스프라이트를 찾지 못했습니다.");

            return best;
        }

        /// <summary>
        /// 스탯 분배 버튼 3종. 준비 화면의 FIGHT <b>아래</b>에 붙는다 —
        /// 퍼즐 중에는 보드만 보면 되고, 성장은 시간에 안 쫓기는 이 구간에서 정한다.
        /// </summary>
        private static void BuildStatAllocation(Transform panel, float y)
        {
            RectTransform group = EditorUiFactory.NewRect("StatAllocation", panel);
            group.anchorMin = group.anchorMax = group.pivot = new Vector2(0.5f, 0.5f);
            group.anchoredPosition = new Vector2(0f, y);
            group.sizeDelta = new Vector2(1000f, 170f);

            Button atk = AllocButton(group, "AllocAttack", -330f, "+ATK");
            Button def = AllocButton(group, "AllocDefense", 0f, "+DEF");
            Button parry = AllocButton(group, "AllocParry", 330f, "+PARRY");

            var allocation = group.gameObject.AddComponent<StatAllocationPanel>();
            var so = new SerializedObject(allocation);
            Set(so, "gameManager", Object.FindFirstObjectByType<ChainRiposte.Game.GameManager>());
            Set(so, "attackButton", atk);
            Set(so, "defenseButton", def);
            Set(so, "parryButton", parry);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>현황 한 줄 — 코드가 채우므로 LocalizedText는 붙이지 않는다.</summary>
        private static TMP_Text StatusLine(Transform parent, string name, float y)
        {
            TextMeshProUGUI text = EditorUiFactory.Text(
                parent, name, new Vector2(0f, y), new Vector2(0.5f, 0.5f), 36f,
                TextAlignmentOptions.Center, new Vector2(1000f, 50f));
            text.color = new Color(0.80f, 0.78f, 0.74f);
            return text;
        }

        private static Button AllocButton(Transform parent, string name, float x, string placeholder) =>
            EditorUiFactory.Button(
                parent, name, new Vector2(x, 0f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(300f, 150f), PanelButtonColor, placeholder, 40f, out _, out _);

        /// <summary>
        /// NPC 자리 — 그림 + 이름표 + 강화 반응.
        /// 스프라이트는 <b>비워 둔다</b>(그래야 IntermissionScreen이 자리 색으로 칠하고,
        /// 성녀는 고른 캐릭터 그림으로 채운다). 이름표는 그림과 같이 움직이면 안 되므로 형제로 둔다.
        /// </summary>
        private static Image NpcSlot(
            Transform parent, string name, Vector2 position,
            out TextMeshProUGUI label, out NpcReaction reaction, string locKey, Color flash)
        {
            RectTransform slot = EditorUiFactory.NewRect(name, parent);
            slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = position;
            slot.sizeDelta = new Vector2(280f, 260f);

            RectTransform bodyRect = EditorUiFactory.NewRect("Body", slot);
            bodyRect.anchorMin = bodyRect.anchorMax = bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.anchoredPosition = new Vector2(0f, 20f);
            bodyRect.sizeDelta = new Vector2(180f, 180f);

            var image = bodyRect.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            reaction = bodyRect.gameObject.AddComponent<NpcReaction>();
            var reactionSo = new SerializedObject(reaction);
            Set(reactionSo, "body", bodyRect);
            Set(reactionSo, "tintTarget", image);
            reactionSo.FindProperty("flashColor").colorValue = flash;
            reactionSo.ApplyModifiedPropertiesWithoutUndo();

            label = EditorUiFactory.Text(
                slot, "Label", new Vector2(0f, -100f), new Vector2(0.5f, 0.5f), 32f,
                TextAlignmentOptions.Center, new Vector2(280f, 50f));
            EditorUiFactory.Localize(label, locKey);
            return image;
        }

        // ── 공통 ──

        /// <summary>대상 GameObject를 Canvas로 세팅하고 기존 자식을 비운 뒤, 컨텐츠를 붙일 부모를 돌려준다.</summary>
        private static Transform PrepareCanvas(GameObject go, int sortingOrder)
        {
            for (int i = go.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(go.transform.GetChild(i).gameObject);
            EditorUiFactory.SetupCanvas(go, sortingOrder);
            return go.transform;
        }

        private static void Set(SerializedObject so, string propName, Object value)
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop == null)
            {
                Debug.LogError($"[MainSceneBuilder] 직렬화 필드 '{propName}' 를 찾지 못했습니다. 필드명이 바뀌었는지 확인하세요.");
                return;
            }
            prop.objectReferenceValue = value;
        }
    }
}
