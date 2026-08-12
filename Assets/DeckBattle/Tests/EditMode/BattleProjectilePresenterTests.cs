using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class BattleProjectilePresenterTests
    {
        [Test]
        public void ResolveLaunchPosition_UsesConfiguredProjectileLaunchAnchor()
        {
            GameObject unitObject = new GameObject("Unit", typeof(UnitView), typeof(UnitVfxAnchors));
            GameObject launchObject = new GameObject("ProjectileLaunch");
            try
            {
                launchObject.transform.SetParent(unitObject.transform, false);
                launchObject.transform.position = new Vector3(2f, 3f, 4f);
                UnitVfxAnchors anchors = unitObject.GetComponent<UnitVfxAnchors>();
                SetPrivateField(anchors, "projectileLaunch", launchObject.transform);

                Vector3 resolved = BattleProjectilePresenter.ResolveLaunchPosition(
                    unitObject.GetComponent<UnitView>(),
                    new Vector3(10f, 11f, 12f));

                Assert.AreEqual(launchObject.transform.position, resolved);
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
            }
        }

        [Test]
        public void ResolveLaunchPosition_UsesFallbackWhenProjectileLaunchAnchorIsMissing()
        {
            GameObject unitObject = new GameObject("Unit", typeof(UnitView), typeof(UnitVfxAnchors));
            Vector3 fallback = new Vector3(10f, 11f, 12f);
            try
            {
                Vector3 resolved = BattleProjectilePresenter.ResolveLaunchPosition(
                    unitObject.GetComponent<UnitView>(),
                    fallback);

                Assert.AreEqual(fallback, resolved);
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }
    }
}
