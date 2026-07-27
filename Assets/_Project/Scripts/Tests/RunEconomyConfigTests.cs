using ChainRiposte.Core.Progress;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    /// <summary>소울 경제 손잡이 (<c>Docs/PROGRESSION.md</c> §2.4) — 인컴 배수와 사슬 배수.</summary>
    public sealed class RunEconomyConfigTests
    {
        [Test]
        public void 인컴_배수가_소울을_줄인다()
        {
            var e = new RunEconomyConfig { SoulIncomeMultiplier = 0.5f, ChainSoulBonusPerStep = 0f };
            Assert.That(e.ScaleSoulIncome(10, 0), Is.EqualTo(5));
        }

        [Test]
        public void 기본값은_사슬0에서_소울을_바꾸지_않는다()
        {
            var e = new RunEconomyConfig(); // 배수 1, 사슬칸 0
            Assert.That(e.ScaleSoulIncome(50, 0), Is.EqualTo(50));
        }

        [Test]
        public void 사슬_한칸당_인컴이_는다()
        {
            var e = new RunEconomyConfig { SoulIncomeMultiplier = 1f, ChainSoulBonusPerStep = 0.1f, MaxChainMultiplier = 5f };
            Assert.That(e.ScaleSoulIncome(100, 3), Is.EqualTo(130), "1 + 3×0.1 = 1.3배");
        }

        [Test]
        public void 사슬_배수는_상한에_걸린다()
        {
            var e = new RunEconomyConfig { SoulIncomeMultiplier = 1f, ChainSoulBonusPerStep = 0.1f, MaxChainMultiplier = 1.5f };
            Assert.That(e.ScaleSoulIncome(100, 20), Is.EqualTo(150), "상한 1.5배에서 잘린다");
        }

        [Test]
        public void 인컴과_사슬이_함께_곱해진다()
        {
            var e = new RunEconomyConfig { SoulIncomeMultiplier = 0.8f, ChainSoulBonusPerStep = 0.1f, MaxChainMultiplier = 3f };
            Assert.That(e.ScaleSoulIncome(100, 5), Is.EqualTo(120), "0.8 × (1 + 5×0.1=1.5) = 1.2배");
        }

        [Test]
        public void 소울이_0이면_0이다()
        {
            Assert.That(new RunEconomyConfig().ScaleSoulIncome(0, 5), Is.EqualTo(0));
        }
    }
}
