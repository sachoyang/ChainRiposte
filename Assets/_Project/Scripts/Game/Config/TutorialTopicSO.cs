using UnityEngine;
using UnityEngine.Video;

namespace ChainRiposte.Game.Config
{
    /// <summary>
    /// <b>소개 항목 하나</b> = 카드 한 장 (<c>Docs/TUTORIAL.md</c> §3.2).
    /// 어느 판에서 뜨는지는 여기 없다 — <see cref="StageDataSO.Introduces"/>가 들고 있다.
    ///
    /// <para>그래야 스테이지를 재배치하거나 월드3을 넣어도 코드도 이 에셋도 안 고친다.
    /// 최종 고리·고리 깊이·기억 판정이 전부 "데이터가 들고 있다"로 간 것과 같은 판단이다.</para>
    ///
    /// <para><b>보여 줄 것의 우선순위: 영상 → 그림 → 글씨만.</b> 셋 다 비어도 카드는 뜬다 —
    /// 아트가 없다고 안내가 멈추면 안 된다(이 프로젝트의 일관된 규칙).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ChainRiposte/Tutorial Topic", fileName = "Tutorial_")]
    public sealed class TutorialTopicSO : ScriptableObject
    {
        [Header("식별자 — 「봤다」 세이브 키 (비우면 에셋 이름 사용)")]
        [Tooltip("한 번 정하면 바꾸지 말 것. 바꾸면 이미 본 사람에게 카드가 다시 뜬다.")]
        [SerializeField] private string topicId = "";

        [Header("문구 — 현지화 키 (구글 시트에 넣을 것)")]
        [SerializeField] private string titleKey = "";
        [Tooltip("본문. 줄바꿈은 CSV 쪽에 \\n 으로 적는다.")]
        [SerializeField] private string bodyKey = "";

        [Header("보여 줄 것 (영상 → 그림 → 글씨만 순으로 떨어진다)")]
        [Tooltip("게임 중 녹화본 3~4초. 루프로 돈다. " +
                 "안드로이드는 H.264 mp4 만 확실히 재생되고, 카드가 여러 장이므로 한 장당 1MB 안쪽으로.")]
        [SerializeField] private VideoClip clip;
        [Tooltip("영상이 없을 때 쓰는 정지 그림.")]
        [SerializeField] private Sprite image;

        /// <summary>「봤다」 세이브가 이 항목을 가리키는 이름.</summary>
        public string TopicId => string.IsNullOrWhiteSpace(topicId) ? name : topicId;

        public string TitleKey => titleKey;
        public string BodyKey => bodyKey;
        public VideoClip Clip => clip;
        public Sprite Image => image;
    }
}
