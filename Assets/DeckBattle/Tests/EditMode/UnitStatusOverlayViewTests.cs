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

                view.Bind(1, root.transform, 10, 10, 0, 20);
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

        private static void SetPrivateField(object target, string fieldName, Object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field: " + fieldName);
            field.SetValue(target, value);
        }
    }
}
