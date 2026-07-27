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
            var boss = AssetDatabase.LoadAssetAtPath<BossDataSO>(TargetPath);
            bool created = boss == null;

            if (created)
            {
                if (AssetDatabase.LoadAssetAtPath<BossDataSO>(SourcePath) == null)
                {
                    EditorUtility.DisplayDialog("2페이즈 보스 만들기", $"{SourcePath} 를 찾지 못했습니다.", "확인");
                    return;
                }

                if (!AssetDatabase.CopyAsset(SourcePath, TargetPath))
                {
                    EditorUtility.DisplayDialog("2페이즈 보스 만들기", "에셋 복사에 실패했습니다.", "확인");
                    return;
                }

                boss = AssetDatabase.LoadAssetAtPath<BossDataSO>(TargetPath);
            }

            var so = new SerializedObject(boss);

            if (created)
            {
                so.FindProperty("bossId").stringValue = "Boss_03";
                so.FindProperty("displayName").stringValue = "Two-Phase Boss";
            }

            int added = EnsureTwoPhases(so);
            EnsurePhaseVisualSlots(so);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(boss);
            AssetDatabase.SaveAssets();

            AssignToStage();

            Selection.activeObject = boss;
            Debug.Log($"[PhaseBoss] {TargetPath} — {(created ? "생성" : "이미 있어 모자란 것만 채움")}, " +
                      $"인살 페이즈 {added}줄 추가. " +
                      "그림(phase1/trans/phase2)은 캐릭터별 겉모습의 「인살 페이즈별 그림」에 꽂고, " +
                      "2페이즈 채보는 「인살 페이즈 ▸ Hp Phases」에 따로 짜세요(비우면 1페이즈와 같은 채보를 씁니다).");
        }

        /// <summary>
        /// 인살 페이즈를 두 줄로 맞춘다. <b>이미 적힌 값은 절대 안 건드리고 모자란 줄과 빈 칸만 채운다</b> —
        /// 손으로 만든 에셋(줄이 하나뿐이거나 텅 빈 것)도 이 메뉴 한 번으로 고쳐지게 하기 위한 것이다.
        /// 예전엔 "이미 있으면 통째로 지나감"이라 그런 에셋이 조용히 1페이즈 보스로 동작했다.
        ///
        /// <para>1페이즈는 HP·체간 칸을 비워 둔다(= 공용값). 같은 숫자를 두 번 적으면 한쪽만 고치는 사고가 난다.
        /// 2페이즈만 더 무겁게 적어 출발점을 준다.</para>
        /// </summary>
        /// <returns>새로 늘린 줄 수.</returns>
        private static int EnsureTwoPhases(SerializedObject so)
        {
            SerializedProperty phases = so.FindProperty("battlePhases");
            int previous = phases.arraySize;

            if (previous < 2)
            {
                phases.arraySize = 2;
                // 배열을 늘리면 Unity가 <b>직전 원소를 복사</b>한다 — 안 비우면 2페이즈가 1페이즈 그림을 물려받는다
                for (int i = Mathf.Max(previous, 0); i < 2; i++)
                    ClearPhase(phases.GetArrayElementAtIndex(i));
            }

            float maxHp = so.FindProperty("maxHp").floatValue;
            float maxPosture = so.FindProperty("maxPosture").floatValue;

            FillIfBlank(phases.GetArrayElementAtIndex(0), "Phase 1", 0f, 0f, null);
            FillIfBlank(phases.GetArrayElementAtIndex(1), "Phase 2",
                Mathf.Round(maxHp * Phase2HpFactor), Mathf.Round(maxPosture * Phase2PostureFactor), TransitionKey);

            return Mathf.Max(0, phases.arraySize - previous);
        }

        private static void ClearPhase(SerializedProperty entry)
        {
            entry.FindPropertyRelative("label").stringValue = string.Empty;
            entry.FindPropertyRelative("sprite").objectReferenceValue = null;
            entry.FindPropertyRelative("transitionSprite").objectReferenceValue = null;
            entry.FindPropertyRelative("transitionTextKey").stringValue = string.Empty;
            entry.FindPropertyRelative("maxHp").floatValue = 0f;
            entry.FindPropertyRelative("maxPosture").floatValue = 0f;
            entry.FindPropertyRelative("hpPhases").arraySize = 0;
        }

        /// <summary>빈 칸만 채운다. 0이나 빈 문자열이 아니면 손으로 맞춘 값으로 보고 그대로 둔다.</summary>
        private static void FillIfBlank(SerializedProperty entry, string label, float maxHp, float maxPosture, string textKey)
        {
            SerializedProperty labelProp = entry.FindPropertyRelative("label");
            if (string.IsNullOrWhiteSpace(labelProp.stringValue))
                labelProp.stringValue = label;

            SerializedProperty hp = entry.FindPropertyRelative("maxHp");
            if (maxHp > 0f && hp.floatValue <= 0f)
                hp.floatValue = maxHp;

            SerializedProperty posture = entry.FindPropertyRelative("maxPosture");
            if (maxPosture > 0f && posture.floatValue <= 0f)
                posture.floatValue = maxPosture;

            SerializedProperty key = entry.FindPropertyRelative("transitionTextKey");
            if (!string.IsNullOrEmpty(textKey) && string.IsNullOrWhiteSpace(key.stringValue))
                key.stringValue = textKey;
        }

        /// <summary>캐릭터별 겉모습마다 페이즈 그림 슬롯 2칸을 열어 둔다. <b>이미 꽂은 그림은 안 건드린다.</b></summary>
        private static void EnsurePhaseVisualSlots(SerializedObject so)
        {
            SerializedProperty visuals = so.FindProperty("characterVisuals");
            for (int i = 0; i < visuals.arraySize; i++)
            {
                SerializedProperty phaseVisuals = visuals.GetArrayElementAtIndex(i).FindPropertyRelative("phaseVisuals");
                int previous = phaseVisuals.arraySize;
                if (previous >= 2)
                    continue;

                phaseVisuals.arraySize = 2;
                for (int p = previous; p < 2; p++)
                {
                    // 여기도 직전 원소가 복사되므로 비운다 — 안 그러면 2페이즈에 1페이즈 그림이 박힌다
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
