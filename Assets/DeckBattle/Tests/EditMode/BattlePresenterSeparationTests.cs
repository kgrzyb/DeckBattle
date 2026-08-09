using System.Reflection;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class BattlePresenterSeparationTests
    {
        [Test]
        public void BattleView_DelegatesUnitsProjectilesAndVfxToPresenters()
        {
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

            Assert.IsNotNull(typeof(BattleView).GetField("unitPresenter", Fields));
            Assert.IsNotNull(typeof(BattleView).GetField("projectilePresenter", Fields));
            Assert.IsNotNull(typeof(BattleView).GetField("vfxPresenter", Fields));
            Assert.IsNull(typeof(BattleView).GetField("activeProjectileViews", Fields));
            Assert.IsNull(typeof(BattleView).GetField("pooledProjectileViews", Fields));
            Assert.IsNull(typeof(BattleView).GetField("activeAttackEffects", Fields));
            Assert.IsNull(typeof(BattleView).GetField("activeDamageEffects", Fields));
        }
    }
}
