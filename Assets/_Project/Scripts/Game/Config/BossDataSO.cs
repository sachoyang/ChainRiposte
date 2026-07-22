using System;
using System.Collections.Generic;
using ChainRiposte.Core.Combat;
using UnityEngine;

namespace ChainRiposte.Game.Config
{
    /// <summary>
    /// 보스 전투 밸런스 데이터. 공격 시퀀스의 텔레그래프 길이를 섞어
    /// 정박/엇박 리듬을 기획자가 인스펙터에서 설계한다 (GDD §5.2).
    /// </summary>
    [CreateAssetMenu(menuName = "ChainRiposte/Boss Data", fileName = "Boss_")]
    public sealed class BossDataSO : ScriptableObject
    {
        [Header("표시")]
        [SerializeField] private string displayName = "Boss";
        [Tooltip("월드맵 정보 패널에 띄울 보스 이미지. 아직 안 가본 스테이지에서는 검은 실루엣으로 나온다.")]
        [SerializeField] private Sprite portrait;
        [Tooltip("전투 화면에 서는 보스 이미지. 비우면 위 초상을 그대로 쓴다.")]
        [SerializeField] private Sprite battleSprite;

        [Header("생존/체간")]
        [SerializeField, Min(1f)] private float maxHp = 120f;
        [Tooltip("체간 한계치 — 도달 시 인살 가능")]
        [SerializeField, Min(1f)] private float maxPosture = 100f;

        [Header("체간 상승/회복")]
        [Tooltip("패링 성공 1회당 체간 상승량 (대폭)")]
        [SerializeField, Min(0f)] private float parryPostureGain = 25f;
        [Tooltip("공격 적중 시 체간 상승량 = ATK × 이 배율 (소폭)")]
        [SerializeField, Min(0f)] private float attackPostureFactor = 0.5f;
        [Tooltip("체간 자연 회복 속도 (초당). 0이면 회복 없음")]
        [SerializeField, Min(0f)] private float postureDecayPerSecond = 6f;
        [Tooltip("체크 시 보스 HP 비율에 비례해 체간 회복이 느려진다 (HP가 낮을수록 무너지기 쉬움)")]
        [SerializeField] private bool scaleDecayWithHp = true;

        [Header("공격 패턴 (위에서부터 순서대로, 끝나면 반복)")]
        [Tooltip("전투 시작 후 첫 텔레그래프까지의 대기")]
        [SerializeField, Min(0f)] private float firstAttackDelaySeconds = 1.5f;
        [SerializeField] private AttackEntry[] pattern = Array.Empty<AttackEntry>();

        [Serializable]
        private struct AttackEntry
        {
            [Tooltip("텔레그래프(예비 동작) 길이 — 끝나는 순간 타격")]
            [Min(0.05f)] public float telegraphSeconds;
            [Tooltip("패링 실패 시 피해 (DEF 적용 전)")]
            [Min(0f)] public float damage;
            [Tooltip("해제 시 패링 불가 공격 — 반드시 맞는 압박 수")]
            public bool parryable;
            [Tooltip("타격 후 다음 공격까지의 후딜레이")]
            [Min(0f)] public float recoverySeconds;
        }

        public string DisplayName => displayName;

        /// <summary>월드맵 표시용 초상. 없으면 컨트롤러가 이미지를 숨긴다.</summary>
        public Sprite Portrait => portrait;

        /// <summary>전투 화면의 보스 본체. 전용 이미지가 없으면 초상으로 대체한다.</summary>
        public Sprite BattleSprite => battleSprite != null ? battleSprite : portrait;

        public BossConfig ToConfig()
        {
            if (pattern.Length == 0)
                throw new InvalidOperationException($"{name}: 공격 패턴이 비어 있습니다.");

            var attacks = new List<BossAttackConfig>(pattern.Length);
            foreach (AttackEntry entry in pattern)
                attacks.Add(new BossAttackConfig(
                    entry.telegraphSeconds, entry.damage, entry.parryable, entry.recoverySeconds));

            return new BossConfig
            {
                Name = displayName,
                MaxHp = maxHp,
                MaxPosture = maxPosture,
                ParryPostureGain = parryPostureGain,
                AttackPostureFactor = attackPostureFactor,
                PostureDecayPerSecond = postureDecayPerSecond,
                ScaleDecayWithHp = scaleDecayWithHp,
                FirstAttackDelaySeconds = firstAttackDelaySeconds,
                Pattern = attacks,
            };
        }
    }
}
