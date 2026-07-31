using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Combat;

namespace ChainRiposte.Core.Stage
{
    /// <summary>
    /// 스테이지 하나의 정의. Game 레이어의 StageDataSO.ToConfig()로 생성된다.
    /// AnimationCurve 등 UnityEngine 타입은 Func&lt;float, float&gt;로 감싸져 주입된다.
    /// </summary>
    public sealed class StageConfig
    {
        // ── 보드 ──
        /// <summary>[x, y] true=Active. y=0이 바닥.</summary>
        public bool[,] ActiveMask;

        /// <summary>초기 배치되는 벽(장애물)의 위치.</summary>
        public IReadOnlyList<GridPos> WallPositions = Array.Empty<GridPos>();

        /// <summary>벽 내구도.</summary>
        public int WallHp = 3;

        // ── 퍼즐 규칙 ──
        /// <summary>
        /// 스왑 가능 횟수. <b>0이면 무제한</b>(기본).
        ///
        /// <para>여느 3매치와 달리 이 게임의 퍼즐 페이즈에는 <b>달성할 목표가 없다</b> —
        /// 소울을 캐며 보스를 기다리는 준비 구간이고, 판을 끝내는 것은 보스 시계와 보스 타일이다.
        /// 목표가 없는데 수를 제한하면 "N수 안에 무엇을 해라"가 성립하지 않고 그저
        /// <b>퍼즐을 열심히 푼 사람이 처벌받는다</b> — 스왑할수록 죽음에 가까워지는데
        /// 퍼즐은 참여를 유도하고 싶은 구간이다.</para>
        ///
        /// <para>파밍 상한은 이미 두 겹이다: 판 시계(<see cref="BossEngageSeconds"/>)와
        /// 소울 광맥(스테이지 매장량). 턴이 그 일을 또 할 필요가 없다.</para>
        /// </summary>
        public int TurnLimit;

        /// <summary>콤보(연쇄) 1단계당 영혼석 배수 증가량. 배수 = 1 + 증가량 × (콤보 − 1).</summary>
        public float ComboSoulMultiplierStep = 0.5f;

        /// <summary>물약 타일 1개 매치당 HP 회복량.</summary>
        public int PotionHealAmount = 10;

        /// <summary>리필 시 타일 종류별 스폰 가중치.</summary>
        public IReadOnlyList<TileSpawnWeight> SpawnWeights = Array.Empty<TileSpawnWeight>();

        /// <summary>
        /// <b>보드가 정해져 있는 판인가</b> (<c>Docs/TUTORIAL.md</c> §4.3). 켜면 엔진이 배치를
        /// 스스로 고치지 않는다 — 초기 배치의 <b>즉시 매치 재추첨</b>과 <b>데드락 리롤</b> 둘 다 끈다.
        ///
        /// <para>둘 다 "플레이어를 구해 주는" 장치인데, 대본으로 짠 판에서는 반대로 <b>대본을 망가뜨린다</b>:
        /// 재추첨은 <see cref="ScriptedTileSpawner"/>의 다음 칸을 몰래 당겨 써서 그 뒤 전체를 한 칸씩
        /// 밀어 버리고, 리롤은 애써 놓은 자리를 통째로 섞는다.</para>
        ///
        /// <para>대신 <b>판을 짠 쪽이 책임진다</b> — 시작부터 매치가 나거나 둘 수가 없으면 그대로 그렇게 된다.</para>
        /// </summary>
        public bool FixedBoard;

        /// <summary>
        /// 난수 씨앗. <b>0이면 매번 다른 판</b>(평소). 0이 아니면 초기 배치·리필·기믹이 전부
        /// 같은 순서로 나와 <b>매번 같은 판</b>이 된다 — 튜토리얼이 "저 칸을 저기로"라고
        /// 말할 수 있는 근거다.
        /// </summary>
        public int BoardSeed;

        /// <summary>
        /// <b>스폰 대본</b> — 이 순서대로 타일이 나온다(<see cref="ScriptedTileSpawner"/>).
        /// 다 떨어지면 평소 가중치 추첨으로 돌아간다.
        ///
        /// <para>보드의 <c>ActivePositions</c> 순서대로 <b>초기 배치부터</b> 소비된다는 점에 주의 —
        /// 앞쪽이 시작 보드이고 그 뒤가 리필이다.</para>
        /// </summary>
        public IReadOnlyList<TileDefinition> ScriptedSpawns = Array.Empty<TileDefinition>();

        // ── 보스 난입 (6단계에서 사용) ──
        /// <summary>x=누적 영혼석(점수) → y=보스 타일 스폰 확률(0~1).</summary>
        public Func<float, float> BossChanceByScore = _ => 0f;

        /// <summary>x=경과 초 → y=보스 타일 스폰 확률(0~1). 점수 곡선과 둘 중 최댓값 적용.</summary>
        public Func<float, float> BossChanceBySeconds = _ => 0f;

        /// <summary>
        /// 판 전체 시계 — 퍼즐 시작 후 이 시간이 지나면 보스 타일과 무관하게 보스전에 돌입한다.
        /// 0 이하면 시계를 끄고 보스 타일이 내려올 때까지 기다린다.
        ///
        /// <para>예전에는 보스 타일마다 카운트다운이 돌고 만료되면 <b>HP 반토막</b>으로 기습 돌입했다.
        /// 지금은 페널티가 규칙이 아니라 <b>플레이 결과</b>에서 나온다 — 퍼즐에서 잡몹에게 맞은 HP가
        /// 그대로 보스전으로 이어진다.</para>
        /// </summary>
        public float BossEngageSeconds = 90f;

        /// <summary>
        /// 보드 위에 동시에 존재할 수 있는 보스 타일 수.
        /// 스폰 확률은 <b>리필 타일 하나하나에</b> 굴려지므로, 이 상한이 없으면 한 웨이브에 여러 개가
        /// 쏟아져 보드가 보스 타일로 도배된다.
        /// </summary>
        public int MaxLiveBossTiles = 1;

        // ── 전투 (7단계에서 사용) ──
        /// <summary>이 스테이지에 난입하는 보스. null이면 전투 없음 (테스트/퍼즐 전용).</summary>
        public BossConfig Boss;

        // ── 스테이지 기믹 (GDD §3.6) ──
        /// <summary>이 스테이지에서 활성화되는 기믹 목록. 조합 가능.</summary>
        public IReadOnlyList<GimmickType> Gimmicks = Array.Empty<GimmickType>();

        /// <summary>기믹 밸런스 수치. 활성화된 기믹만 참조한다.</summary>
        public GimmickSettings GimmickSettings = new();

        /// <summary>ActiveMask로 빈 보드를 생성한다. 초기 벽 배치는 3단계 보드 초기화에서 수행.</summary>
        public BoardGrid CreateBoard() => new(ActiveMask);
    }
}
