using System.Collections.Generic;
using ChainRiposte.Game.Map;
using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 월드맵 노드 그림을 <b>여섯 개 한 번에</b> 갈아 끼운다.
    ///
    /// <para><b>왜 도구인가</b>: 노드는 씬에 실물로 놓인 오브젝트라 그림 교체는 원래 드래그 몇 번이면 된다.
    /// 그런데 노드가 여섯 개이고, 그림마다 픽셀 크기·PPU가 달라서 <b>하나씩 꽂으면 크기가 제각각</b>이 된다.
    /// 여기서는 크기를 그림에서 역산해 전부 같은 월드 크기로 맞춘다 — 세션 11의 타일 크기,
    /// 세션 14의 잠금 배지와 같은 규칙이다.</para>
    ///
    /// <para><b>크기의 기준은 길 두께다.</b> 노드가 길보다 조금 크면 길이 노드 양옆으로 삐져나와
    /// 노드와 길이 겹쳐 보인다("색깔이 겹쳐 보인다"). 노드는 길을 <b>덮어야</b> 그 위에 놓인 것으로 읽힌다.
    /// 그래서 절대 크기를 적는 대신 <b>길 두께의 배수</b>로 정한다 — 길을 굵게 하면 노드도 따라 커진다.</para>
    ///
    /// <para><b>트랜스폼은 노드가 아니라 <c>Art</c> 자식만 건드린다.</b> 노드 자신을 키우면 라벨·배지가
    /// 같이 끌려간다(세션 14에 노드와 글자를 23배로 만든 사고가 바로 이것이었다).</para>
    /// </summary>
    public sealed class MapNodeArtWindow : EditorWindow
    {
        private const string ArtChildName = "Art";
        private const string LockBadgeName = "LockedBadge";

        /// <summary>잠금 배지는 노드 그림의 몇 %인가. 덮되 테두리는 보이게 — 빌더와 같은 값.</summary>
        private const float LockBadgeRatio = 0.8f;

        private Sprite _sprite;
        private float _pathMultiple = 3f;
        private float _pathWidth = -1f;
        private bool _applyPathWidth;
        private bool _resetTint = true;

        [MenuItem("Tools/ChainRiposte/Map/Node Art… (노드 그림 한 번에 교체)")]
        private static void Open()
        {
            GetWindow<MapNodeArtWindow>(true, "지도 노드 그림", true).minSize = new Vector2(420f, 380f);
        }

        private void OnEnable() => ReadCurrentPathWidth();

        private void OnGUI()
        {
            List<MapNode> nodes = FindNodes();
            LineRenderer path = FindPath();

            EditorGUILayout.HelpBox(
                "열려 있는 씬의 노드 그림을 전부 같은 그림·같은 크기로 맞춥니다.\n" +
                "노드 자신의 트랜스폼은 건드리지 않습니다 — Art 자식만 조절하므로 라벨·배지 위치는 그대로입니다.",
                MessageType.None);

            EditorGUILayout.Space();
            DrawStatus(nodes, path);

            EditorGUILayout.Space();
            _sprite = (Sprite)EditorGUILayout.ObjectField("노드 그림", _sprite, typeof(Sprite), false);
            _resetTint = EditorGUILayout.Toggle(
                new GUIContent("색을 흰색으로", "지금 노드는 단색 사각형을 보라·초록으로 틴트한 것이라 그림을 꽂아도 그 색이 남는다. " +
                                          "그림 본래 색으로 보려면 켜 둘 것."),
                _resetTint);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("크기 — 길 두께 기준", EditorStyles.boldLabel);
            _pathMultiple = EditorGUILayout.Slider(
                new GUIContent("길 두께의 배수", "노드 가로 크기 = 길 두께 × 이 값. 3보다 작으면 길이 노드 옆으로 삐져나온다."),
                _pathMultiple, 1.5f, 8f);

            _applyPathWidth = EditorGUILayout.Toggle(
                new GUIContent("길 두께도 바꾸기", "길 자체가 굵어서 노드를 키워도 안 덮이는 경우에 쓴다."),
                _applyPathWidth);
            using (new EditorGUI.DisabledScope(!_applyPathWidth || path == null))
                _pathWidth = EditorGUILayout.Slider("길 두께", _pathWidth, 0.02f, 0.4f);

            float target = TargetWorldWidth(path);
            EditorGUILayout.LabelField("→ 노드 가로 크기", target > 0f ? $"{target:0.###} (월드)" : "길을 찾지 못했습니다");

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(nodes.Count == 0 || _sprite == null || target <= 0f))
            {
                if (GUILayout.Button($"노드 {nodes.Count}개에 적용", GUILayout.Height(30f)))
                    Apply(nodes, path, target);
            }

            using (new EditorGUI.DisabledScope(nodes.Count == 0 || target <= 0f))
            {
                if (GUILayout.Button("그림은 그대로 두고 크기만 맞추기"))
                    Apply(nodes, path, target, sizeOnly: true);
            }
        }

        private void DrawStatus(List<MapNode> nodes, LineRenderer path)
        {
            if (nodes.Count == 0)
            {
                EditorGUILayout.HelpBox("열려 있는 씬에 MapNode가 없습니다. StageSelect 씬을 여세요.", MessageType.Warning);
                return;
            }

            SpriteRenderer sample = ResolveArt(nodes[0], create: false);
            string size = sample != null ? $"{sample.bounds.size.x:0.###}" : "?";
            string sprite = sample != null && sample.sprite != null ? sample.sprite.name : "없음";
            string width = path != null ? $"{path.widthMultiplier:0.###}" : "?";

            EditorGUILayout.LabelField("지금", $"노드 {nodes.Count}개 · 그림 {sprite} · 가로 {size} · 길 두께 {width}");
        }

        /// <summary>노드 가로 크기 = 길 두께 × 배수. 길이 없으면 크기를 정할 근거가 없으므로 0.</summary>
        private float TargetWorldWidth(LineRenderer path)
        {
            if (path == null)
                return 0f;

            float width = _applyPathWidth ? _pathWidth : path.widthMultiplier;
            return width * _pathMultiple;
        }

        private void Apply(List<MapNode> nodes, LineRenderer path, float targetWorldWidth, bool sizeOnly = false)
        {
            if (_applyPathWidth && path != null)
            {
                Undo.RecordObject(path, "노드 그림 적용");
                path.widthMultiplier = _pathWidth;
                EditorUtility.SetDirty(path);
            }

            int pathOrder = path != null ? path.sortingOrder : 0;
            int applied = 0;

            foreach (MapNode node in nodes)
            {
                SpriteRenderer art = ResolveArt(node, create: true);
                if (art == null)
                    continue;

                Undo.RecordObject(art, "노드 그림 적용");
                Undo.RecordObject(art.transform, "노드 그림 적용");

                if (!sizeOnly)
                {
                    art.sprite = _sprite;
                    if (_resetTint)
                        art.color = Color.white;
                }

                // 노드는 길보다 반드시 위에 그려져야 한다 — 순서가 같으면 어느 쪽이 위인지가 상황마다 달라진다
                if (art.sortingOrder <= pathOrder)
                    art.sortingOrder = pathOrder + 1;

                FitWidth(art.transform, art.sprite, targetWorldWidth);
                EditorUtility.SetDirty(art);

                RefitLockBadge(node, art, pathOrder);
                LinkIconRenderer(node, art);
                applied++;
            }

            if (applied > 0)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(nodes[0].gameObject.scene);

            ReadCurrentPathWidth();
            Debug.Log($"[MapNodeArt] 노드 {applied}개 적용 — 가로 {targetWorldWidth:0.###} (길 두께의 {_pathMultiple:0.##}배).");
        }

        /// <summary>
        /// 그림을 원하는 <b>월드 가로 크기</b>로 맞춘다. 픽셀 크기·PPU가 달라도 같은 크기가 되도록
        /// 스케일을 그림에서 역산한다 — 그래야 아트를 바꿔도 노드 크기가 안 흔들린다.
        /// <b>비율은 유지</b>한다(정사각형으로 늘리면 도트가 뭉개진다).
        /// </summary>
        private static void FitWidth(Transform art, Sprite sprite, float targetWorldWidth)
        {
            if (sprite == null || sprite.bounds.size.x <= Mathf.Epsilon)
                return;

            float k = targetWorldWidth / sprite.bounds.size.x;
            Vector3 parent = art.parent != null ? art.parent.lossyScale : Vector3.one;
            art.localScale = new Vector3(
                k / Mathf.Max(Mathf.Epsilon, parent.x),
                k / Mathf.Max(Mathf.Epsilon, parent.y),
                1f);
        }

        /// <summary>
        /// 잠금 배지(사슬)도 새 노드 크기에 다시 맞춘다. 안 맞추면 노드만 커지고 사슬은 옛 크기로 남아
        /// <b>노드 한가운데 작은 사슬</b>이 뜬다. 크기 규칙은 빌더와 같은 80%.
        /// </summary>
        private static void RefitLockBadge(MapNode node, SpriteRenderer art, int pathOrder)
        {
            Transform badge = node.transform.Find(LockBadgeName);
            var renderer = badge != null ? badge.GetComponent<SpriteRenderer>() : null;
            if (renderer == null)
                return;

            Undo.RecordObject(renderer, "노드 그림 적용");
            Undo.RecordObject(badge, "노드 그림 적용");

            if (renderer.sortingOrder <= art.sortingOrder)
                renderer.sortingOrder = art.sortingOrder + 1;

            FitWidth(badge, renderer.sprite, art.bounds.size.x * LockBadgeRatio);
            EditorUtility.SetDirty(renderer);
        }

        /// <summary>
        /// <c>MapNode</c>가 틴트를 입힐 렌더러로 이 <c>Art</c>를 보게 한다. 비워 두면 노드 자신에서
        /// <c>SpriteRenderer</c>를 찾는데, 지금 구조에서는 그게 없어서 <b>잠금 틴트가 조용히 사라진다.</b>
        /// </summary>
        private static void LinkIconRenderer(MapNode node, SpriteRenderer art)
        {
            var so = new SerializedObject(node);
            SerializedProperty prop = so.FindProperty("iconRenderer");
            if (prop == null || prop.objectReferenceValue == art)
                return;

            prop.objectReferenceValue = art;
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// 그림을 들고 있는 <c>Art</c> 자식. 없으면 만든다 — 그림을 노드 자신에 두면 크기를 맞출 때
        /// 라벨·배지가 같이 끌려간다(세션 14의 23배 사고).
        /// </summary>
        private static SpriteRenderer ResolveArt(MapNode node, bool create)
        {
            Transform art = node.transform.Find(ArtChildName);
            if (art != null)
            {
                var existing = art.GetComponent<SpriteRenderer>();
                if (existing != null || !create)
                    return existing;
                return Undo.AddComponent<SpriteRenderer>(art.gameObject);
            }

            // 옛 구조 — 그림이 노드 자신에 붙어 있다. 자식으로 옮겨 두면 앞으로 크기를 안전하게 만질 수 있다.
            var onSelf = node.GetComponent<SpriteRenderer>();
            if (!create)
                return onSelf;

            var go = new GameObject(ArtChildName);
            Undo.RegisterCreatedObjectUndo(go, "노드 그림 적용");
            go.transform.SetParent(node.transform, false);
            go.transform.SetAsFirstSibling();

            var created = go.AddComponent<SpriteRenderer>();
            if (onSelf != null)
            {
                created.sprite = onSelf.sprite;
                created.color = onSelf.color;
                created.sortingOrder = onSelf.sortingOrder;
                Undo.DestroyObjectImmediate(onSelf);
            }

            return created;
        }

        private static List<MapNode> FindNodes()
        {
            var nodes = new List<MapNode>(
                Object.FindObjectsByType<MapNode>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            nodes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return nodes;
        }

        private static LineRenderer FindPath()
        {
            var controller = Object.FindFirstObjectByType<StageSelectController>();
            if (controller != null)
            {
                var so = new SerializedObject(controller);
                SerializedProperty prop = so.FindProperty("pathLine");
                if (prop != null && prop.objectReferenceValue is LineRenderer wired)
                    return wired;
            }

            return Object.FindFirstObjectByType<LineRenderer>();
        }

        private void ReadCurrentPathWidth()
        {
            LineRenderer path = FindPath();
            if (path != null && _pathWidth <= 0f)
                _pathWidth = path.widthMultiplier;
        }
    }
}
