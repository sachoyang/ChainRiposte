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
        private const string ClearBadgeName = "ClearedBadge";

        /// <summary>배지는 노드 그림의 몇 %인가. 덮되 테두리는 보이게 — 빌더와 같은 값.</summary>
        private const float BadgeRatio = 0.8f;

        private Sprite _sprite;
        private Sprite _lockSprite;
        private Sprite _clearSprite;
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
            EditorGUILayout.LabelField("상태 표시 — 비우면 그대로 둔다", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "잠김·클리어 그림은 노드 그림 위에 얹히는 배지입니다(노드 크기의 80%).\n" +
                "클리어 배지를 꽂으면 깬 판이 글자 색뿐 아니라 노드에서도 읽힙니다 — " +
                "지금은 글자 색 하나뿐이라 지도를 훑을 때 놓치기 쉽습니다.",
                MessageType.None);
            _lockSprite = (Sprite)EditorGUILayout.ObjectField("잠김 그림", _lockSprite, typeof(Sprite), false);
            _clearSprite = (Sprite)EditorGUILayout.ObjectField("클리어 그림", _clearSprite, typeof(Sprite), false);

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
            bool nothingChosen = _sprite == null && _lockSprite == null && _clearSprite == null;
            using (new EditorGUI.DisabledScope(nodes.Count == 0 || nothingChosen || target <= 0f))
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

                // 그림을 안 고른 채로 눌렀다고 이미 꽂아 둔 노드 그림을 지우면 안 된다
                if (!sizeOnly && _sprite != null)
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

                ApplyBadge(node, art, LockBadgeName, "lockedBadge", sizeOnly ? null : _lockSprite);
                ApplyBadge(node, art, ClearBadgeName, "clearedBadge", sizeOnly ? null : _clearSprite);
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
        /// 상태 배지(잠김 사슬 · 클리어 표시)를 노드 그림 위에 얹고 새 노드 크기에 다시 맞춘다.
        ///
        /// <para>크기를 다시 안 맞추면 노드만 커지고 배지는 옛 크기로 남아 <b>노드 한가운데 작은 사슬</b>이 뜬다.
        /// 그림을 안 주면 <b>크기만</b> 맞춘다 — 이미 꽂아 둔 그림을 지우지 않기 위해서다.</para>
        ///
        /// <para>배지는 항상 <b>꺼진 채로</b> 둔다. 켜고 끄는 것은 <see cref="MapNode.ApplyState"/>의 일이고,
        /// 켜 둔 채 저장하면 잠기지도 깨지도 않은 노드에 배지가 붙어 있는 씬이 남는다.</para>
        /// </summary>
        private static void ApplyBadge(MapNode node, SpriteRenderer art, string childName, string field, Sprite sprite)
        {
            Transform badge = node.transform.Find(childName);
            if (badge == null)
            {
                if (sprite == null)
                    return; // 오브젝트도 그림도 없으면 만들 이유가 없다

                var go = new GameObject(childName);
                Undo.RegisterCreatedObjectUndo(go, "노드 그림 적용");
                go.transform.SetParent(node.transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                go.AddComponent<SpriteRenderer>();
                badge = go.transform;
            }

            var renderer = badge.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = Undo.AddComponent<SpriteRenderer>(badge.gameObject);

            Undo.RecordObject(renderer, "노드 그림 적용");
            Undo.RecordObject(badge, "노드 그림 적용");

            if (sprite != null)
                renderer.sprite = sprite;
            if (renderer.sprite == null)
                return; // 그림이 없는 배지는 켜 봐야 흰 사각형이다

            if (renderer.sortingOrder <= art.sortingOrder)
                renderer.sortingOrder = art.sortingOrder + 1;

            FitWidth(badge, renderer.sprite, art.bounds.size.x * BadgeRatio);
            badge.gameObject.SetActive(false);
            EditorUtility.SetDirty(renderer);

            LinkBadge(node, field, badge.gameObject);
        }

        /// <summary>
        /// <c>MapNode</c>가 이 배지를 켜고 끌 수 있게 연결한다. 안 연결하면 오브젝트만 씬에 남고
        /// <b>영영 안 켜진다</b> — 클리어 배지가 세션 14에 정확히 그 상태로 은퇴해 있었다.
        /// </summary>
        private static void LinkBadge(MapNode node, string field, GameObject badge)
        {
            var so = new SerializedObject(node);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null || prop.objectReferenceValue == badge)
                return;

            prop.objectReferenceValue = badge;
            so.ApplyModifiedProperties();
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
