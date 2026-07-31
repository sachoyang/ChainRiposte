using System.Collections.Generic;
using ChainRiposte.Game;
using ChainRiposte.Game.Combat;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.Flow;
using ChainRiposte.Game.Puzzle;
using ChainRiposte.Game.Tutorial;
using ChainRiposte.Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// <b>첫 등반 튜토리얼</b>(<c>Docs/TUTORIAL.md</c> §4)의 에셋과 씬 배선을 깐다.
    ///
    /// <para>둘 다 <b>비어 있는 것만 채운다</b> — 손으로 고친 값(문구·영상·보드)을 덮어쓰지 않는다.
    /// <c>Create Default Characters</c>·<c>Create Default Themes</c>와 같은 규칙이다.</para>
    /// </summary>
    public static class TutorialClimbMenu
    {
        private const string TopicFolder = "Assets/_Project/Data/Tutorial";
        private const string StagePath = "Assets/_Project/Data/Stage_Tutorial.asset";
        private const string SourceStagePath = "Assets/_Project/Data/Stage_1_1.asset";
        private const string DirectorName = "TutorialDirector";

        /// <summary>(에셋 이름, 세이브 id, 현지화 키 뿌리).</summary>
        private static readonly (string asset, string id, string key)[] Steps =
        {
            ("Tutorial_Climb_Why", "climb.why", "tutorial.climb.why"),
            ("Tutorial_Climb_Match", "climb.match", "tutorial.climb.match"),
            ("Tutorial_Climb_Enrage", "climb.enrage", "tutorial.climb.enrage"),
            ("Tutorial_Climb_Boss", "climb.boss", "tutorial.climb.boss"),
            ("Tutorial_Climb_Parry", "climb.parry", "tutorial.climb.parry"),
            ("Tutorial_Climb_Whiff", "climb.whiff", "tutorial.climb.whiff"),
            ("Tutorial_Climb_Execute", "climb.execute", "tutorial.climb.execute"),
            ("Tutorial_Climb_Chain", "climb.chain", "tutorial.climb.chain"),
        };

        [MenuItem("Tools/ChainRiposte/Tutorial/Create First Climb Assets")]
        private static void CreateAssets()
        {
            Directory(TopicFolder);

            var topics = new List<TutorialTopicSO>();
            foreach ((string asset, string id, string key) in Steps)
                topics.Add(EnsureTopic(asset, id, key));

            StageDataSO stage = EnsureStage(topics[0]);
            AssetDatabase.SaveAssets();

            Selection.activeObject = stage;
            EditorGUIUtility.PingObject(stage);
            Debug.Log($"[Tutorial] 첫 등반 에셋을 준비했습니다 — 항목 {topics.Count}개 + {StagePath}.\n" +
                      "다음: ①Stage_Tutorial 의 보드·스폰 대본을 손으로 짜고 " +
                      "②Tools ▸ ChainRiposte ▸ Tutorial ▸ Add Tutorial Director To Main 실행 " +
                      "③Title 씬의 TitleController ▸ Tutorial Stage 에 이 에셋을 꽂으세요.");
        }

        /// <summary>
        /// 항목 하나. <b>이미 있으면 손대지 않는다</b> — 문구 키나 영상을 손으로 갈아 끼웠을 수 있다.
        /// </summary>
        private static TutorialTopicSO EnsureTopic(string assetName, string id, string keyRoot)
        {
            string path = $"{TopicFolder}/{assetName}.asset";
            var topic = AssetDatabase.LoadAssetAtPath<TutorialTopicSO>(path);
            if (topic != null)
                return topic;

            topic = ScriptableObject.CreateInstance<TutorialTopicSO>();
            AssetDatabase.CreateAsset(topic, path);

            var so = new SerializedObject(topic);
            so.FindProperty("topicId").stringValue = id;
            so.FindProperty("titleKey").stringValue = $"{keyRoot}.title";
            so.FindProperty("bodyKey").stringValue = $"{keyRoot}.body";
            so.ApplyModifiedPropertiesWithoutUndo();
            return topic;
        }

        /// <summary>
        /// <c>Stage_Tutorial</c>. <b><c>Stage_01</c>을 재활용하지 않는다</b> (§4.2) — 그쪽은 실험용이라
        /// 보스 시계가 15초이고 지도를 안 거치면 <c>IsBossFinale</c>이 항상 true라 기억이 바로 떨어진다.
        /// 1-1을 원본으로 복사해 타일 목록·기믹 수치를 물려받고, 튜토리얼에 필요한 것만 덮어쓴다.
        /// </summary>
        private static StageDataSO EnsureStage(TutorialTopicSO whyCard)
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDataSO>(StagePath);
            if (stage == null)
            {
                if (!AssetDatabase.CopyAsset(SourceStagePath, StagePath))
                {
                    Debug.LogError($"[Tutorial] {SourceStagePath} 를 복사하지 못했습니다.");
                    return null;
                }

                stage = AssetDatabase.LoadAssetAtPath<StageDataSO>(StagePath);
            }

            var so = new SerializedObject(stage);
            so.FindProperty("stageId").stringValue = "Stage_Tutorial";
            so.FindProperty("runsFirstClimbTutorial").boolValue = true;
            so.FindProperty("fixedBoard").boolValue = true;

            // 씨앗이 0이면 매번 다른 판이 된다 — 튜토리얼이 "저 칸을 저기로"라고 말할 수 없다.
            SerializedProperty seed = so.FindProperty("boardSeed");
            if (seed.intValue == 0)
                seed.intValue = 1337;

            // 첫 카드(왜 오르는가)는 판 시작 직전에 뜬다 — ①과 같은 길이라 별도 코드가 필요 없다.
            //
            // 원본(1-1)에서 딸려 온 ① 기믹 카드들은 여기서 <b>걷어낸다</b>. 튜토리얼 판에서는 ②가
            // 같은 것을 플레이로 가르치므로, 그대로 두면 읽고 나서 또 배우게 된다.
            SerializedProperty introduces = so.FindProperty("introduces");
            if (!ContainsCard(introduces, whyCard))
            {
                introduces.arraySize = 1;
                introduces.GetArrayElementAtIndex(0).objectReferenceValue = whyCard;
            }

            // 배울 시간을 준다 — 1-1의 90초로는 카드를 읽는 사이에 보스가 내려온다.
            SerializedProperty engage = so.FindProperty("bossEngageSeconds");
            if (engage.floatValue < 150f)
                engage.floatValue = 180f;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
            return stage;
        }

        /// <summary>이미 손으로 짜 놓은 목록인가 — 그러면 안 건드린다(재실행이 무해해야 한다).</summary>
        private static bool ContainsCard(SerializedProperty array, TutorialTopicSO card)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == card)
                    return true;
            }

            return false;
        }

        // ── 씬 배선 ───────────────────────────────────────────────────

        /// <summary>
        /// <b>비파괴</b>다 — <c>TutorialDirector</c> 오브젝트 하나만 만들거나 다시 만든다.
        /// 건너뛰기 버튼은 소개 카드 캔버스(정렬 19) 안에 둔다 — 카드보다 위에 있을 필요는 없고,
        /// 오히려 카드가 떠 있는 동안에는 가려져야 한다(카드를 먼저 읽고 나서 넘길지 정한다).
        /// </summary>
        [MenuItem("Tools/ChainRiposte/Tutorial/Add Tutorial Director To Main")]
        private static void AddDirector()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            var card = Object.FindFirstObjectByType<TutorialCard>(FindObjectsInactive.Include);
            if (manager == null || card == null)
            {
                EditorUtility.DisplayDialog("튜토리얼 진행자 추가",
                    "GameManager 또는 TutorialCard 를 찾지 못했습니다.\n" +
                    "전투 씬(Main)을 열고, 먼저 Add Tutorial Card To Main 을 실행하세요.", "확인");
                return;
            }

            var existing = GameObject.Find(DirectorName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var go = new GameObject(DirectorName);
            Undo.RegisterCreatedObjectUndo(go, "Add Tutorial Director");
            var director = go.AddComponent<TutorialDirector>();

            Button skip = BuildSkipButton(card);

            var so = new SerializedObject(director);
            so.FindProperty("gameManager").objectReferenceValue = manager;
            so.FindProperty("puzzle").objectReferenceValue = Object.FindFirstObjectByType<PuzzleController>(FindObjectsInactive.Include);
            so.FindProperty("combat").objectReferenceValue = Object.FindFirstObjectByType<CombatController>(FindObjectsInactive.Include);
            so.FindProperty("boardView").objectReferenceValue = Object.FindFirstObjectByType<BoardView>(FindObjectsInactive.Include);
            so.FindProperty("puzzleInput").objectReferenceValue = Object.FindFirstObjectByType<PuzzleInput>(FindObjectsInactive.Include);
            so.FindProperty("combatInput").objectReferenceValue = Object.FindFirstObjectByType<CombatInput>(FindObjectsInactive.Include);
            so.FindProperty("card").objectReferenceValue = card;
            so.FindProperty("skipButton").objectReferenceValue = skip;

            string[] fields = { "stepMatch", "stepEnrage", "stepBossTile", "stepParry", "stepWhiff", "stepExecute", "stepChain" };
            for (int i = 0; i < fields.Length; i++)
            {
                // Steps[0] 은 판 시작 카드라 진행자가 안 든다 — 그래서 하나씩 밀어 읽는다.
                var topic = AssetDatabase.LoadAssetAtPath<TutorialTopicSO>($"{TopicFolder}/{Steps[i + 1].asset}.asset");
                so.FindProperty(fields[i]).objectReferenceValue = topic;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            Debug.Log("[Tutorial] 진행자를 얹고 배선했습니다. 도는 조건은 " +
                      "Stage_Tutorial ▸ 첫 등반 튜토리얼 + 아직 안 본 것 둘 다입니다. " +
                      "다시 보려면 Tools ▸ ChainRiposte ▸ Progress ▸ Reset Tutorial.");
        }

        /// <summary>
        /// 건너뛰기. <b>반드시 넣는다</b> — 없으면 개발자 자신이 매번 다시 본다(§4.7).
        /// 카드 캔버스 안에 두므로 카드가 떠 있는 동안에는 딤 아래에 깔려 안 눌린다.
        /// </summary>
        private static Button BuildSkipButton(TutorialCard card)
        {
            Transform canvas = card.transform;
            Transform old = canvas.Find("SkipTutorial");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            Button button = EditorUiFactory.Button(
                canvas, "SkipTutorial", new Vector2(-40f, -40f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(260f, 90f), Color.white, string.Empty, 34f,
                out Image _, out TextMeshProUGUI label);
            EditorUiFactory.Localize(label, "tutorial.skip");
            return button;
        }

        private static void Directory(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Tutorial");
        }
    }
}
