using System.Collections.Generic;
using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Match
{
    /// <summary>같은 종류 타일 3개 이상이 이룬 매치 하나. L/T자로 겹친 가로+세로 런은 한 그룹으로 병합된다.</summary>
    public sealed class MatchGroup
    {
        public TileDefinition Definition { get; }
        public IReadOnlyList<GridPos> Positions { get; }

        public MatchGroup(TileDefinition definition, IReadOnlyList<GridPos> positions)
        {
            Definition = definition;
            Positions = positions;
        }
    }
}
