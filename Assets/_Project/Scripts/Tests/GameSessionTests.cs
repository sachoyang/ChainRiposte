using System;
using ChainRiposte.Core.Flow;
using ChainRiposte.Core.Stats;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    /// <summary>
    /// 페이즈 전환 규칙. 새 페이즈를 넣고 화이트리스트 갱신을 빠뜨리면
    /// 플레이 중에야 예외로 터지므로 여기서 잡는다.
    /// </summary>
    public sealed class GameSessionTests
    {
        private static GameSession Session() => new(new PlayerStatsConfig());

        [Test]
        public void 퍼즐에서_보스_돌입_준비를_거쳐_전투로_간다()
        {
            GameSession session = Session();
            session.StartPuzzle();

            session.StartIntermission();
            Assert.That(session.Phase, Is.EqualTo(GamePhase.Intermission));

            session.StartCombat();
            Assert.That(session.Phase, Is.EqualTo(GamePhase.Combat));
        }

        [Test]
        public void 준비를_건너뛰고_바로_전투로도_갈_수_있다()
        {
            GameSession session = Session();
            session.StartPuzzle();

            session.StartCombat();

            Assert.That(session.Phase, Is.EqualTo(GamePhase.Combat));
        }

        [Test]
        public void 준비_중에도_패배할_수_있다()
        {
            // 기믹 피해나 기습 페널티로 준비 화면에서 죽는 경로
            GameSession session = Session();
            session.StartPuzzle();
            session.StartIntermission();

            session.EndStage(victory: false);

            Assert.That(session.Phase, Is.EqualTo(GamePhase.Defeat));
        }

        [Test]
        public void 끝난_뒤에는_어디로도_갈_수_없다()
        {
            GameSession session = Session();
            session.StartPuzzle();
            session.EndStage(victory: false);

            Assert.Throws<InvalidOperationException>(() => session.StartCombat());
        }

        [Test]
        public void 전환마다_이전_페이즈와_함께_통지된다()
        {
            GameSession session = Session();
            GamePhase from = GamePhase.None, to = GamePhase.None;
            session.PhaseChanged += (previous, next) => { from = previous; to = next; };

            session.StartPuzzle();
            session.StartIntermission();

            Assert.That(from, Is.EqualTo(GamePhase.Puzzle));
            Assert.That(to, Is.EqualTo(GamePhase.Intermission));
        }
    }
}
