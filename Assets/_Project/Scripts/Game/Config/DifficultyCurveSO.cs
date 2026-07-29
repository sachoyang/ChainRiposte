using ChainRiposte.Core.Combat;
using UnityEngine;

namespace ChainRiposte.Game.Config
{
    /// <summary>
    /// 기획자가 인스펙터에서 조절하는 <b>난이도 곡선</b> (<c>Docs/PROGRESSION.md</c> §2.5 · §3 · §6).
    /// 런타임에는 순수 C# <see cref="DifficultyCurve"/>로 변환된다.
    ///
    /// <para><b>여기와 보스 에셋의 역할</b>: 보스 에셋(<see cref="BossDataSO"/>)의 숫자는
    /// "이 보스가 어떤 놈인가"이고, 여기 값은 "몇 번째 고리라서 얼마나 부푸나"다.
    /// 특정 보스만 험하게 하려면 <b>그 보스 에셋</b>을, 후반이 통째로 물렁하면 <b>여기</b>를 만진다.
    /// 이 구분이 흐려지면 물렁할 때 어디를 봐야 할지 알 수 없게 된다.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ChainRiposte/Difficulty Curve", fileName = "DifficultyCurve")]
    public sealed class DifficultyCurveSO : ScriptableObject
    {
        [Header("고리 깊이별 증가율 (1-1 = 깊이 0)")]
        [Tooltip("고리 하나당 보스 HP 증가율. 0.04 = 고리당 +4%. " +
            "HP는 전투 '길이'일 뿐이라 크게 올리면 지루해진다 — 어려움은 아래 셋에서 온다.")]
        [SerializeField] private float hpPerLink = 0.04f;

        [Tooltip("고리 하나당 체간 한계치 증가율. 인살까지 필요한 패링 횟수가 늘어난다. " +
            "'보스가 물렁하다'고 느끼면 여기부터 올린다.")]
        [SerializeField] private float posturePerLink = 0.08f;

        [Tooltip("고리 하나당 노트 피해 증가율. 실수 한 번이 아파진다.")]
        [SerializeField] private float damagePerLink = 0.06f;

        [Tooltip("고리 하나당 템포 증가율. 노트가 빨리 오고 예비동작이 짧아진다 — " +
            "채보를 안 건드리고 조일 수 있는 유일한 레버라 강력하지만, 조금씩 올릴 것.")]
        [SerializeField] private float tempoPerLink = 0.03f;

        [Header("NG+ 회차별 증가율 (고리 배수 위에 곱해진다)")]
        [SerializeField] private float hpPerNewGamePlus = 0.25f;
        [SerializeField] private float posturePerNewGamePlus = 0.25f;
        [SerializeField] private float damagePerNewGamePlus = 0.3f;
        [SerializeField] private float tempoPerNewGamePlus = 0.08f;

        [Header("천장")]
        [Tooltip("템포 배수의 상한. 속도만은 어느 지점을 넘으면 실력이 아니라 운이 된다 — " +
            "예비동작이 사람의 반응 시간보다 짧아진다. NG+를 여러 회차 돌아도 여기서 멈춘다.")]
        [SerializeField, Min(1f)] private float maxTempoMultiplier = 1.5f;

        public DifficultyCurve ToConfig() => new()
        {
            HpPerLink = hpPerLink,
            PosturePerLink = posturePerLink,
            DamagePerLink = damagePerLink,
            TempoPerLink = tempoPerLink,
            HpPerNewGamePlus = hpPerNewGamePlus,
            PosturePerNewGamePlus = posturePerNewGamePlus,
            DamagePerNewGamePlus = damagePerNewGamePlus,
            TempoPerNewGamePlus = tempoPerNewGamePlus,
            MaxTempoMultiplier = maxTempoMultiplier,
        };
    }
}
