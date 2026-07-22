using ChainRiposte.Core.Flow;
using ChainRiposte.Game.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 보스 돌입 직전의 준비 화면.
    ///
    /// <b>시간 제한이 없다.</b> 퍼즐은 보스 카운트다운으로 계속 쫓기는 구간이라,
    /// 성장을 결정하는 순간까지 쫓기면 피로해진다. 여기서만큼은 플레이어가 버튼을 눌러야 넘어간다.
    /// 스탯 분배는 기존 퍼즐 HUD의 +ATK/+DEF/+PARRY 버튼을 그대로 쓴다 — 배우던 UI가 바뀌지 않는다.
    /// </summary>
    public sealed class IntermissionScreen : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        [Header("씬 참조 (빌더가 자동 배선)")]
        [Tooltip("이 페이즈 동안만 켜지는 패널")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [Tooltip("남은 포인트 안내")]
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private Button fightButton;

        private GameSession _session;

        private void Awake()
        {
            if (gameManager == null || panelRoot == null || fightButton == null)
            {
                Debug.LogError(
                    $"{nameof(IntermissionScreen)}: 참조가 비어 있습니다. " +
                    "Tools ▸ ChainRiposte ▸ Build Main Scene UI 를 실행하세요.", this);
                enabled = false;
                return;
            }

            _session = gameManager.Session;
            _session.PhaseChanged += OnPhaseChanged;
            _session.Stats.StatAllocated += OnStatAllocated;
            _session.Stats.SoulsChanged += OnSoulsChanged;

            fightButton.onClick.AddListener(() => _session.StartCombat());
            panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_session == null)
                return;

            _session.PhaseChanged -= OnPhaseChanged;
            _session.Stats.StatAllocated -= OnStatAllocated;
            _session.Stats.SoulsChanged -= OnSoulsChanged;
        }

        private void OnStatAllocated(Core.Stats.StatType stat, int newLevel) => Refresh();
        private void OnSoulsChanged(int souls, int required) => Refresh();

        private void OnPhaseChanged(GamePhase previous, GamePhase next)
        {
            bool active = next == GamePhase.Intermission;
            panelRoot.SetActive(active);
            if (active)
                Refresh();
        }

        private void Refresh()
        {
            if (!panelRoot.activeSelf)
                return;

            if (titleText != null)
                titleText.text = Loc.GetText("intermission.title");

            if (pointsText != null)
            {
                int points = _session.Stats.PendingPoints;
                pointsText.text = points > 0
                    ? Loc.GetText("intermission.points", points)
                    : Loc.GetText("intermission.nopoints");
            }
        }
    }
}
