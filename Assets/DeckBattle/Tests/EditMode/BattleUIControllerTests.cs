using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class PreparationAnnouncementTests
    {
        [Test]
        public void PlayerPreparationMessage_IsStable()
        {
            Assert.AreEqual("Prepare", RoundAnnouncementView.PlayerPreparationMessage);
        }

        [Test]
        public void EnemyPreparationMessage_IsStable()
        {
            Assert.AreEqual("Opponent is preparing...", RoundAnnouncementView.EnemyPreparationMessage);
        }
    }
}
