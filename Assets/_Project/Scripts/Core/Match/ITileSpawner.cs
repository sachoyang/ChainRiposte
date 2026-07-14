using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Match
{
    /// <summary>
    /// 리필 시 스폰될 타일 종류를 결정한다.
    /// 6단계 보스 난입은 이 인터페이스를 감싸는 데코레이터(확률적으로 보스 타일 반환)로 구현된다.
    /// </summary>
    public interface ITileSpawner
    {
        TileDefinition NextDefinition();
    }
}
