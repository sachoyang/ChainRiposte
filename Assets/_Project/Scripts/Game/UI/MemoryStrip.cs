using System.Collections.Generic;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.Localization;
using ChainRiposte.Game.Memories;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 지금까지 삼킨 <b>보스의 기억</b>을 작은 아이콘 줄로 보여 준다 (<c>Docs/PROGRESSION.md</c> §2.2).
    ///
    /// <para>개수가 데이터로 정해지므로 <b>씬에 둔 원본 아이콘 하나를 복제</b>한다 —
    /// 노트 원·캐릭터 카드와 같은 규칙이다. 아트가 생기면 원본의 <c>Image</c> 스프라이트만 갈아 끼우면 된다.</para>
    ///
    /// <para>이 컴포넌트는 화면마다 <b>붙이는 것만으로</b> 동작한다(스스로 <see cref="GameManager"/>를 읽고
    /// 켜질 때 다시 그린다) — 퍼즐 HUD·준비 화면·결과 화면 쪽 코드를 건드리지 않으려는 것이다.
    /// 어느 화면에 기억을 띄울지는 코드가 아니라 <b>컴포넌트를 어디 붙였는지</b>가 정한다
    /// (<c>ThemedSprite</c>·<c>LocalizedText</c>와 같은 방식).</para>
    /// </summary>
    public sealed class MemoryStrip : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("이 판의 GameManager. 비우면 씬에서 찾는다.")]
        [SerializeField] private GameManager gameManager;
        [Tooltip("아이콘이 늘어설 자리(가로 Layout Group을 붙여 두면 정렬은 Unity가 한다). " +
                 "비우면 이 오브젝트 자신.")]
        [SerializeField] private RectTransform container;
        [Tooltip("복제할 원본 아이콘. 스프라이트를 비워 두면 그 그림이 기본값이 된다 " +
                 "— 기억 에셋에 아이콘을 안 꽂았을 때 이 그림이 그대로 남는다.")]
        [SerializeField] private Image iconTemplate;

        [Header("표시")]
        [Tooltip("기억이 하나도 없으면 줄을 숨긴다. 빈 칸이 남아 있으면 '뭔가 안 나온 것'처럼 보인다.")]
        [SerializeField] private bool hideWhenEmpty = true;
        [Tooltip("이 판에서 새로 삼킨 기억을 강조한다(결과 화면용). 나머지는 어둡게 남는다 " +
                 "— 지우면 원래 몇 개를 모았는지 알 수 없다.")]
        [SerializeField] private bool highlightGained;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color dimColor = new(1f, 1f, 1f, 0.45f);
        [SerializeField, Min(1f)] private float gainedScale = 1.3f;

        [Tooltip("선택 — 새로 삼킨 기억의 이름·효과를 적는다(결과 화면). 새 기억이 없으면 비워 둔다.")]
        [SerializeField] private TMP_Text gainedLabel;

        private readonly List<Image> _icons = new();

        private void Awake()
        {
            if (gameManager == null)
                gameManager = FindFirstObjectByType<GameManager>();

            if (container == null)
                container = transform as RectTransform;

            // 원본은 틀일 뿐이라 화면에 나오지 않는다 — 복제본만 켠다.
            if (iconTemplate != null)
                iconTemplate.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (gameManager != null)
                gameManager.MemoryGained += OnMemoryGained;

            Loc.LanguageChanged += Redraw;
            Redraw();
        }

        private void OnDisable()
        {
            if (gameManager != null)
                gameManager.MemoryGained -= OnMemoryGained;

            Loc.LanguageChanged -= Redraw;
        }

        private void OnMemoryGained(BossMemorySO memory) => Redraw();

        private void Redraw()
        {
            if (iconTemplate == null || container == null)
                return;

            List<BossMemorySO> memories = Collect();

            if (hideWhenEmpty && container.gameObject.activeSelf == (memories.Count == 0))
                container.gameObject.SetActive(memories.Count > 0);

            for (int i = 0; i < memories.Count; i++)
                Apply(IconAt(i), memories[i]);

            // 남는 아이콘은 지우지 않고 꺼 둔다 — 판마다 개수가 오가므로 매번 만들고 부수지 않는다.
            for (int i = memories.Count; i < _icons.Count; i++)
                _icons[i].gameObject.SetActive(false);

            DrawGainedLabel();
        }

        /// <summary>
        /// 그릴 목록 = <b>진입 시점에 가진 것</b> + <b>이 판에서 방금 삼킨 것</b>.
        /// 방금 삼킨 것은 세이브에 들어갔지만 진입 시점 사본에는 없으므로 여기서 뒤에 붙인다
        /// (진입 사본을 안 쓰고 세이브를 직접 읽으면, 전투 중에 이 판의 보스 기억이 이미 있는 것처럼 보인다).
        /// </summary>
        private List<BossMemorySO> Collect()
        {
            if (gameManager == null)
                return new List<BossMemorySO>();

            List<BossMemorySO> memories = MemoryLibrary.Resolve(gameManager.MemoriesAtEntry);
            BossMemorySO gained = gameManager.GainedMemory;
            if (gained != null && !memories.Contains(gained))
                memories.Add(gained);

            return memories;
        }

        private void Apply(Image icon, BossMemorySO memory)
        {
            // 기억 에셋에 아이콘이 없으면 원본의 그림을 그대로 둔다 — 안 꽂았다고 칸이 비면 안 된다.
            if (memory.Icon != null)
                icon.sprite = memory.Icon;

            bool isGained = highlightGained && memory == gameManager.GainedMemory;
            icon.color = highlightGained && !isGained ? dimColor : normalColor;
            icon.transform.localScale = Vector3.one * (isGained ? gainedScale : 1f);
            icon.gameObject.SetActive(true);
            icon.gameObject.name = $"Memory_{memory.MemoryId}";
        }

        private Image IconAt(int index)
        {
            while (_icons.Count <= index)
            {
                Image clone = Instantiate(iconTemplate, container);
                clone.transform.localScale = Vector3.one;
                _icons.Add(clone);
            }

            return _icons[index];
        }

        /// <summary>새로 삼킨 기억의 이름·효과 한 줄. 강조를 안 쓰거나 새 기억이 없으면 비운다.</summary>
        private void DrawGainedLabel()
        {
            if (gainedLabel == null)
                return;

            BossMemorySO gained = highlightGained && gameManager != null ? gameManager.GainedMemory : null;
            if (gained == null)
            {
                gainedLabel.text = string.Empty;
                return;
            }

            string title = Loc.GetText("result.memory.gained", Text(gained.NameKey));
            string detail = Text(gained.DescriptionKey);
            gainedLabel.text = string.IsNullOrEmpty(detail) ? title : $"{title}\n{detail}";
        }

        // 키를 안 적은 기억도 있을 수 있다 — 그때는 빈 문자열이라 줄이 조용히 짧아진다.
        private static string Text(string key) =>
            string.IsNullOrWhiteSpace(key) ? string.Empty : Loc.GetText(key);
    }
}
