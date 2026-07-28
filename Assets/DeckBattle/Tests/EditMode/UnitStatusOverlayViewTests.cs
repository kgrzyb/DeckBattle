using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DeckBattle.Tests
{
    public sealed class UnitStatusOverlayViewTests
    {
        [Test]
        public void SetHealthAndMana_ScalesFillRects()
        {
            GameObject root = new GameObject("Overlay", typeof(RectTransform), typeof(UnitStatusOverlayView));
            GameObject hpFillObject = new GameObject("HpFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            GameObject manaFillObject = new GameObject("ManaFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hpFillObject.transform.SetParent(root.transform, false);
            manaFillObject.transform.SetParent(root.transform, false);

            try
            {
                UnitStatusOverlayView view = root.GetComponent<UnitStatusOverlayView>();
                Image hpImage = hpFillObject.GetComponent<Image>();
                Image manaImage = manaFillObject.GetComponent<Image>();
                RectTransform hpFill = hpFillObject.GetComponent<RectTransform>();
                RectTransform manaFill = manaFillObject.GetComponent<RectTransform>();

                SetPrivateField(view, "hpFillImage", hpImage);
                SetPrivateField(view, "hpFillTransform", hpFill);
                SetPrivateField(view, "manaFillImage", manaImage);
                SetPrivateField(view, "manaFillTransform", manaFill);

                view.Bind(1, root.transform, "Swordsman", 10, 10, 0, 20);
                view.SetHealth(4, 10);
                view.SetMana(5, 20);

                Assert.AreEqual(0.4f, hpImage.fillAmount);
                Assert.AreEqual(0.4f, hpFill.localScale.x);
                Assert.AreEqual(0.25f, manaImage.fillAmount);
                Assert.AreEqual(0.25f, manaFill.localScale.x);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SetStatuses_CreatesAtMostFourPooledIconSlots()
        {
            GameObject root = new GameObject("Overlay", typeof(RectTransform), typeof(UnitStatusOverlayView));
            var statuses = new UnitStatusCollection(8);
            statuses.TryAdd(new StatusInstance { Kind = StatusKind.Stun, Stacks = 1 }, out _);
            statuses.TryAdd(new StatusInstance { Kind = StatusKind.Shield, Stacks = 1, RemainingShield = 3 }, out _);
            statuses.TryAdd(new StatusInstance { Kind = StatusKind.Burn, Stacks = 2 }, out _);
            statuses.TryAdd(new StatusInstance { Kind = StatusKind.Mark, Stacks = 1 }, out _);
            statuses.TryAdd(new StatusInstance { Kind = StatusKind.Empower, Stacks = 1 }, out _);

            try
            {
                UnitStatusOverlayView view = root.GetComponent<UnitStatusOverlayView>();
                view.SetStatuses(statuses, 3);
                view.SetStatuses(statuses, 3);

                Assert.AreEqual(5, root.GetComponentsInChildren<Image>(true).Length);
                RectTransform shieldBar = root.transform.Find("ShieldBar") as RectTransform;
                Assert.IsNotNull(shieldBar);
                Assert.AreEqual(new Vector2(0.25f, 1f), shieldBar.anchorMin);
                Assert.AreEqual(new Vector2(0.75f, 1f), shieldBar.anchorMax);
                Assert.IsTrue(shieldBar.gameObject.activeSelf);

                view.SetStatuses(statuses, 0);
                Assert.IsFalse(shieldBar.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SetPrivateField(object target, string fieldName, Object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field: " + fieldName);
            field.SetValue(target, value);
        }
    }
}
