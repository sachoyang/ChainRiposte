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
        [Tooltip("클리어했을 때만 켜지는 오브젝트 (깃발/체크 등)")]
        [SerializeField] private GameObject clearedBadge;
        [Tooltip("잠긴 노드에 곱해지는 색")]
        [SerializeField] private Color lockedTint = new(0.35f, 0.35f, 0.40f, 1f);

        private Color _unlockedColor;
        private bool _colorCaptured;

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
#endif
    }
}
