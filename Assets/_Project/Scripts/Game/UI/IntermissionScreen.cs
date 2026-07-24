using ChainRiposte.Core.Flow;
using ChainRiposte.Core.Stats;
using ChainRiposte.Game.Characters;
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
    ///
    /// 스탯 분배(<see cref="StatAllocationPanel"/>)는 FIGHT 아래에 자식으로 붙어 이 페이즈에만 켜진다 —
    /// 퍼즐 화면에는 버튼을 두지 않는다. 뒤의 퍼즐판은 살짝 어둡게 덮어 여기가 준비 시간임을 알린다.
    /// </summary>
    public sealed class IntermissionScreen : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        [Header("씬 참조 (빌더가 자동 배선)")]
        [Tooltip("이 페이즈 동안만 켜지는 화면 전체 루트 (딤 + 아래 띠)")]
        [SerializeField] private GameObject panelRoot;
        [Tooltip("뒤의 퍼즐판을 덮는 어둠. 여기가 준비 화면이라는 것을 한눈에 알리는 장치다.")]
        [SerializeField] private Image dimOverlay;
        [Tooltip("어둠의 세기. 퍼즐판이 비쳐 보일 정도로만 — 완전히 가리면 다음 판이 안 읽힌다.")]
        [SerializeField] private Color dimColor = new(0f, 0f, 0f, 0.45f);
        [SerializeField] private TMP_Text titleText;
        [Tooltip("화면 위쪽에서 다가오는 보스 그림자 (예고 연출). 비워도 동작한다.")]
        [SerializeField] private BossShadow bossShadow;
        [Tooltip("보스가 다가온다는 경고 + 업그레이드 재촉")]
        [SerializeField] private TMP_Text warningText;
        [Tooltip("남은 포인트 안내")]
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private Button fightButton;

        [Header("현황 — 여기서 성장을 정하므로 지갑과 현재 수치가 같이 보여야 한다")]
        [Tooltip("체력")]
        [SerializeField] private TMP_Text hpText;
        [Tooltip("레벨 / 영혼석 / 남은 포인트")]
        [SerializeField] private TMP_Text soulsText;
        [Tooltip("현재 공격·방어·판정")]
        [SerializeField] private TMP_Text statsText;

        [Header("업그레이드 NPC")]
        [Tooltip("성녀 — ATK/DEF를 올리면 반응한다. 그림은 고른 캐릭터를 따라간다.")]
        [SerializeField] private Image saintImage;
        [SerializeField] private TMP_Text saintLabel;
        [SerializeField] private NpcReaction saintReaction;
        [Tooltip("캐릭터에 성녀 그림이 없을 때 쓸 기본 그림")]
        [SerializeField] private Sprite fallbackSaintSprite;
        [Tooltip("대장장이 — PARRY(판정)를 올리면 반응한다. 캐릭터와 무관하게 한 명.")]
        [SerializeField] private Image blacksmithImage;
        [SerializeField] private TMP_Text blacksmithLabel;
        [SerializeField] private NpcReaction blacksmithReaction;
        [Tooltip("스프라이트가 비었을 때 자리를 보여 줄 색")]
        [SerializeField] private Color npcPlaceholderColor = new(0.30f, 0.28f, 0.38f, 1f);

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
            // 코드가 매번 채우는 문구라 LocalizedText가 못 잡는다 — 언어가 바뀌면 여기서 다시 그린다.
            Loc.LanguageChanged += Refresh;

            fightButton.onClick.AddListener(() => _session.StartCombat());
            panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            Loc.LanguageChanged -= Refresh;
            if (_session == null)
                return;

            _session.PhaseChanged -= OnPhaseChanged;
            _session.Stats.StatAllocated -= OnStatAllocated;
            _session.Stats.SoulsChanged -= OnSoulsChanged;
        }

        /// <summary>
        /// 누가 강화해 줬는지를 몸짓으로 보여 준다 —
        /// <b>성녀는 공격·방어</b>(축복), <b>대장장이는 판정</b>(무기를 벼려 패링 창을 넓힌다).
        /// </summary>
        private void OnStatAllocated(StatType stat, int newLevel)
        {
            NpcReaction reaction = stat == StatType.Parry ? blacksmithReaction : saintReaction;
            if (reaction != null)
                reaction.Play();

            Refresh();
        }

        private void OnSoulsChanged(int souls, int required) => Refresh();

        private void OnPhaseChanged(GamePhase previous, GamePhase next)
        {
            bool active = next == GamePhase.Intermission;
            panelRoot.SetActive(active);

            // 그림자는 페이즈가 켜지는 순간 한 번만 다가온다 — Refresh(스탯 변경마다 호출)에 두면 매번 다시 다가온다.
            if (bossShadow != null)
            {
                if (active)
                    bossShadow.Show(ResolveBossShadowSprite());
                else
                    bossShadow.Hide();
            }

            if (active)
                Refresh();
        }

        /// <summary>
        /// 이 판 보스의 그림. 전투 화면과 같은 규칙 — 테마가 겉모습을 갈아 끼우고, 없으면 SO 그림.
        /// (여기선 <see cref="BossShadow"/>가 어둡게 칠하므로 원본 색은 중요하지 않다.)
        /// </summary>
        private Sprite ResolveBossShadowSprite()
        {
            Config.BossDataSO boss = gameManager.StageData != null ? gameManager.StageData.BossData : null;
            if (boss == null)
                return null;

            if (Theming.ThemeService.TryGetBoss(boss.BossId, out Theming.ThemeSO.BossEntry themed) && themed.sprite != null)
                return themed.sprite;

            return boss.BattleSprite;
        }

        private void Refresh()
        {
            if (!panelRoot.activeSelf)
                return;

            if (dimOverlay != null)
                dimOverlay.color = dimColor;

            if (titleText != null)
                titleText.text = Loc.GetText("intermission.title");

            if (warningText != null)
                warningText.text = Loc.GetText("intermission.warning");

            // HUD는 딤 뒤에 깔려 읽기 어렵다 — 성장을 정하는 화면이니 필요한 숫자는 여기 다시 적는다.
            Core.Stats.PlayerStats stats = _session.Stats;
            if (hpText != null)
                hpText.text = Loc.GetText("puzzle.hp", _session.Health.Current, _session.Health.Max);

            if (soulsText != null)
                soulsText.text = Loc.GetText(
                    "puzzle.souls", stats.Level, stats.Souls, stats.SoulsToNextLevel, stats.PendingPoints);

            if (statsText != null)
                statsText.text = Loc.GetText(
                    "puzzle.stats", stats.AttackDamage, stats.DamageReduction, stats.ParryWindowSeconds);

            if (pointsText != null)
            {
                int points = stats.PendingPoints;
                pointsText.text = points > 0
                    ? Loc.GetText("intermission.points", points)
                    : Loc.GetText("intermission.nopoints");
            }

            // 성녀는 고른 캐릭터를 따라온다. 캐릭터에 그림이 없으면 씬에 꽂아 둔 기본 성녀.
            PlayerCharacterSO character = CharacterService.Current;
            Sprite saint = character != null && character.SaintSprite != null
                ? character.SaintSprite
                : fallbackSaintSprite;
            if (saintImage != null && saint != null)
                saintImage.sprite = saint;

            RefreshNpc(saintImage, saintLabel, saintReaction, "intermission.npc.saint");
            RefreshNpc(blacksmithImage, blacksmithLabel, blacksmithReaction, "intermission.npc.blacksmith");
        }

        /// <summary>스프라이트를 아직 안 넣었어도 자리가 보이도록 플레이스홀더 색을 칠한다.</summary>
        private void RefreshNpc(Image image, TMP_Text label, NpcReaction reaction, string locKey)
        {
            if (image != null)
            {
                image.color = image.sprite != null ? Color.white : npcPlaceholderColor;
                // 반응이 되돌아갈 '쉬는 색'도 같이 옮겨 준다 — 안 하면 번쩍인 뒤 옛 색으로 돌아간다.
                if (reaction != null)
                    reaction.ResetRestColor(image.color);
            }

            if (label != null)
                label.text = Loc.GetText(locKey);
        }
    }
}
