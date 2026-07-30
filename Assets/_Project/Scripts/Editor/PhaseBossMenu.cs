using ChainRiposte.Game.Config;
using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 월드2의 보스를 <b>2페이즈로 싸우는 판</b>(2-3)과 1페이즈로 싸우는 판(2-1·2-2)으로 갈라 주는 툴.
    ///
    /// <para><b>보스 에셋은 하나다</b>(사용자 결정 2026-07-30). 예전에는 <c>Boss_02</c>를 복사한
    /// <c>Boss_03</c>에 페이즈를 붙여 2-3에만 배정했는데, 같은 보스가 에셋 둘로 갈려 있어
    /// <b>그 보스의 채보·수치를 두 곳에 똑같이 적어야</b> 했다(한쪽만 고치면 조용히 어긋난다).
    /// 이제 "몇 페이즈로 싸우나"는 보스가 아니라 <b>판</b>이 정한다 —
    /// <c>StageDataSO.battlePhaseLimit</c>.</para>
    ///
    /// <para>이 메뉴가 하는 일: 공용 보스에 인살 페이즈 2줄과 그림 슬롯을 <b>모자란 만큼만</b> 채우고,
    /// 2-1·2-2는 1페이즈, 2-3은 전부로 맞춘다. <b>이미 적힌 값은 안 건드린다</b>
    /// (테마·캐릭터 생성 메뉴와 같은 규칙).</para>
    /// </summary>
    public static class PhaseBossMenu
    {
        private const string DataFolder = "Assets/_Project/Data";

        /// <summary>월드2 세 판이 공유하는 보스.</summary>
        private const string BossPath = DataFolder + "/Boss_02.asset";

        /// <summary>전환 컷씬 문구의 기본 키. 보스마다 바꿔도 된다.</summary>
        private const string TransitionKey = "boss.phase.transition";

        /// <summary>2페이즈는 더 무겁게. 어차피 인스펙터에서 조절할 값이라 출발점만 준다.</summary>
        private const float Phase2HpFactor = 1.4f;
        private const float Phase2PostureFactor = 1.2f;

        [MenuItem("Tools/ChainRiposte/Setup Two-Phase Boss (2-3)")]
        private static void Setup()
        {
            var boss = AssetDatabase.LoadAssetAtPath<BossDataSO>(BossPath);
            if (boss == null)
            {
                EditorUtility.DisplayDialog("2페이즈 보스 설정", $"{BossPath} 를 찾지 못했습니다.", "확인");
                return;
            }

            var so = new SerializedObject(boss);
            int added = EnsureTwoPhases(so);
            EnsurePhaseVisualSlots(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(boss);

            // 앞의 두 판은 1페이즈, 마지막 판만 전부. 보스 참조도 공용 보스로 맞춘다.
            SetStage("Stage_2_1", boss, phaseLimit: 1);
            SetStage("Stage_2_2", boss, phaseLimit: 1);
            SetStage("Stage_2_3", boss, phaseLimit: 0);

            AssetDatabase.SaveAssets();
            Selection.activeObject = boss;
            Debug.Log($"[PhaseBoss] {BossPath} — 인살 페이즈 {added}줄 추가(이미 있던 값은 유지). " +
                      "2-1·2-2 = 1페이즈, 2-3 = 전부로 맞췄습니다. " +
                      "그림(phase1/trans/phase2)은 캐릭터별 겉모습의 「인살 페이즈별 그림」에 꽂고, " +
                      "2페이즈 채보는 「인살 페이즈 ▸ Hp Phases」에 따로 짜세요(비우면 1페이즈와 같은 채보를 씁니다).");
        }

        /// <summary>
        /// 인살 페이즈를 두 줄로 맞춘다. <b>이미 적힌 값은 절대 안 건드리고 모자란 줄과 빈 칸만 채운다</b> —
        /// 손으로 만든 에셋(줄이 하나뿐이거나 텅 빈 것)도 이 메뉴 한 번으로 고쳐지게 하기 위한 것이다.
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

        /// <summary>그 판이 이 보스를 몇 페이즈로 싸울지 적는다. 이미 그렇게 돼 있으면 아무것도 안 한다.</summary>
        private static void SetStage(string stageName, BossDataSO boss, int phaseLimit)
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDataSO>($"{DataFolder}/{stageName}.asset");
            if (stage == null)
                return;

            var so = new SerializedObject(stage);
            SerializedProperty bossField = so.FindProperty("bossData");
            SerializedProperty limitField = so.FindProperty("battlePhaseLimit");
            if (bossField.objectReferenceValue == boss && limitField.intValue == phaseLimit)
                return;

            bossField.objectReferenceValue = boss;
            limitField.intValue = phaseLimit;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
            Debug.Log($"[PhaseBoss] {stageName}: 보스 = {boss.name}, 인살 페이즈 = " +
                      (phaseLimit == 0 ? "전부" : phaseLimit.ToString()));
        }
    }
}
