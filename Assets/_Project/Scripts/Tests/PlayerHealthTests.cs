using ChainRiposte.Core.Stats;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    public sealed class PlayerHealthTests
    {
        [Test]
        public void 회복은_최대치를_넘지_않는다()
        {
            var health = new PlayerHealth(100);
            health.ApplyDamage(30);

            health.Heal(50);

            Assert.That(health.Current, Is.EqualTo(100));
        }

        [Test]
        public void 피해가_최대치를_넘으면_0에서_멈추고_사망한다()
        {
            var health = new PlayerHealth(100);

            bool dead = health.ApplyDamage(150);

            Assert.That(dead, Is.True);
            Assert.That(health.Current, Is.EqualTo(0));
            Assert.That(health.IsDead, Is.True);
        }

        [Test]
        public void 비율_설정은_기습_페널티에_쓰인다()
        {
            var health = new PlayerHealth(100);

            health.SetFraction(0.5f);

            Assert.That(health.Current, Is.EqualTo(50));
        }
    }
}
