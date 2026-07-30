using ChainRiposte.Core.Progress;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    public sealed class StageProgressTests
    {
        private static readonly string[] Stages =
        {
            "Stage_1_1", "Stage_1_2", "Stage_1_3", "Stage_2_1", "Stage_2_2", "Stage_2_3",
        };

        [Test]
        public void 첫_스테이지는_항상_열려_있고_나머지는_잠긴다()
        {
            var progress = new StageProgress();

            Assert.That(progress.IsUnlocked(Stages, 0), Is.True);
            Assert.That(progress.IsUnlocked(Stages, 1), Is.False);
            Assert.That(progress.IsUnlocked(Stages, 5), Is.False);
        }

        [Test]
        public void 클리어하면_바로_다음_스테이지만_열린다()
        {
            var progress = new StageProgress();

            progress.MarkCleared("Stage_1_1");

            Assert.That(progress.IsUnlocked(Stages, 1), Is.True);
            Assert.That(progress.IsUnlocked(Stages, 2), Is.False);
        }

        [Test]
        public void 월드를_건너뛰어도_경로_순서대로_이어진다()
        {
            var progress = new StageProgress(new[] { "Stage_1_1", "Stage_1_2", "Stage_1_3" });

            Assert.That(progress.IsUnlocked(Stages, 3), Is.True, "1-3을 깼으면 2-1이 열려야 한다");
            Assert.That(progress.HighestUnlockedIndex(Stages), Is.EqualTo(3));
        }

        [Test]
        public void 중간을_건너뛴_기록은_그_앞에서_막힌다()
        {
            // 치트/데이터 손상으로 1-2 기록만 있는 경우 — 1-1을 안 깼으므로 1-2는 잠긴 채다
            var progress = new StageProgress(new[] { "Stage_1_2" });

            Assert.That(progress.IsUnlocked(Stages, 1), Is.False);
            Assert.That(progress.HighestUnlockedIndex(Stages), Is.EqualTo(0));
        }

        [Test]
        public void 이미_깬_스테이지를_다시_깨면_저장이_필요없다()
        {
            var progress = new StageProgress();

            Assert.That(progress.MarkCleared("Stage_1_1"), Is.True);
            Assert.That(progress.MarkCleared("Stage_1_1"), Is.False);
            Assert.That(progress.ClearedStageIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void 직렬화하고_되읽으면_기록이_유지된다()
        {
            var progress = new StageProgress(new[] { "Stage_1_1", "Stage_1_2" });

            StageProgress restored = StageProgress.Deserialize(progress.Serialize());

            Assert.That(restored.IsCleared("Stage_1_1"), Is.True);
            Assert.That(restored.IsCleared("Stage_1_2"), Is.True);
            Assert.That(restored.IsCleared("Stage_1_3"), Is.False);
        }

        [Test]
        public void 진입해야_정보가_공개된다()
        {
            var progress = new StageProgress();

            Assert.That(progress.IsRevealed("Stage_1_2"), Is.False, "해금만으로는 공개하지 않는다");

            Assert.That(progress.MarkAttempted("Stage_1_2"), Is.True);
            Assert.That(progress.MarkAttempted("Stage_1_2"), Is.False, "두 번째부터는 저장할 필요 없다");
            Assert.That(progress.IsRevealed("Stage_1_2"), Is.True);
            Assert.That(progress.IsCleared("Stage_1_2"), Is.False, "진입은 클리어가 아니다");
        }

        [Test]
        public void 클리어하면_진입_기록도_함께_남는다()
        {
            var progress = new StageProgress();

            progress.MarkCleared("Stage_1_1");

            Assert.That(progress.IsRevealed("Stage_1_1"), Is.True, "깼다면 당연히 들어가 본 것");
        }

        [Test]
        public void 시도_기록도_직렬화된다()
        {
            var progress = new StageProgress();
            progress.MarkCleared("Stage_1_1");
            progress.MarkAttempted("Stage_1_2");

            StageProgress restored = StageProgress.Deserialize(progress.Serialize());

            Assert.That(restored.IsCleared("Stage_1_1"), Is.True);
            Assert.That(restored.IsCleared("Stage_1_2"), Is.False);
            Assert.That(restored.IsRevealed("Stage_1_2"), Is.True, "시도만 한 스테이지도 공개 상태가 유지돼야 한다");
            Assert.That(restored.IsRevealed("Stage_1_3"), Is.False);
        }

        [Test]
        public void 기존_v1_세이브를_읽으면_클리어분이_공개상태로_올라온다()
        {
            // v1 형식 = 클리어 id를 ';'로 이은 것뿐. 사용자의 기존 세이브가 이 형식이다.
            StageProgress restored = StageProgress.Deserialize("Stage_1_1;Stage_1_2");

            Assert.That(restored.IsCleared("Stage_1_1"), Is.True);
            Assert.That(restored.IsCleared("Stage_1_2"), Is.True);
            Assert.That(restored.IsRevealed("Stage_1_1"), Is.True, "깬 스테이지는 공개 상태여야 한다");
            Assert.That(restored.IsRevealed("Stage_1_3"), Is.False);
        }

        [Test]
        public void 빈_세이브를_읽어도_안전하다()
        {
            StageProgress progress = StageProgress.Deserialize(null);

            Assert.That(progress.ClearedStageIds.Count, Is.EqualTo(0));
            Assert.That(progress.HighestUnlockedIndex(Stages), Is.EqualTo(0));
        }

        [Test]
        public void 다음_회차는_클리어만_지우고_공개는_남긴다()
        {
            var progress = new StageProgress(new[] { "Stage_1_1", "Stage_1_2" });
            progress.MarkAttempted("Stage_1_3");

            progress.BeginNewGamePlus();

            Assert.That(progress.ClearedStageIds.Count, Is.Zero, "사슬을 처음부터 다시 오른다");
            Assert.That(progress.IsUnlocked(Stages, 1), Is.False, "1-2는 다시 잠긴다");
            Assert.That(progress.IsUnlocked(Stages, 0), Is.True, "첫 판은 언제나 열려 있다");
            Assert.That(progress.IsRevealed("Stage_1_1"), Is.True, "이미 본 보스를 다시 ??? 로 가리지 않는다");
            Assert.That(progress.IsRevealed("Stage_1_3"), Is.True);
        }

        [Test]
        public void 빈_id는_무시한다()
        {
            // 노드에 스테이지가 비어 있으면 빈 문자열이 들어온다 → 클리어로 취급하면 안 된다
            var progress = new StageProgress(new[] { "", "  ", null });

            Assert.That(progress.ClearedStageIds.Count, Is.EqualTo(0));
            Assert.That(progress.IsCleared(""), Is.False);
        }
    }
}
