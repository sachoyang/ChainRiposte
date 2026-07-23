using ChainRiposte.Core.Stats;
using ChainRiposte.Game.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// +ATK / +DEF / +PARRY 분배 버튼 묶음.
    ///
    /// <b>퍼즐 화면에는 두지 않는다.</b> 퍼즐 중에는 보드를 보는 것이 전부이고,
    /// 성장은 보스 돌입 전 준비 화면에서 시간에 안 쫓기며 정하면 된다.
    /// 그래서 이 묶음은 <see cref="IntermissionScreen"/>의 자식으로 살고 그 페이즈에만 켜진다.
    ///
    /// UI는 씬에 실물로 배치하고 여기서는 참조만 받는다.
    /// </summary>
    public sealed class StatAllocationPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        [Header("씬 참조 (빌더가 자동 배선)")]
        [SerializeField] private Button attackButton;
        [SerializeField] private Button defenseButton;
        [SerializeField] private Button parryButton;

        private PlayerStats _stats;

        private void Awake()
        {
            if (gameManager == null || attackButton == null || defenseButton == null || parryButton == null)
            {
                Debug.LogError($"{nameof(StatAllocationPanel)}: 참조가 비어 있습니다. " +
                    "Tools ▸ ChainRiposte ▸ Build Main Scene UI 를 실행하세요.", this);
                enabled = false;
                return;
            }

            _stats = gameManager.Session.Stats;
            attackButton.onClick.AddListener(() => Allocate(StatType.Attack));
            defenseButton.onClick.AddListener(() => Allocate(StatType.Defense));
            parryButton.onClick.AddListener(() => Allocate(StatType.Parry));

            _stats.SoulsChanged += OnSoulsChanged;
            _stats.StatAllocated += OnStatAllocated;
        }

        private void OnDestroy()
        {
            if (_stats == null)
                return;

            _stats.SoulsChanged -= OnSoulsChanged;
            _stats.StatAllocated -= OnStatAllocated;
        }

        // 코드가 매번 채우는 라벨이라 LocalizedText를 붙이지 않는다 — 언어 전환은 여기서 다시 그린다.
        private void OnEnable()
        {
            Loc.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => Loc.LanguageChanged -= Refresh;

        private void OnSoulsChanged(int souls, int required) => Refresh();
        private void OnStatAllocated(StatType stat, int newLevel) => Refresh();

        private void Allocate(StatType stat)
        {
            if (_stats == null || !_stats.CanAllocate(stat))
                return;

            _stats.Allocate(stat);
        }

        private void Refresh()
        {
            if (_stats == null || !isActiveAndEnabled)
                return;

            SetButton(attackButton, "puzzle.alloc.attack", StatType.Attack);
            SetButton(defenseButton, "puzzle.alloc.defense", StatType.Defense);
            SetButton(parryButton, "puzzle.alloc.parry", StatType.Parry);
        }

        private void SetButton(Button button, string locKey, StatType stat)
        {
            // 상한에 걸린 것과 포인트가 모자란 것은 다르다 — 비용이 2 이상인 스탯이 생기면서 갈렸다.
            string label = _stats.IsAtCap(stat)
                ? Loc.GetText(locKey + ".max")
                : Loc.GetText(locKey, _stats.GetStatLevel(stat));

            // 값이 1보다 비싸면 얼마가 드는지 버튼에 적는다 — 안 적으면 왜 안 눌리는지 알 수 없다.
            int cost = _stats.GetPointCost(stat);
            if (cost > 1 && !_stats.IsAtCap(stat))
                label += Loc.GetText("puzzle.alloc.cost", cost);

            button.interactable = _stats.CanAllocate(stat);
            button.GetComponentInChildren<TMP_Text>().text = label;
        }
    }
}
