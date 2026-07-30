using ChainRiposte.Core.Board;
using UnityEngine;

namespace ChainRiposte.Game.Config
{
    /// <summary>타일 종류 정의. 에셋 이름(name)이 타일 Id가 된다.</summary>
    [CreateAssetMenu(menuName = "ChainRiposte/Tile Definition", fileName = "Tile_")]
    public sealed class TileDefinitionSO : ScriptableObject
    {
        [SerializeField] private TileCategory category = TileCategory.Monster;

        [Tooltip("몬스터: 매치 처치 시 기본 영혼석 (콤보 배수 적용 전)")]
        [SerializeField, Min(0)] private int baseSouls = 10;

        [Tooltip("벽 등 내구도형 타일의 최대 HP (일반 타일은 0)")]
        [SerializeField, Min(0)] private int maxHp;

        [Header("성난 몬스터 — 이 종류만의 값 (0이면 스테이지의 공용값)")]
        [Tooltip("이 몬스터가 성났을 때 때리는 피해. 0이면 스테이지의 공용값(성난 몬스터 ▸ 기본 피해, 지금 8)을 쓴다. " +
            "해골은 세게, 슬라임은 약하게 같은 차등을 주고 싶을 때만 적으면 된다.")]
        [SerializeField, Min(0)] private int attackDamage;

        [Tooltip("성난 뒤 때리기까지의 시간(초). 0이면 스테이지의 공용값(박 수 × 박 길이, 지금 3 × 1.6 = 4.8초)을 쓴다.\n\n" +
            "카운트다운은 모든 몬스터가 같은 맥박(박 길이)에 맞춰 도므로, 이 값은 성날 때 " +
            "반올림(초 ÷ 박 길이)로 박 수가 된다(최소 1박). 박 길이가 1.6초면 3초 → 2박(3.2초)이다. " +
            "초를 그대로 쓰고 싶으면 스테이지의 '박 길이'를 1초로 두면 된다.")]
        [SerializeField, Min(0f)] private float attackSeconds;

        [Header("프로토타입 비주얼 (에셋 단계에서 교체)")]
        [SerializeField] private Color placeholderColor = Color.white;
        [SerializeField] private Sprite sprite;

        [Header("배경판 — 아이콘 뒤에 깔려 타일 경계를 읽히게 한다")]
        [Tooltip("이 타일 전용 배경. 비우면 BoardView의 공용 배경을 쓴다.")]
        [SerializeField] private Sprite backgroundSprite;
        [Tooltip("배경판 색(틴트). 알파가 0이면 배경을 아예 그리지 않는다 — 그림 없이 색만 넣어도 받침이 생긴다.")]
        [SerializeField] private Color backgroundColor = new(1f, 1f, 1f, 0f);

        public TileCategory Category => category;
        public Color PlaceholderColor => placeholderColor;
        public Sprite Sprite => sprite;
        public Sprite BackgroundSprite => backgroundSprite;
        public Color BackgroundColor => backgroundColor;

        private TileDefinition _cached;

        /// <summary>동일 SO는 항상 같은 TileDefinition 인스턴스를 반환한다 (뷰 매핑 키로 사용 가능).</summary>
        public TileDefinition ToDefinition() =>
            _cached ??= new TileDefinition(name, category, baseSouls, maxHp, attackDamage, attackSeconds);
    }
}
