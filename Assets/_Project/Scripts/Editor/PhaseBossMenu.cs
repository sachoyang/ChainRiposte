using ChainRiposte.Game.Config;
using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// <b>2페이즈 보스</b>(인살 두 번) 에셋을 만들어 2-3에 배정하는 툴.
    ///
    /// <para>2페이즈 보스를 <b>따로 관리</b>하기 위한 것이다(사용자 결정) — 지금 <c>Boss_02</c> 하나를
    /// 2-1·2-2·2-3이 공유하므로, 거기에 페이즈를 붙이면 월드2 세 판이 전부 2페이즈가 된다.</para>
    ///
    /// <para>수치·채보는 <c>Boss_02</c>를 복사해 출발점으로 삼는다. <b>이미 있으면 아무것도 안 한다</b> —
    /// 손으로 맞춰 둔 값을 덮어쓰지 않기 위해서다(테마·캐릭터 생성 메뉴와 같은 규칙).</para>
    /// </summary>
    public static class PhaseBossMenu
    {
        private const string DataFolder = "Assets/_Project/Data";
        private const string SourcePath = DataFolder + "/Boss_02.asset";
        private const string TargetPath = DataFolder + "/Boss_03.asset";
        private const string StagePath = DataFolder + "/Stage_2_3.asset";

        /// <summary>전환 컷씬 문구의 기본 키. 보스마다 바꿔도 된다.</summary>
        private const string TransitionKey = "boss.phase.transition";

        /// <summary>2페이즈는 더 무겁게. 어차피 인스펙터에서 조절할 값이라 출발점만 준다.</summary>
        private const float Phase2HpFactor = 1.4f;
        private const float Phase2PostureFactor = 1.2f;

        [MenuItem("Tools/ChainRiposte/Create Two-Phase Boss (2-3)")]
        private static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<BossDataSO>(TargetPath) != null)
            {
                Debug.Log($"[PhaseBoss] {TargetPath} 가 이미 있어 그대로 둡니다. " +
                          "수치·그림은 인스펙터에서 조절하세요.");
                AssignToStage();
                return;
            }

            var source = AssetDatabase.LoadAssetAtPath<BossDataSO>(SourcePath);
            if (source == null)
            {
                EditorUtility.DisplayDialog("2페이즈 보스 만들기",
                    $"{SourcePath} 를 찾지 못했습니다.", "확인");
                return;
            }

            if (!AssetDatabase.CopyAsset(SourcePath, TargetPath))
            {
                EditorUtility.DisplayDialog("2페이즈 보스 만들기", "에셋 복사에 실패했습니다.", "확인");
                return;
            }

            var boss = AssetDatabase.LoadAssetAtPath<BossDataSO>(TargetPath);
            var so = new SerializedObject(boss);

            so.FindProperty("bossId").stringValue = "Boss_03";
            so.FindProperty("displayName").stringValue = "Two-Phase Boss";

            float maxHp = so.FindProperty("maxHp").floatValue;
            float maxPosture = so.FindProperty("maxPosture").floatValue;

            BuildBattlePhases(so, maxHp, maxPosture);
            AddPhaseVisualSlots(so);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(boss);
            AssetDatabase.SaveAssets();

            AssignToStage();

            Selection.activeObject = boss;
            Debug.Log($"[PhaseBoss] {TargetPath} 생성 — 인살 2회. " +
                      "그림(phase1/trans/phase2)은 캐릭터별 겉모습의 「인살 페이즈별 그림」에 꽂고, " +
                      "2페이즈 채보는 「인살 페이즈 ▸ Hp Phases」에 따로 짜세요(비우면 1페이즈와 같은 채보를 씁니다).");
        }

        /// <summary>
        /// 인살 페이즈 두 줄. 1페이즈는 공용 수치를 그대로 쓰고(칸을 비운다),
        /// 2페이즈만 더 무겁게 적어 둔다 — 같은 숫자를 두 번 적으면 한쪽만 고치는 사고가 난다.
        /// </summary>
        private static void BuildBattlePhases(SerializedObject so, float maxHp, float maxPosture)
        {
            SerializedProperty phases = so.FindProperty("battlePhases");
            phases.arraySize = 2;

            SerializedProperty first = phases.GetArrayElementAtIndex(0);
            first.FindPropertyRelative("label").stringValue = "Phase 1";
            first.FindPropertyRelative("maxHp").floatValue = 0f;      // 0 = 공용값
            first.FindPropertyRelative("maxPosture").floatValue = 0f;
            first.FindPropertyRelative("transitionTextKey").stringValue = string.Empty; // 첫 페이즈는 넘어올 일이 없다
            first.FindPropertyRelative("hpPhases").arraySize = 0;
            first.FindPropertyRelative("sprite").objectReferenceValue = null;
            first.FindPropertyRelative("transitionSprite").objectReferenceValue = null;

            SerializedProperty second = phases.GetArrayElementAtIndex(1);
            second.FindPropertyRelative("label").stringValue = "Phase 2";
            second.FindPropertyRelative("maxHp").floatValue = Mathf.Round(maxHp * Phase2HpFactor);
            second.FindPropertyRelative("maxPosture").floatValue = Mathf.Round(maxPosture * Phase2PostureFactor);
            second.FindPropertyRelative("transitionTextKey").stringValue = TransitionKey;
            second.FindPropertyRelative("hpPhases").arraySize = 0;
            second.FindPropertyRelative("sprite").objectReferenceValue = null;
            second.FindPropertyRelative("transitionSprite").objectReferenceValue = null;
        }

        /// <summary>캐릭터별 겉모습마다 페이즈 그림 슬롯 2칸을 열어 둔다 — 비어 있으면 공용 그림으로 떨어진다.</summary>
        private static void AddPhaseVisualSlots(SerializedObject so)
        {
            SerializedProperty visuals = so.FindProperty("characterVisuals");
            for (int i = 0; i < visuals.arraySize; i++)
            {
                SerializedProperty phaseVisuals = visuals.GetArrayElementAtIndex(i).FindPropertyRelative("phaseVisuals");
                phaseVisuals.arraySize = 2;
                for (int p = 0; p < 2; p++)
                {
                    SerializedProperty entry = phaseVisuals.GetArrayElementAtIndex(p);
                    entry.FindPropertyRelative("sprite").objectReferenceValue = null;
                    entry.FindPropertyRelative("transitionSprite").objectReferenceValue = null;
                }
            }
        }

        /// <summary>2-3만 새 보스로 바꾼다. 2-1·2-2는 건드리지 않는다.</summary>
        private static void AssignToStage()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDataSO>(StagePath);
            var boss = AssetDatabase.LoadAssetAtPath<BossDataSO>(TargetPath);
            if (stage == null || boss == null)
                return;

            var so = new SerializedObject(stage);
            SerializedProperty field = so.FindProperty("bossData");
            if (field.objectReferenceValue == boss)
                return;

            field.objectReferenceValue = boss;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PhaseBoss] {StagePath} 의 보스를 Boss_03 으로 바꿨습니다.");
        }
    }
}
