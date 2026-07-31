using ChainRiposte.Game.Config;
using UnityEngine;

namespace ChainRiposte.Game.Map
{
    /// <summary>
    /// 월드맵의 스테이지 노드. 씬에 실물로 배치하고 인스펙터에서 스테이지를 지정한다.
    /// StageSelectController는 이 컴포넌트의 Transform 위치를 읽어 경로를 만든다 →
    /// 씬에서 노드를 드래그하면 캐릭터 이동 경로가 그대로 따라온다.
    ///
    /// 잠금/클리어 표시도 <b>씬 오브젝트</b>(배지)로 두고 여기서는 켜고 끄기만 한다 —
    /// 자물쇠·깃발 아트로 교체하려면 배지 오브젝트만 바꾸면 된다.
    /// </summary>
    public sealed class MapNode : MonoBehaviour
    {
        [Tooltip("이 노드가 나타내는 스테이지 데이터")]
        [SerializeField] private StageDataSO stage;

        [Header("상태 표시 (선택 — 비워도 동작한다)")]
        [Tooltip("잠겼을 때 틴트를 입힐 렌더러. 비우면 이 오브젝트에서 찾는다.")]
        [SerializeField] private SpriteRenderer iconRenderer;
        [Tooltip("잠겼을 때만 켜지는 오브젝트 (자물쇠 아이콘 등)")]
        [SerializeField] private GameObject lockedBadge;
        [Tooltip("클리어했을 때만 켜지는 오브젝트 (깃발/체크 등). 비워도 된다 — 클리어 표시는 아래 라벨 색이 맡는다.")]
        [SerializeField] private GameObject clearedBadge;
        [Tooltip("잠긴 노드에 곱해지는 색")]
        [SerializeField] private Color lockedTint = new(0.35f, 0.35f, 0.40f, 1f);

        [Header("스테이지 글자 (1-1, 1-2 …)")]
        [Tooltip("클리어 여부를 <b>글자 색</b>으로 알린다. 작은 CLEAR 배지보다 멀리서도 읽힌다.")]
        [SerializeField] private TMPro.TMP_Text label;
        [Tooltip("깬 스테이지의 글자 색 (연초록)")]
        [SerializeField] private Color clearedLabelColor = new(0.55f, 0.92f, 0.60f);
        [Tooltip("잠긴 스테이지의 글자 색 — 갈 수 없는 곳이라는 게 글자에서도 읽혀야 한다.")]
        [SerializeField] private Color lockedLabelColor = new(0.45f, 0.45f, 0.50f);

        private Color _unlockedColor;
        private bool _colorCaptured;
        // 아직 안 깬 상태의 글자 색. 씬에서 칠한 색이 원본이므로 처음 한 번만 기억한다.
        private Color _labelColor;
        private bool _labelColorCaptured;

        public StageDataSO Stage => stage;
        public Vector3 Position => transform.position;

        /// <summary>이전 스테이지를 클리어하지 못해 잠긴 상태인가.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>컨트롤러가 진행도를 읽어 노드 상태를 갱신한다.</summary>
        public void ApplyState(bool unlocked, bool cleared)
        {
            IsLocked = !unlocked;

            SpriteRenderer renderer = ResolveRenderer();
            if (renderer != null)
                renderer.color = unlocked ? _unlockedColor : _unlockedColor * lockedTint;

            if (lockedBadge != null)
                lockedBadge.SetActive(!unlocked);
            if (clearedBadge != null)
                clearedBadge.SetActive(cleared);

            ApplyLabelColor(unlocked, cleared);
        }

        /// <summary>
        /// 스테이지 글자(1-1, 1-2 …)의 색으로 상태를 알린다.
        ///
        /// <para>작은 <c>CLEAR</c> 배지는 지도를 훑을 때 읽히지 않는다 — 이미 화면에 있는 글자의
        /// <b>색</b>을 바꾸는 편이 멀리서도 한눈에 들어온다. 잠김은 회색, 클리어는 연초록.</para>
        /// </summary>
        private void ApplyLabelColor(bool unlocked, bool cleared)
        {
            if (label == null)
                return;

            if (!_labelColorCaptured)
            {
                _labelColor = label.color;
                _labelColorCaptured = true;
            }

            label.color = !unlocked ? lockedLabelColor
                : cleared ? clearedLabelColor
                : _labelColor;
        }

        private SpriteRenderer ResolveRenderer()
        {
            if (iconRenderer == null)
                iconRenderer = GetComponent<SpriteRenderer>();
            if (iconRenderer != null && !_colorCaptured)
            {
                _unlockedColor = iconRenderer.color;
                _colorCaptured = true;
            }
            return iconRenderer;
        }

#if UNITY_EDITOR
        /// <summary>에디터 빌더 전용 — 노드 생성 시 스테이지를 주입한다.</summary>
        public void SetStageEditorOnly(StageDataSO value) => stage = value;

        /// <summary>에디터 빌더 전용 — 생성한 배지 오브젝트를 연결한다.</summary>
        public void SetBadgesEditorOnly(GameObject locked, GameObject cleared)
        {
            lockedBadge = locked;
            clearedBadge = cleared;
        }

        /// <summary>에디터 빌더 전용 — 스테이지 글자를 연결한다(클리어·잠김을 색으로 알린다).</summary>
        public void SetLabelEditorOnly(TMPro.TMP_Text value) => label = value;

        /// <summary>
        /// 에디터 빌더 전용 — 그림을 든 <c>Art</c> 자식을 연결한다. 비워 두면 노드 자신에서
        /// <c>SpriteRenderer</c>를 찾는데, 그림이 자식에 있는 지금 구조에서는 못 찾아
        /// <b>잠금 틴트가 조용히 사라진다.</b>
        /// </summary>
        public void SetIconRendererEditorOnly(SpriteRenderer value) => iconRenderer = value;
#endif
    }
}
