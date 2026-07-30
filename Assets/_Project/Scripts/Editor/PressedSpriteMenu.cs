using System.Collections.Generic;
using System.Text;
using ChainRiposte.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 열려 있는 씬의 버튼에 <b>눌림 그림</b>을 배선한다. Unity 내장 Sprite Swap 이라 실행 코드는 없다 —
    /// 이 툴이 하는 일은 버튼마다 Transition 을 바꾸고 짝이 되는 스프라이트를 꽂는 것뿐이다.
    ///
    /// <para><b>규칙은 이름 하나다</b>: 버튼 그림이 <c>btn_wide</c> 면 <b>같은 텍스처 안의</b>
    /// <c>btn_wide_pressed</c> 를 찾아 꽂는다. 목록을 코드에 적어 두지 않으므로 아트가 늘어도 이 파일은 안 고친다.
    /// 짝이 없는 버튼은 <b>건드리지 않는다</b>(색 틴트 그대로) — 빈 Pressed 슬롯을 넣으면 누를 때 버튼이 사라진다.</para>
    ///
    /// <para><b>비파괴</b>다. 오브젝트를 만들거나 지우지 않고 이미 있는 버튼의 값만 고치므로
    /// 손으로 꽂아 둔 UI 가 날아가지 않는다. 되돌리기(Ctrl+Z)도 된다.</para>
    /// </summary>
    public static class PressedSpriteMenu
    {
        private const string PressedSuffix = "_pressed";

        /// <summary>
        /// 눌림 조각이 없는 이 그림들은 <b>내려앉는 것</b>으로 눌림을 표현한다
        /// (<see cref="PressOffset"/>). 전투의 패링·공격 버튼이 여기 해당한다.
        /// </summary>
        private const string OffsetSpritePrefix = "arrow";

        [MenuItem("Tools/ChainRiposte/UI/Apply Pressed Sprites (Open Scene)")]
        private static void Apply()
        {
            Selectable[] selectables =
                Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var applied = new List<string>();
            var skipped = new List<string>();

            foreach (Selectable selectable in selectables)
                ApplyTo(selectable, applied, skipped);

            // 씬에 직접 놓인 것만 — 프리팹 인스턴스의 것은 Prefabs 메뉴가 원본에 넣는다.
            PauseMenu pause = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (pause != null && !PrefabUtility.IsPartOfPrefabInstance(pause))
                ApplyPauseToggle(pause, applied);

            if (applied.Count > 0)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Report(applied, skipped);
        }

        /// <summary>
        /// 프리팹 안의 버튼에도 같은 규칙을 적용한다.
        /// <para>씬만 훑으면 <b>프리팹 인스턴스에 override 로만 박혀</b> 다른 씬의 같은 프리팹은 안 바뀐다 —
        /// 시스템 메뉴(일시정지·설정)가 지금 프리팹 하나를 두 씬이 공유하므로 원본을 고쳐야 한다.</para>
        /// </summary>
        [MenuItem("Tools/ChainRiposte/UI/Apply Pressed Sprites (Prefabs)")]
        private static void ApplyToPrefabs()
        {
            var applied = new List<string>();
            var skipped = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                int before = applied.Count;

                foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
                    ApplyTo(selectable, applied, skipped, prefabPath: path);

                // 일시정지 토글의 눌림 그림 두 벌도 원본에 넣는다 — 씬 인스턴스에 박으면 다른 씬이 못 받는다.
                ApplyPauseToggle(root.GetComponentInChildren<PauseMenu>(true), applied);

                if (applied.Count > before)
                    PrefabUtility.SaveAsPrefabAsset(root, path);

                PrefabUtility.UnloadPrefabContents(root);
            }

            Report(applied, skipped);
        }

        /// <summary>
        /// 버튼 하나 — 짝이 되는 <c>_pressed</c> 조각이 있으면 Sprite Swap, 없고 <c>arrow_*</c> 면 내려앉기,
        /// 둘 다 아니면 <b>건드리지 않는다</b>(빈 Pressed 슬롯을 넣으면 누를 때 버튼이 사라진다).
        /// </summary>
        private static void ApplyTo(
            Selectable selectable, List<string> applied, List<string> skipped, string prefabPath = null)
        {
            string label = prefabPath == null ? Path(selectable) : $"{System.IO.Path.GetFileName(prefabPath)}:{Path(selectable)}";

            // 프리팹 인스턴스는 원본이 관리한다 — 여기서 고치면 인스턴스 override 로 박혀서
            // 나중에 프리팹을 고쳐도 이 씬만 안 따라온다(시스템 메뉴가 두 씬이 공유하는 프리팹이다).
            if (prefabPath == null && PrefabUtility.IsPartOfPrefabInstance(selectable))
            {
                skipped.Add($"{label} (프리팹이 관리 — Prefabs 메뉴로 처리)");
                return;
            }

            if (selectable.targetGraphic is not Image image || image.sprite == null)
            {
                skipped.Add($"{label} (그림 없음)");
                return;
            }

            Sprite pressed = FindVariant(image.sprite, PressedSuffix);
            if (pressed == null)
            {
                if (image.sprite.name.StartsWith(OffsetSpritePrefix, System.StringComparison.OrdinalIgnoreCase))
                    ApplyPressOffset(selectable, label, applied, prefabPath == null);
                else
                    skipped.Add($"{label} ({image.sprite.name}{PressedSuffix} 없음)");

                return;
            }

            // 프리팹 내용물(LoadPrefabContents)은 씬에 없는 임시 오브젝트라 되돌리기 대상이 아니다.
            if (prefabPath == null)
                Undo.RecordObject(selectable, "Apply Pressed Sprites");

            selectable.transition = Selectable.Transition.SpriteSwap;

            SpriteState state = selectable.spriteState;
            state.pressedSprite = pressed;
            // 하이라이트·선택 상태는 비워 둔다 — 터치에는 마우스 오버가 없고,
            // 채우면 손을 뗀 뒤에도 눌린 그림이 남아 있는 것처럼 보인다.
            state.highlightedSprite = null;
            state.selectedSprite = null;
            selectable.spriteState = state;

            EditorUtility.SetDirty(selectable);
            applied.Add($"{label} → {pressed.name}");
        }

        /// <summary>
        /// 눌림 그림이 없는 화살표 버튼 — <b>색은 지금 그대로</b> 두고(회색 틴트) 몇 픽셀 내려앉게 한다.
        /// 이미 붙어 있으면 그대로 둔다(내려가는 양을 손으로 조절해 뒀을 수 있다).
        /// </summary>
        private static void ApplyPressOffset(Selectable selectable, string label, List<string> applied, bool inScene)
        {
            if (selectable.GetComponent<PressOffset>() != null)
                return;

            if (inScene)
                Undo.AddComponent<PressOffset>(selectable.gameObject);
            else
                selectable.gameObject.AddComponent<PressOffset>();

            EditorUtility.SetDirty(selectable.gameObject);
            applied.Add($"{label} → 내려앉기(PressOffset)");
        }

        /// <summary>
        /// 일시정지 버튼은 아이콘이 ⏸↔▶ 로 바뀌는 <b>토글</b>이라 눌림 그림도 두 벌이다.
        /// 한 벌만 꽂으면 ▶ 상태에서 ⏸의 눌림 그림이 뜬다 — <see cref="PauseMenu"/>가 같이 갈아 끼우도록
        /// 짝을 찾아 채워 준다(여기서도 규칙은 <c>_pressed</c> 하나).
        /// </summary>
        private static void ApplyPauseToggle(PauseMenu pause, List<string> applied)
        {
            if (pause == null)
                return;

            var so = new SerializedObject(pause);
            bool changed = FillVariant(so, "pauseSprite", "pausePressedSprite")
                           | FillVariant(so, "playSprite", "playPressedSprite");
            if (!changed)
                return;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pause);
            applied.Add("PauseMenu (일시정지 토글의 눌림 그림 2종)");
        }

        private static bool FillVariant(SerializedObject so, string sourceField, string targetField)
        {
            SerializedProperty target = so.FindProperty(targetField);
            if (target == null || target.objectReferenceValue != null)
                return false; // 손으로 꽂아 둔 것은 건드리지 않는다

            if (so.FindProperty(sourceField)?.objectReferenceValue is not Sprite source)
                return false;

            Sprite pressed = FindVariant(source, PressedSuffix);
            if (pressed == null)
                return false;

            target.objectReferenceValue = pressed;
            return true;
        }

        /// <summary>같은 텍스처(시트) 안에서 이름에 접미사가 붙은 조각을 찾는다.</summary>
        private static Sprite FindVariant(Sprite sprite, string suffix)
        {
            string wanted = sprite.name + suffix;
            string path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(path))
                return null;

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite candidate && candidate.name == wanted)
                    return candidate;
            }

            return null;
        }

        private static string Path(Component component)
        {
            var builder = new StringBuilder(component.name);
            Transform parent = component.transform.parent;
            while (parent != null)
            {
                builder.Insert(0, parent.name + "/");
                parent = parent.parent;
            }

            return builder.ToString();
        }

        private static void Report(List<string> applied, List<string> skipped)
        {
            var log = new StringBuilder($"[PressedSprite] 배선 {applied.Count}개 / 건너뜀 {skipped.Count}개\n");
            foreach (string line in applied)
                log.Append("  ✔ ").AppendLine(line);
            foreach (string line in skipped)
                log.Append("  · ").AppendLine(line);
            log.Append("건너뛴 버튼은 그림 그대로 두었습니다 — 눌림 조각(<이름>").Append(PressedSuffix)
               .Append(")을 시트에 슬라이스해 두면 다시 실행할 때 붙습니다.");

            // 확인 창을 띄우지 않는다 — 모달은 에디터를 멈춰서 자동 실행(MCP·배치)에서 걸린다.
            Debug.Log(log.ToString());
        }
    }
}
