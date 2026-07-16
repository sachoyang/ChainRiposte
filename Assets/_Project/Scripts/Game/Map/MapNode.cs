using ChainRiposte.Game.Config;
using UnityEngine;

namespace ChainRiposte.Game.Map
{
    /// <summary>
    /// 월드맵의 스테이지 노드. 씬에 실물로 배치하고 인스펙터에서 스테이지를 지정한다.
    /// StageSelectController는 이 컴포넌트의 Transform 위치를 읽어 경로를 만든다 →
    /// 씬에서 노드를 드래그하면 캐릭터 이동 경로가 그대로 따라온다.
    /// </summary>
    public sealed class MapNode : MonoBehaviour
    {
        [Tooltip("이 노드가 나타내는 스테이지 데이터")]
        [SerializeField] private StageDataSO stage;

        public StageDataSO Stage => stage;
        public Vector3 Position => transform.position;

#if UNITY_EDITOR
        /// <summary>에디터 빌더 전용 — 노드 생성 시 스테이지를 주입한다.</summary>
        public void SetStageEditorOnly(StageDataSO value) => stage = value;
#endif
    }
}
