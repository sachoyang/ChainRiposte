using System.IO;
using ChainRiposte.Game.Localization;
using ChainRiposte.Game.Map;
using ChainRiposte.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 일시정지·설정 클러스터(<c>PauseCanvas</c>)를 <b>프리팹 하나로</b> 만들고 다른 씬에 얹는 툴.
    ///
    /// <para>왜 프리팹인가: 전투 씬과 월드맵이 <b>같은 메뉴</b>를 써야 하는데, 씬마다 따로 만들면
    /// 아이콘을 갈아 끼울 때 두 곳을 손대야 하고 한쪽만 고쳐진다(이 프로젝트에서 이미 여러 번 났던 사고다).
    /// 프리팹이 원본이고 씬은 그 인스턴스일 뿐이라, 아이콘·색·배치를 프리팹에서 한 번만 고치면 된다.</para>
    ///
    /// <para>씬마다 다른 것은 <b>「나가기」 목적지</b>뿐이다 — 전투 씬은 지도로, 지도는 타이틀로.
    /// 그 한 줄만 인스턴스에서 덮어쓴다(<see cref="PauseMenu"/>의 <c>quitSceneName</c>).</para>
    /// </summary>
    public static class SystemMenuPrefabMenu
    {
        private const string CanvasName = "PauseCanvas";
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string PrefabPath = PrefabFolder + "/SystemMenuCanvas.prefab";

        private const string OptionsPanelName = "OptionsPanel";

        /// <summary>설정 패널 원본. 타이틀·전투 씬이 <b>같은 이것</b>의 인스턴스를 쓴다.</summary>
        internal const string OptionsPanelPrefabPath = PrefabFolder + "/OptionsPanel.prefab";

        [MenuItem("Tools/ChainRiposte/System Menu/Extract Prefab From Open Scene")]
        private static void Extract()
        {
            var pause = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (pause == null)
            {
                EditorUtility.DisplayDialog("시스템 메뉴 프리팹",
                    "이 씬에서 PauseMenu 를 찾지 못했습니다. Main 씬을 열고 다시 실행하세요.", "확인");
                return;
            }

            if (!Directory.Exists(PrefabFolder))
            {
                Directory.CreateDirectory(PrefabFolder);
                AssetDatabase.Refresh();
            }

            GameObject root = pause.gameObject;

            // 이미 프리팹 인스턴스면 그 원본에 저장한다 — 인스턴스를 또 프리팹으로 만들면 원본이 둘이 된다.
            if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
                Debug.Log($"[SystemMenu] 이미 프리팹 인스턴스라 원본에 반영했습니다: " +
                          $"{AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(root))}", root);
                return;
            }

            // 손으로 꽂은 아이콘 스프라이트가 그대로 프리팹 값이 되고, 씬의 것은 그 인스턴스가 된다.
            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
            EditorSceneManager.MarkSceneDirty(root.scene);

            Debug.Log($"[SystemMenu] {PrefabPath} 로 뽑았고 이 씬의 것은 인스턴스가 됐습니다. " +
                      "앞으로 아이콘·색·배치는 프리팹에서 고치세요.", prefab);
        }

        [MenuItem("Tools/ChainRiposte/System Menu/Add To StageSelect (나가기 = 타이틀)")]
        private static void AddToMap()
        {
            var map = Object.FindFirstObjectByType<StageSelectController>(FindObjectsInactive.Include);
            if (map == null)
            {
                EditorUtility.DisplayDialog("시스템 메뉴 추가",
                    "이 씬에서 StageSelectController 를 찾지 못했습니다. StageSelect 씬을 열고 다시 실행하세요.", "확인");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("시스템 메뉴 추가",
                    $"{PrefabPath} 가 없습니다.\n먼저 Main 씬에서 " +
                    "System Menu ▸ Extract Prefab From Open Scene 을 실행하세요.", "확인");
                return;
            }

            // 같은 이름의 옛것만 갈아 끼운다 — 지도의 다른 UI(정보 패널·START·상태 창)는 건드리지 않는다.
            var existing = GameObject.Find(CanvasName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Add System Menu");
            instance.name = CanvasName;

            // 지도에서 「나가기」는 타이틀로 간다 — 지도에서 지도로 나가는 것은 아무 일도 아니다.
            var pause = instance.GetComponent<PauseMenu>();
            if (pause != null)
            {
                var so = new SerializedObject(pause);
                so.FindProperty("quitSceneName").stringValue = "Title";
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(pause);
            }

            RetargetQuitLabel(instance);

            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            Debug.Log("[SystemMenu] 지도에 시스템 메뉴를 얹었습니다(나가기 = 타이틀). " +
                      "버튼 배치를 지도에 맞게 옮겼으면 그 인스턴스에만 남고 프리팹은 안 바뀝니다.", instance);
        }

        // ── 설정 패널 (타이틀·전투 씬 공용) ──

        /// <summary>
        /// 시스템 메뉴 프리팹 안의 <c>OptionsPanel</c>을 <b>별도 프리팹으로</b> 뽑는다(중첩 프리팹).
        ///
        /// <para>왜: 설정 패널은 <b>타이틀에도</b> 있는데 예전에는 빌더가 씬마다 코드로 새로 짜서
        /// <b>사본이 둘</b>이었다. 그래서 시스템 메뉴 쪽 패널을 고쳐도 타이틀은 안 바뀌었다.
        /// 이제 원본은 하나이고 두 씬이 그 인스턴스를 쓴다.</para>
        /// </summary>
        [MenuItem("Tools/ChainRiposte/System Menu/Extract Options Panel Prefab")]
        private static void ExtractOptionsPanel()
        {
            var menu = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (menu == null)
            {
                EditorUtility.DisplayDialog("설정 패널 프리팹",
                    $"{PrefabPath} 가 없습니다. 먼저 Main 씬에서 Extract Prefab From Open Scene 을 실행하세요.", "확인");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(OptionsPanelPrefabPath) != null)
            {
                Debug.Log($"[SystemMenu] {OptionsPanelPrefabPath} 가 이미 있습니다 — 아무것도 안 했습니다.");
                return;
            }

            // 프리팹 내용물을 열어 그 안의 패널을 프리팹으로 저장하면 <b>중첩 프리팹</b>이 된다.
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            Transform panel = FindChild(root.transform, OptionsPanelName);
            if (panel == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                EditorUtility.DisplayDialog("설정 패널 프리팹", $"{PrefabPath} 안에 {OptionsPanelName} 이 없습니다.", "확인");
                return;
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(
                panel.gameObject, OptionsPanelPrefabPath, InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            Debug.Log($"[SystemMenu] {OptionsPanelPrefabPath} 로 뽑았습니다(시스템 메뉴 안에서는 중첩 인스턴스). " +
                      "이제 설정 항목은 이 프리팹에서만 고치면 타이틀·전투 씬에 같이 반영됩니다.", saved);
        }

        /// <summary>
        /// 열려 있는 씬의 <c>OptionsPanel</c>을 <b>프리팹 인스턴스로 교체</b>하고, 그것을 가리키던
        /// 컴포넌트 참조(<c>TitleController.optionsPanel</c> 등)를 새 것으로 옮긴다.
        /// </summary>
        [MenuItem("Tools/ChainRiposte/System Menu/Replace Options Panel In Open Scene")]
        private static void ReplaceOptionsPanel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OptionsPanelPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("설정 패널 교체",
                    $"{OptionsPanelPrefabPath} 가 없습니다. 먼저 Extract Options Panel Prefab 을 실행하세요.", "확인");
                return;
            }

            OptionsPanel[] existing = Object.FindObjectsByType<OptionsPanel>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject old = null;
            foreach (OptionsPanel candidate in existing)
            {
                // 이미 프리팹 인스턴스면 건드릴 이유가 없다(그쪽은 원본을 따라간다).
                if (PrefabUtility.IsPartOfPrefabInstance(candidate))
                    continue;

                old = candidate.gameObject;
                break;
            }

            if (old == null)
            {
                Debug.Log("[SystemMenu] 이 씬에는 코드로 만들어진 설정 패널이 없습니다 — 아무것도 안 했습니다.");
                return;
            }

            Transform parent = old.transform.parent;
            int siblingIndex = old.transform.GetSiblingIndex();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Replace Options Panel");
            instance.name = OptionsPanelName;
            instance.transform.SetSiblingIndex(siblingIndex);
            instance.SetActive(false); // 평소엔 닫혀 있다 — 열고 닫는 것은 각 화면의 컨트롤러가 한다

            int rewired = Rewire(old, instance);
            Undo.DestroyObjectImmediate(old);

            EditorSceneManager.MarkSceneDirty(instance.scene);
            Debug.Log($"[SystemMenu] 설정 패널을 프리팹 인스턴스로 교체했습니다 (참조 {rewired}곳 갱신). " +
                      "이제 이 씬도 프리팹을 고치면 같이 바뀝니다.", instance);
        }

        /// <summary>
        /// 씬 안에서 옛 패널을 가리키던 참조를 새 인스턴스로 옮긴다. 필드 이름을 코드에 적지 않고
        /// <b>가리키는 대상으로</b> 찾으므로, 앞으로 어느 컴포넌트가 설정 패널을 들고 있어도 따라온다.
        /// </summary>
        private static int Rewire(GameObject old, GameObject replacement)
        {
            int count = 0;
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour.gameObject == old)
                    continue;

                var so = new SerializedObject(behaviour);
                SerializedProperty property = so.GetIterator();
                bool changed = false;
                while (property.NextVisible(enterChildren: true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference
                        || property.objectReferenceValue == null)
                        continue;

                    if (property.objectReferenceValue == old)
                    {
                        property.objectReferenceValue = replacement;
                        changed = true;
                    }
                    else if (property.objectReferenceValue is Component component && component.gameObject == old)
                    {
                        // Button 같은 컴포넌트 참조도 같은 이름의 것으로 옮겨 준다
                        Component moved = replacement.GetComponent(component.GetType());
                        if (moved != null)
                        {
                            property.objectReferenceValue = moved;
                            changed = true;
                        }
                    }
                }

                if (!changed)
                    continue;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(behaviour);
                count++;
            }

            return count;
        }

        /// <summary>
        /// 빌더가 부른다 — 프리팹이 있으면 그 인스턴스를 만들어 주고, 없으면 null(그때는 빌더가 코드로 짠다).
        /// </summary>
        internal static GameObject InstantiateOptionsPanel(Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OptionsPanelPrefabPath);
            if (prefab == null)
                return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = OptionsPanelName;
            instance.SetActive(false);
            return instance;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }

            return null;
        }

        /// <summary>
        /// 「지도로 나가기」 문구를 「타이틀로 나가기」로 바꾼다 — 목적지가 달라졌으니 글씨도 달라야 한다.
        /// 키를 갈아 끼우는 것이므로 번역은 CSV가 계속 맡는다.
        /// </summary>
        private static void RetargetQuitLabel(GameObject instance)
        {
            foreach (LocalizedText label in instance.GetComponentsInChildren<LocalizedText>(true))
            {
                // 프로퍼티(Key) 대신 직렬화 필드를 고친다 — 프로퍼티는 즉시 다시 그리려 하고,
                // 에디터에서는 Awake가 안 돌아 TMP 참조가 아직 없다.
                var so = new SerializedObject(label);
                SerializedProperty key = so.FindProperty("key");
                if (key.stringValue != "pause.quit" && key.stringValue != "pause.quit.confirm")
                    continue;

                key.stringValue = key.stringValue == "pause.quit" ? "pause.quit.title" : "pause.quit.title.confirm";
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }
        }
    }
}
