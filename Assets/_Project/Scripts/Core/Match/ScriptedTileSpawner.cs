using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Match
{
    /// <summary>
    /// <b>정해진 순서대로</b> 타일을 뱉는 스포너 (<c>Docs/TUTORIAL.md</c> §4.3).
    /// 대본이 다 떨어지면 평소 스포너에 넘긴다.
    ///
    /// <para>튜토리얼처럼 "의도한 수만 걸리는 판"을 만들려고 있는 것이다. 엔진에 새 개념을
    /// 넣지 않는다 — <see cref="ITileSpawner"/>는 메서드 하나짜리라 여기만 갈아 끼우면
    /// 초기 배치도 리필도 전부 대본을 따른다.</para>
    ///
    /// <para><b>고정 씨앗과 짝이다.</b> 대본만으로는 부족하다 — 대본이 떨어진 뒤의 리필과
    /// 기믹의 무작위가 판마다 달라지므로, <c>PuzzleEngine(config, spawner, rng)</c>에
    /// 씨앗을 고정한 난수를 같이 넣어야 매번 같은 판이 나온다.</para>
    ///
    /// <para>대본이 <b>초기 배치부터</b> 소비된다는 점에 주의. 보드의
    /// <see cref="BoardGrid.ActivePositions"/> 순서대로 한 칸에 하나씩 들어가고,
    /// 그 뒤에 나오는 것이 리필이다.</para>
    /// </summary>
    public sealed class ScriptedTileSpawner : ITileSpawner
    {
        private readonly IReadOnlyList<TileDefinition> _script;
        private readonly ITileSpawner _fallback;
        private int _index;

        public ScriptedTileSpawner(IReadOnlyList<TileDefinition> script, ITileSpawner fallback)
        {
            _script = script ?? Array.Empty<TileDefinition>();
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        }

        /// <summary>대본을 다 읽었는가 — 이후로는 평소 스포너가 답한다.</summary>
        public bool Exhausted => _index >= _script.Count;

        /// <summary>지금까지 읽은 칸 수. 대본이 보드 크기와 맞는지 확인할 때 쓴다.</summary>
        public int Consumed => _index;

        public TileDefinition NextDefinition() =>
            _index < _script.Count ? _script[_index++] : _fallback.NextDefinition();
    }
}
