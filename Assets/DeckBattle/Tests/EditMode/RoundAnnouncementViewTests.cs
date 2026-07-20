using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class RoundAnnouncementViewTests
    {
        [Test]
        public void FormatRoundResult_WhenPlayerDealsMoreDamage_ShowsRoundWon()
        {
            var result = new RoundResolutionResult(5, 2, 20, 15, false, false, BattleSide.Player);

            Assert.AreEqual("Round Won", RoundAnnouncementView.FormatRoundResult(result));
        }

        [Test]
        public void FormatRoundResult_WhenEnemyDealsMoreDamage_ShowsRoundLost()
        {
            var result = new RoundResolutionResult(1, 4, 16, 19, false, false, BattleSide.Player);

            Assert.AreEqual("Round Lost", RoundAnnouncementView.FormatRoundResult(result));
        }

        [Test]
        public void FormatRoundResult_WhenDamageIsEqual_ShowsDrawWithoutDamage()
        {
            var result = new RoundResolutionResult(3, 3, 17, 17, false, false, BattleSide.Player);

            Assert.AreEqual("Draw", RoundAnnouncementView.FormatRoundResult(result));
        }
    }
}
