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

        [Header("프로토타입 비주얼 (에셋 단계에서 교체)")]
        [SerializeField] private Color placeholderColor = Color.white;
        [SerializeField] private Sprite sprite;

        public TileCategory Category => category;
        public Color PlaceholderColor => placeholderColor;
        public Sprite Sprite => sprite;

        private TileDefinition _cached;

        /// <summary>동일 SO는 항상 같은 TileDefinition 인스턴스를 반환한다 (뷰 매핑 키로 사용 가능).</summary>
        public TileDefinition ToDefinition() =>
            _cached ??= new TileDefinition(name, category, baseSouls, maxHp);
    }
}
