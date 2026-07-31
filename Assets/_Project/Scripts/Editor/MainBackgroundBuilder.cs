using ChainRiposte.Game.Theming;
using ChainRiposte.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 퍼즐·전투 화면에 <b>테마 배경 자리</b>를 깐다 (비파괴 — 같은 이름의 것만 갈아 끼운다).
    ///
    /// <para><b>왜 필요했나</b>: 테마의 <c>puzzle</c>·<c>combat</c> 키가 비어 있는 것도 문제였지만,
    /// 더 근본적으로 <c>Main</c> 씬에는 <see cref="ThemedSprite"/>가 <b>한 개도 없었다</b> —
    /// 테마에 그림을 넣어도 받을 오브젝트가 없어서 두 화면이 계속 단색이었다.</para>
    ///
    /// <para><b>두 화면은 성격이 다르다.</b> 퍼즐은 보드가 월드 좌표라 배경도 <see cref="SpriteRenderer"/>여야
    /// 카메라와 같이 움직인다. 전투는 통째로 UI 캔버스라 <see cref="Image"/>여야 화면을 덮는다.
    /// 한 종류로 통일할 수 없어서 각각 만든다.</para>
    ///
    /// <para>배경은 <b>안 움직인다</b>(<c>amplitude 0</c>). 퍼즐은 보드를, 전투는 다가오는 원을 읽는 화면이라
    /// 뒤가 흔들리면 방해다 — 타이틀·인트로와 정반대다. 대신 <see cref="BackgroundPanner"/>가
    /// 비율을 지키며 화면을 덮으므로, <b>가로로 긴 그림은 세로 화면에서 가운데만</b> 보인다.</para>
    /// </summary>
    internal static class MainBackgroundBuilder
    {
        private const string PuzzleBgName = "PuzzleBackground";
        private const string CombatBgName = "CombatBackground";

        /// <summary>보드 배경 셀이 -10, 타일 받침이 -1이다. 그보다 확실히 아래.</summary>
        private const int PuzzleBgSort = -200;

        [MenuItem("Tools/ChainRiposte/Add Backgrounds To Main")]
        private static void Build()
        {
            UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.name != "Main")
            {
                EditorUtility.DisplayDialog("배경 깔기", "Main 씬을 열고 실행하세요.", "확인");
                return;
            }

            BuildPuzzleBackground();
            BuildCombatBackground();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                "[MainBackgroundBuilder] 배경 자리를 깔았습니다. 그림은 테마가 채웁니다 — " +
                "Theme_Irithyll/Theme_Ashina 의 backgrounds 에 puzzle·combat 키가 있어야 보입니다.");
        }

        /// <summary>
        /// 퍼즐 배경 — <b>월드</b>. 보드가 월드 좌표라 UI로 깔면 카메라가 보드에 맞춰 움직일 때 어긋난다.
        /// 씬 루트에 두는 이유: 보드(<c>Board</c>)의 자식으로 넣으면 보드 스케일을 물려받아
        /// 판 크기(8×8 / 10×10)마다 배경 크기가 달라진다.
        /// </summary>
        private static void BuildPuzzleBackground()
        {
            GameObject go = FindOrCreateRoot(PuzzleBgName);
            go.transform.position = new Vector3(0f, 0f, 10f); // 보드보다 뒤

            var renderer = Ensure<SpriteRenderer>(go);
            renderer.sortingOrder = PuzzleBgSort;
            renderer.color = Color.white;

            Themed(go, ThemeSO.KeyPuzzle);
            Panner(go);
        }

        /// <summary>
        /// 전투 배경 — <b>UI</b>. <c>CombatScreen/Root</c>의 <b>맨 앞 자식</b>으로 넣는다.
        /// uGUI는 형제 순서대로 그리므로 맨 앞이 곧 맨 뒤다 — 뒤에 넣으면 보스와 게이지를 덮는다.
        /// </summary>
        private static void BuildCombatBackground()
        {
            Transform root = FindCombatRoot();
            if (root == null)
            {
                Debug.LogWarning("[MainBackgroundBuilder] CombatScreen/Root 를 찾지 못해 전투 배경은 건너뜁니다.");
                return;
            }

            Transform existing = root.Find(CombatBgName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(CombatBgName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Add Backgrounds");
                go.transform.SetParent(root, false);
            }

            go.transform.SetSiblingIndex(0); // 맨 뒤에 그려지도록

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = Ensure<Image>(go);
            image.color = Color.white;
            // 배경이 입력을 먹으면 그 위의 패링·공격 버튼이 안 눌리는 화면이 생긴다
            image.raycastTarget = false;

            Themed(go, ThemeSO.KeyCombat);
            Panner(go);
        }

        private static Transform FindCombatRoot()
        {
            foreach (GameObject root in UnityEditor.SceneManagement.EditorSceneManager
                         .GetActiveScene().GetRootGameObjects())
            {
                if (root.name != "CombatScreen")
                    continue;
                return root.transform.Find("Root");
            }

            return null;
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            foreach (GameObject root in UnityEditor.SceneManagement.EditorSceneManager
                         .GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name)
                    return root;
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Add Backgrounds");
            return go;
        }

        /// <summary>테마가 그림을 채우게 한다. 키가 테마에 없으면 그림이 안 바뀔 뿐 화면은 안 깨진다.</summary>
        private static void Themed(GameObject go, string key)
        {
            var themed = Ensure<ThemedSprite>(go);
            var so = new SerializedObject(themed);
            so.FindProperty("backgroundKey").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 비율을 지키며 화면을 덮게 한다. <b>흔들지는 않는다</b>(amplitude 0) —
        /// 퍼즐은 보드를, 전투는 다가오는 원을 읽는 화면이라 뒤가 움직이면 방해다.
        /// </summary>
        private static void Panner(GameObject go)
        {
            var panner = Ensure<BackgroundPanner>(go);
            var so = new SerializedObject(panner);
            so.FindProperty("amplitude").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }
    }
}
