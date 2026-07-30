using System.Collections.Generic;
using ChainRiposte.Core.Progress;
using ChainRiposte.Core.Stats;
using ChainRiposte.Game.Characters;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.Localization;
using ChainRiposte.Game.Memories;
using ChainRiposte.Game.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// <b>지금 내 상태</b>를 보는 창 — 월드맵에서 캐릭터 얼굴 버튼을 누르면 열린다.
    ///
    /// <para>퍼즐 화면에는 HUD가 늘 떠 있지만 지도에는 없어서, 다음 판에 들어가기 전에
    /// "내가 지금 얼마나 강한가 · 무슨 기억을 가졌나"를 확인할 방법이 없었다.</para>
    ///
    /// <para><b>읽기 전용</b>이다. 여기서 스탯을 분배하지 않는다 — 분배는 보스 돌입 준비 화면의 일이고,
    /// 지도에서도 쓸 수 있게 하면 "언제 쓰는 포인트인가"가 흐려진다.</para>
    ///
    /// <para>수치의 원천은 <see cref="RunStateService"/>(저장된 런) + 공용 밸런스 SO + 고른 캐릭터의 특화다 —
    /// <c>GameManager.BuildStatsConfig</c>와 <b>같은 순서</b>로 쌓는다. 그래야 지도에서 본 숫자와
    /// 판에 들어가서 보는 숫자가 어긋나지 않는다.</para>
    /// </summary>
    public sealed class StatusPanel : MonoBehaviour
    {
        [Header("데이터")]
        [Tooltip("공용 밸런스. 지도에는 GameManager가 없으므로 여기서 직접 읽어 실수치를 계산한다.")]
        [SerializeField] private PlayerStatsConfigSO statsConfig;

        [Header("여는 버튼 (캐릭터 얼굴)")]
        [SerializeField] private Button faceButton;
        [Tooltip("얼굴 버튼의 그림. 고른 캐릭터의 초상으로 채운다.")]
        [SerializeField] private Image faceImage;

        [Header("패널")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text soulsText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text chainText;
        [SerializeField] private TMP_Text memoryHeaderText;
        [Tooltip("삼킨 기억의 이름·효과 목록. 아이콘은 같은 패널에 붙인 MemoryStrip이 그린다.")]
        [SerializeField] private TMP_Text memoryListText;

        private void Awake()
        {
            if (faceButton != null)
                faceButton.onClick.AddListener(Toggle);

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            ApplyFace();
            Close(); // 지도에 들어오면 닫힌 상태로 시작한다 — 길이 먼저 보여야 한다
        }

        private void OnEnable()
        {
            Loc.LanguageChanged += Redraw;
            CharacterService.Changed += OnCharacterChanged;
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= Redraw;
            CharacterService.Changed -= OnCharacterChanged;
        }

        private void OnCharacterChanged(PlayerCharacterSO character)
        {
            ApplyFace();
            Redraw();
        }

        public void Toggle()
        {
            if (panelRoot == null)
                return;

            if (panelRoot.activeSelf)
                Close();
            else
                Open();
        }

        public void Open()
        {
            if (panelRoot == null)
                return;

            // 켜기 전에 그린다 — 자식 MemoryStrip이 OnEnable에서 자기 몫을 그리므로 순서를 다투지 않는다.
            Redraw();
            panelRoot.SetActive(true);
        }

        public void Close()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void ApplyFace()
        {
            PlayerCharacterSO character = CharacterService.Current;
            if (character == null)
                return;

            // 그림이 없으면 씬에 꽂아 둔 것을 그대로 둔다 — 배선이 덜 됐다고 버튼이 사라지면 안 된다.
            if (faceImage != null && character.Portrait != null)
                faceImage.sprite = character.Portrait;

            if (portraitImage != null && character.Portrait != null)
                portraitImage.sprite = character.Portrait;
        }

        private void Redraw()
        {
            if (titleText != null)
                titleText.text = Loc.GetText("status.title");

            PlayerCharacterSO character = CharacterService.Current;
            if (nameText != null)
                nameText.text = character != null ? Loc.GetText(character.NameKey) : string.Empty;

            RunState run = RunStateService.Current;
            PlayerStats stats = BuildStats(character, run);

            if (stats != null)
            {
                if (hpText != null)
                    hpText.text = Loc.GetText("status.hp", stats.MaxHp);

                if (soulsText != null)
                    soulsText.text = Loc.GetText(
                        "puzzle.souls", stats.Level, stats.Souls, stats.SoulsToNextLevel, stats.PendingPoints);

                if (statsText != null)
                    statsText.text = Loc.GetText(
                        "puzzle.stats", stats.AttackDamage, stats.DamageReduction, stats.ParryWindowSeconds);
            }

            if (chainText != null)
                chainText.text = Loc.GetText("status.chain", run.ChainStep);

            DrawMemories(run);
        }

        /// <summary>
        /// 저장된 런의 성장으로 실수치를 되살린다. <b>여기서 만든 것은 표시용</b>이고 저장되지 않는다 —
        /// 판에 들어갈 때 <c>GameManager</c>가 같은 재료로 다시 만든다.
        /// </summary>
        private PlayerStats BuildStats(PlayerCharacterSO character, RunState run)
        {
            if (statsConfig == null)
            {
                Debug.LogWarning($"{nameof(StatusPanel)}: 공용 밸런스(statsConfig)가 비어 있어 수치를 못 그립니다.", this);
                return null;
            }

            PlayerStatsConfig config = statsConfig.ToConfig();
            if (character != null && character.HasBonuses)
                character.ApplyBonuses(config);

            return new PlayerStats(config, run.Stats);
        }

        private void DrawMemories(RunState run)
        {
            List<BossMemorySO> memories = MemoryLibrary.Resolve(run.AcquiredMemoryIds);

            if (memoryHeaderText != null)
                memoryHeaderText.text = Loc.GetText("status.memories", memories.Count);

            if (memoryListText == null)
                return;

            if (memories.Count == 0)
            {
                memoryListText.text = Loc.GetText("status.memories.none");
                return;
            }

            var builder = new System.Text.StringBuilder();
            foreach (BossMemorySO memory in memories)
            {
                string name = Text(memory.NameKey);
                string desc = Text(memory.DescriptionKey);
                if (builder.Length > 0)
                    builder.Append('\n');

                builder.Append(string.IsNullOrEmpty(desc) ? name : $"{name} — {desc}");
            }

            memoryListText.text = builder.ToString();
        }

        // 키를 안 적은 기억도 있을 수 있다 — 그때는 조용히 짧아진다.
        private static string Text(string key) =>
            string.IsNullOrWhiteSpace(key) ? string.Empty : Loc.GetText(key);
    }
}
