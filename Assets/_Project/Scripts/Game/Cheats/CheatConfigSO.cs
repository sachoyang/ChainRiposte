using ChainRiposte.Game.Config;
using UnityEngine;

namespace ChainRiposte.Game.Cheats
{
    /// <summary>
    /// 치트가 쓸 재료 — 스탯 설정 + <b>지도 순서대로</b>의 스테이지 목록.
    ///
    /// <para><b>왜 에셋 한 장인가</b>: 치트를 이제 에디터 메뉴와 게임 안 버튼이 <b>같이</b> 쓴다.
    /// 에디터는 <c>AssetDatabase</c>로 에셋을 훑을 수 있지만 <b>빌드에는 그런 것이 없다.</b>
    /// 둘이 재료를 각자 구하면 "에디터에서는 되는데 빌드에서는 다른 판이 깨져 있는" 상태가 된다.
    /// <c>Resources</c>에 한 장 두면 양쪽이 같은 것을 읽는다.</para>
    ///
    /// <para><b>순서를 손으로 적는 이유</b>: 예전 에디터 치트는 에셋을 <b>이름 순</b>으로 정렬했는데
    /// 그러면 <c>Stage_Tutorial</c>이 맨 뒤로 가서, 마지막 한 판을 남기려던 것이 엉뚱하게
    /// 튜토리얼 판이 되고 <b>정작 엔딩이 있는 2-3은 이미 깨진 채</b>로 남았다.
    /// 지도의 순서는 이름이 정하는 게 아니므로 여기에 적어 둔다.</para>
    ///
    /// <para><b>이 에셋을 지우면 옵션의 치트 버튼이 사라진다</b> — 출시 빌드에서 치트를 빼는 방법이다.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ChainRiposte/Cheat Config", fileName = "CheatConfig")]
    public sealed class CheatConfigSO : ScriptableObject
    {
        /// <summary><c>Resources</c> 아래에서 찾을 이름. 파일 이름을 바꾸면 런타임이 못 찾는다.</summary>
        public const string ResourceName = "CheatConfig";

        [Tooltip("스탯 상한·포인트 비용을 읽을 설정. 비우면 기본값으로 계산한다.")]
        [SerializeField] private PlayerStatsConfigSO statsConfig;

        [Tooltip("지도에 놓인 순서대로. 맨 마지막 판은 일부러 안 깬다 — 거기서 엔딩이 나온다.")]
        [SerializeField] private StageDataSO[] orderedStages = System.Array.Empty<StageDataSO>();

        [Header("치트 수치")]
        [Tooltip("공격·방어는 하드 캡이 없다 — 치트가 무한히 올릴 수는 없으니 여기서 끊는다.")]
        [SerializeField] private int uncappedStatLevel = 10;

        [Tooltip("준비 화면의 분배 버튼도 눌러 볼 수 있게 남겨 두는 미분배 포인트.")]
        [SerializeField] private int sparePoints = 5;

        public PlayerStatsConfigSO StatsConfig => statsConfig;
        public StageDataSO[] OrderedStages => orderedStages ?? System.Array.Empty<StageDataSO>();
        public int UncappedStatLevel => Mathf.Max(0, uncappedStatLevel);
        public int SparePoints => Mathf.Max(0, sparePoints);
    }
}
