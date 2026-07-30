using System.Collections.Generic;
using System.IO;
using ChainRiposte.Game.Localization;
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

        /// <summary>예/아니오 확인 대화창 원본. 새 게임·지도로 나가기·진행도 초기화가 <b>같은 이것</b>을 쓴다.</summary>
        internal const string ConfirmPanelPrefabPath = PrefabFolder + "/ConfirmPanel.prefab";

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

        // ── 예/아니오 확인 대화창 (새 게임 · 나가기 · 진행도 초기화 공용) ──

        /// <summary>확인 대화창의 네 부분. 호스트(컨트롤러)가 참조하는 것이 이 넷이다.</summary>
        private struct ConfirmRefs
        {
            public GameObject Panel;
            public TMP_Text Text;
            public Button Yes;
            public Button No;

            public bool IsComplete => Panel != null && Text != null && Yes != null && No != null;
        }

        /// <summary>
        /// 확인 대화창을 프리팹으로 뽑는다. 원본은 <b>시스템 메뉴의 나가기 확인</b>(테두리 박스가 있는 쪽)이다 —
        /// 코드로 만든 다른 두 개(타이틀의 새 게임 확인, 설정의 초기화 확인)는 테두리가 없는 납작한 모양이라
        /// 셋이 서로 다르게 보였다. <b>가장 잘 생긴 하나를 원본으로 삼는다.</b>
        /// </summary>
        [MenuItem("Tools/ChainRiposte/System Menu/Extract Confirm Panel Prefab")]
        private static void ExtractConfirmPanel()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmPanelPrefabPath) != null)
            {
                Debug.Log($"[SystemMenu] {ConfirmPanelPrefabPath} 가 이미 있습니다 — 아무것도 안 했습니다.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            Transform source = FindChild(root.transform, "QuitConfirmPanel");
            if (source == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                EditorUtility.DisplayDialog("확인 대화창 프리팹", $"{PrefabPath} 안에 QuitConfirmPanel 이 없습니다.", "확인");
                return;
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(
                source.gameObject, ConfirmPanelPrefabPath, InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            Debug.Log($"[SystemMenu] {ConfirmPanelPrefabPath} 로 뽑았습니다. " +
                      "이제 Replace Confirm Panels 로 나머지 사본을 이 프리팹의 인스턴스로 갈아 끼우세요.", saved);
        }

        /// <summary>
        /// 코드로 만들어진 확인 대화창 사본을 프리팹 인스턴스로 교체한다 —
        /// <b>설정 패널 프리팹 안</b>과 <b>열려 있는 씬</b> 양쪽.
        ///
        /// <para>대화창을 찾는 기준은 이름이 아니라 <b>모양</b>이다(자식에 YesButton·NoButton이 있는 것).
        /// 이름은 자리마다 다르고(ConfirmPanel / OptionsConfirmPanel / QuitConfirmPanel) 앞으로도 늘어난다.</para>
        /// </summary>
        [MenuItem("Tools/ChainRiposte/System Menu/Replace Confirm Panels (Prefabs + Open Scene)")]
        private static void ReplaceConfirmPanels()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmPanelPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("확인 대화창 교체",
                    $"{ConfirmPanelPrefabPath} 가 없습니다. 먼저 Extract Confirm Panel Prefab 을 실행하세요.", "확인");
                return;
            }

            int replaced = ReplaceConfirmPanelsInPrefab(OptionsPanelPrefabPath, prefab);
            replaced += ReplaceConfirmPanelsInOpenScene(prefab);

            Debug.Log($"[SystemMenu] 확인 대화창 {replaced}개를 프리팹 인스턴스로 교체했습니다. " +
                      "이제 대화창 모양은 ConfirmPanel.prefab 한 곳에서만 고칩니다.");
        }

        private static int ReplaceConfirmPanelsInPrefab(string prefabPath, GameObject prefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            int replaced = 0;

            foreach (Transform candidate in FindConfirmPanels(root.transform))
            {
                ConfirmRefs old = Resolve(candidate.gameObject);
                if (!old.IsComplete)
                    continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, candidate.parent);
                instance.name = candidate.name;
                instance.transform.SetSiblingIndex(candidate.GetSiblingIndex());
                CopyRect(candidate as RectTransform, instance.transform as RectTransform);
                instance.SetActive(false);

                Rewire(root.GetComponentsInChildren<MonoBehaviour>(true), old, Resolve(instance));
                Object.DestroyImmediate(candidate.gameObject);
                replaced++;
            }

            if (replaced > 0)
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            PrefabUtility.UnloadPrefabContents(root);
            return replaced;
        }

        private static int ReplaceConfirmPanelsInOpenScene(GameObject prefab)
        {
            int replaced = 0;
            foreach (Transform candidate in FindConfirmPanels(null))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(candidate))
                    continue; // 이미 프리팹이 관리한다

                ConfirmRefs old = Resolve(candidate.gameObject);
                if (!old.IsComplete)
                    continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, candidate.parent);
                Undo.RegisterCreatedObjectUndo(instance, "Replace Confirm Panel");
                instance.name = candidate.name;
                instance.transform.SetSiblingIndex(candidate.GetSiblingIndex());
                CopyRect(candidate as RectTransform, instance.transform as RectTransform);
                instance.SetActive(false);

                Rewire(
                    Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                    old, Resolve(instance));

                Undo.DestroyObjectImmediate(candidate.gameObject);
                EditorSceneManager.MarkSceneDirty(instance.scene);
                replaced++;
            }

            return replaced;
        }

        /// <summary>
        /// 확인 대화창처럼 생긴 것들 — 자손에 <c>YesButton</c>과 <c>NoButton</c>이 있는 오브젝트.
        /// <paramref name="root"/>가 null이면 열려 있는 씬 전체에서 찾는다.
        ///
        /// <para><b>조상은 버린다.</b> 조건만 보면 대화창을 품은 패널·캔버스까지 다 걸리므로
        /// (설정 패널이 확인 대화창을 품고 있다) <b>다른 후보를 품지 않은 것</b>만 남긴다 —
        /// 이 걸러내기를 빼먹으면 설정 패널 전체가 대화창으로 교체되는 사고가 난다.</para>
        /// </summary>
        private static List<Transform> FindConfirmPanels(Transform root)
        {
            IEnumerable<Transform> all = root != null
                ? root.GetComponentsInChildren<Transform>(true)
                : Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var matches = new List<Transform>();
            foreach (Transform candidate in all)
            {
                if (candidate == root)
                    continue; // 프리팹 루트 자체는 절대 교체 대상이 아니다

                // 이미 공용 프리팹으로 만들어진 것은 건너뛴다 — 안 그러면 <b>두 번 돌릴 때마다</b>
                // 그 안의 Box를 또 대화창으로 갈아 끼워 껍데기가 한 겹씩 늘어난다(실제로 그렇게 됐다).
                if (IsSharedConfirmPanel(candidate))
                    continue;

                if (FindChild(candidate, "YesButton") != null && FindChild(candidate, "NoButton") != null)
                    matches.Add(candidate);
            }

            var innermost = new List<Transform>();
            foreach (Transform candidate in matches)
            {
                bool containsAnother = false;
                foreach (Transform other in matches)
                {
                    if (other != candidate && other.IsChildOf(candidate))
                    {
                        containsAnother = true;
                        break;
                    }
                }

                if (!containsAnother)
                    innermost.Add(candidate);
            }

            return innermost;
        }

        /// <summary>
        /// 이 오브젝트(또는 조상)가 이미 공용 확인 대화창 프리팹의 인스턴스인가 —
        /// <b>두 번 실행해도 아무 일도 안 일어나게</b> 하는 안전장치다.
        /// </summary>
        private static bool IsSharedConfirmPanel(Transform candidate)
        {
            for (Transform t = candidate; t != null; t = t.parent)
            {
                Object source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (source != null && AssetDatabase.GetAssetPath(source) == ConfirmPanelPrefabPath)
                    return true;
            }

            return false;
        }

        /// <summary>대화창의 네 부분을 <b>이름이 아니라 역할</b>로 찾는다 — 자리마다 이름이 다르다.</summary>
        private static ConfirmRefs Resolve(GameObject panel)
        {
            var refs = new ConfirmRefs { Panel = panel };
            Transform yes = FindChild(panel.transform, "YesButton");
            Transform no = FindChild(panel.transform, "NoButton");
            refs.Yes = yes != null ? yes.GetComponent<Button>() : null;
            refs.No = no != null ? no.GetComponent<Button>() : null;

            // 문구는 '버튼에 딸리지 않은' 글씨다 — 버튼 라벨과 구분하는 기준이 이름보다 튼튼하다.
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.GetComponentInParent<Button>() != null)
                    continue;

                refs.Text = text;
                break;
            }

            return refs;
        }

        /// <summary>옛 대화창을 가리키던 참조를 새 인스턴스의 같은 역할로 옮긴다.</summary>
        private static int Rewire(IEnumerable<MonoBehaviour> hosts, ConfirmRefs old, ConfirmRefs replacement)
        {
            if (!replacement.IsComplete)
                return 0;

            int count = 0;
            foreach (MonoBehaviour host in hosts)
            {
                if (host == null || host.transform.IsChildOf(old.Panel.transform))
                    continue;

                var so = new SerializedObject(host);
                SerializedProperty property = so.GetIterator();
                bool changed = false;
                while (property.NextVisible(enterChildren: true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    Object value = property.objectReferenceValue;
                    if (value == null)
                        continue;

                    if (value == old.Panel)
                        property.objectReferenceValue = replacement.Panel;
                    else if (value == (Object)old.Text)
                        property.objectReferenceValue = replacement.Text;
                    else if (value == (Object)old.Yes)
                        property.objectReferenceValue = replacement.Yes;
                    else if (value == (Object)old.No)
                        property.objectReferenceValue = replacement.No;
                    else
                        continue;

                    changed = true;
                }

                if (!changed)
                    continue;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(host);
                count++;
            }

            return count;
        }

        /// <summary>자리(앵커·크기)는 옛것을 그대로 물려받는다 — 대화창이 있던 위치가 화면마다 다르다.</summary>
        private static void CopyRect(RectTransform from, RectTransform to)
        {
            if (from == null || to == null)
                return;

            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.anchoredPosition = from.anchoredPosition;
            to.sizeDelta = from.sizeDelta;
            to.localScale = from.localScale;
        }

        /// <summary>빌더가 부른다 — 프리팹이 있으면 인스턴스를 만들어 네 부분을 돌려준다.</summary>
        internal static bool TryInstantiateConfirmPanel(
            Transform parent, string name, out GameObject panel, out TMP_Text text, out Button yes, out Button no)
        {
            panel = null;
            text = null;
            yes = null;
            no = null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmPanelPrefabPath);
            if (prefab == null)
                return false;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.SetActive(false);

            ConfirmRefs refs = Resolve(instance);
            if (!refs.IsComplete)
                return false;

            panel = refs.Panel;
            text = refs.Text;
            yes = refs.Yes;
            no = refs.No;
            return true;
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
