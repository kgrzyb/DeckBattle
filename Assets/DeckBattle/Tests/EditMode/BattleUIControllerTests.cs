using System.Reflection;
using NUnit.Framework;
using UnityEngine;

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

    public sealed class BattleUIControllerTimerTests
    {
        [Test]
        public void RoundTimerProgress_DecreasesProgressBarScaleOnTheXaxis()
        {
            GameObject uiObject = new GameObject("BattleUI", typeof(BattleUIController));
            GameObject timerObject = new GameObject("RoundTimer");
            GameObject progressObject = new GameObject("ProgressBar", typeof(RectTransform));
            try
            {
                BattleUIController controller = uiObject.GetComponent<BattleUIController>();
                RectTransform progressBar = progressObject.GetComponent<RectTransform>();
                SetPrivateField(controller, "roundTimer", timerObject);
                SetPrivateField(controller, "roundTimerProgressBar", progressBar);
                InvokePrivateMethod(controller, "Awake");

                InvokePrivateMethod(controller, "SetRoundTimerProgress", 0.25f);

                Assert.AreEqual(0.25f, progressBar.localScale.x);
                Assert.AreEqual(1f, progressBar.localScale.y);
                Assert.IsFalse(timerObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(progressObject);
                Object.DestroyImmediate(timerObject);
                Object.DestroyImmediate(uiObject);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName, params object[] arguments)
        {
            target.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, arguments);
        }
    }
}
