using System;
using System.Collections.Generic;
using ChainRiposte.Game.Characters;
using ChainRiposte.Game.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 새 게임에서 캐릭터를 고르는 화면.
    ///
    /// 카드 개수는 <c>Resources/Characters</c> 의 에셋 수로 정해지므로 씬의 <b>템플릿 카드 하나</b>를
    /// 복제해 만든다 (옵션의 언어 버튼, 퍼즐 보드 타일과 같은 규칙).
    /// 고르는 것만 하고 그 뒤에 무엇을 할지는 <see cref="Chosen"/> 을 듣는 쪽이 정한다.
    /// </summary>
    public sealed class CharacterSelectPanel : MonoBehaviour
    {
        [Header("씬 참조 (빌더가 자동 배선)")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("카드가 복제되어 붙을 부모")]
        [SerializeField] private Transform cardParent;
        [Tooltip("복제 원본. 시작할 때 꺼진다.")]
        [SerializeField] private Button cardTemplate;
        [Tooltip("고른 캐릭터의 설명. 없으면 생략된다.")]
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        [Header("선택 표시 색")]
        [SerializeField] private Color selectedColor = new(0.55f, 0.16f, 0.18f, 1f);
        [SerializeField] private Color unselectedColor = new(0.20f, 0.18f, 0.24f, 1f);

        private readonly List<(Button button, PlayerCharacterSO character)> _cards = new();
        private PlayerCharacterSO _selected;
        private bool _built;

        /// <summary>확인까지 누른 순간. 인자는 고른 캐릭터.</summary>
        public event Action<PlayerCharacterSO> Chosen;

        /// <summary>뒤로 가기.</summary>
        public event Action Cancelled;

        /// <summary>고를 것이 하나뿐(또는 없음)이면 화면을 띄울 이유가 없다.</summary>
        public static bool IsNeeded => CharacterService.All.Count > 1;

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(Confirm);
            if (backButton != null)
                backButton.onClick.AddListener(() => Cancelled?.Invoke());

            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            Loc.LanguageChanged += Refresh;
            Build();
            Refresh();
        }

        private void OnDisable() => Loc.LanguageChanged -= Refresh;

        public void Open()
        {
            gameObject.SetActive(true);
            // 저장된 선택이 있으면 거기서 시작 — 다시 고르는 사람이 자기 캐릭터를 찾기 쉽다.
            _selected = CharacterService.Current;
            Refresh();
        }

        private void Build()
        {
            if (_built || cardTemplate == null || cardParent == null)
                return;

            _built = true;
            cardTemplate.gameObject.SetActive(false);

            foreach (PlayerCharacterSO character in CharacterService.All)
            {
                Button card = Instantiate(cardTemplate, cardParent);
                card.gameObject.name = $"Character_{character.CharacterId}";
                card.gameObject.SetActive(true);

                // 카드 안의 Portrait 이미지 — 이름으로 찾아 두면 씬에서 카드 모양을 바꿔도 계속 붙는다.
                Transform portrait = card.transform.Find("Portrait");
                if (portrait != null)
                {
                    var image = portrait.GetComponent<Image>();
                    if (image != null && character.Portrait != null)
                    {
                        image.sprite = character.Portrait;
                        image.preserveAspect = true;
                        image.color = Color.white;
                    }
                }

                PlayerCharacterSO captured = character;
                card.onClick.AddListener(() => SelectCard(captured));
                _cards.Add((card, character));
            }

            if (_selected == null && _cards.Count > 0)
                _selected = _cards[0].character;
        }

        private void SelectCard(PlayerCharacterSO character)
        {
            _selected = character;
            Refresh();
        }

        private void Confirm()
        {
            if (_selected == null)
                return;

            CharacterService.Select(_selected);
            Chosen?.Invoke(_selected);
        }

        private void Refresh()
        {
            if (titleText != null)
                titleText.text = Loc.GetText("character.select.title");

            foreach ((Button button, PlayerCharacterSO character) in _cards)
            {
                bool selected = character == _selected;

                var image = button.GetComponent<Image>();
                if (image != null)
                    image.color = selected ? selectedColor : unselectedColor;

                // 이름표 — 카드 안의 "Name" 텍스트. 코드가 채우므로 LocalizedText는 붙이지 않는다.
                Transform nameSlot = button.transform.Find("Name");
                if (nameSlot != null)
                {
                    var label = nameSlot.GetComponent<TMP_Text>();
                    if (label != null)
                        label.text = Loc.GetText(character.NameKey);
                }
            }

            if (descriptionText != null)
            {
                descriptionText.text = _selected != null && !string.IsNullOrEmpty(_selected.DescriptionKey)
                    ? Loc.GetText(_selected.DescriptionKey)
                    : string.Empty;
            }

            if (confirmButton != null)
                confirmButton.interactable = _selected != null;
        }
    }
}
