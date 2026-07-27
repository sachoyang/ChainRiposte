using UnityEngine;

namespace ChainRiposte.Game.Audio
{
    /// <summary>
    /// 씬 배경음. 씬에 GameObject 하나 놓고 클립만 꽂으면, 그 씬에 들어올 때 BGM 버스로 흐른다.
    /// JuiceDirector 가 없는 씬(인트로 / 타이틀 / 월드맵)용이다 — 전투 씬 음악은 JuiceDirector 가
    /// 퍼즐·전투 페이즈에 맞춰 직접 켠다(같은 BGM 소스라 서로 자연스럽게 교체된다).
    ///
    /// AudioService.PlayBgm 은 같은 클립이면 다시 시작하지 않으므로, 씬을 넘나들어도 음악이 끊기지 않는다.
    /// 씬 오서링 원칙 그대로 — 컨트롤러가 아니라 씬에 붙은 이 컴포넌트가 "무슨 곡"인지 들고 있는다.
    /// </summary>
    public sealed class SceneMusic : MonoBehaviour
    {
        [Tooltip("이 씬에서 재생할 배경음. 비워 두면 아무것도 하지 않는다(이전 곡을 그대로 둔다).")]
        [SerializeField] private AudioClip clip;

        [SerializeField] private bool loop = true;

        [Tooltip("켜질 때 자동 재생. 끄면 코드가 직접 AudioService.PlayBgm 을 호출한다.")]
        [SerializeField] private bool playOnEnable = true;

        private void OnEnable()
        {
            // 클립이 없으면 손대지 않는다 — 배선을 덜 했다고 앞 씬의 음악을 꺼 버리면 안 된다.
            if (playOnEnable && clip != null)
                AudioService.PlayBgm(clip, loop);
        }
    }
}
