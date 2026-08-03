using System.Collections.Generic;
using ChainRiposte.Game.Config;
using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// <b>첫 등반 튜토리얼</b>(<c>Docs/TUTORIAL.md</c> §4)의 에셋을 만든다.
    ///
    /// <para><b>비어 있는 것만 채운다</b> — 손으로 고친 값(문구·영상·보드)을 덮어쓰지 않는다.
    /// <c>Create Default Characters</c>·<c>Create Default Themes</c>와 같은 규칙이다.</para>
    ///
    /// <para>씬 쪽 배선(<c>TutorialDirector</c>·건너뛰기 버튼)은 이미 Main 씬에 실물로 있다.
    /// 다시 까는 메뉴는 걷어냈다 — 손으로 맞춘 배선을 되돌릴 뿐이었다.</para>
    /// </summary>
    public static class TutorialClimbMenu
    {
        private const string TopicFolder = "Assets/_Project/Data/Tutorial";
        private const string StagePath = "Assets/_Project/Data/Stage_Tutorial.asset";
        private const string SourceStagePath = "Assets/_Project/Data/Stage_1_1.asset";

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

        private static void Directory(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Tutorial");
        }
    }
}
