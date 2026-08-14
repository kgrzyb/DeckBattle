using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
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
                GameObject hpDamageFillObject = new GameObject("HpDamageFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                hpDamageFillObject.transform.SetParent(root.transform, false);
                Image hpDamageImage = hpDamageFillObject.GetComponent<Image>();
                RectTransform hpDamageFill = hpDamageFillObject.GetComponent<RectTransform>();

                SetPrivateField(view, "hpFillImage", hpImage);
                SetPrivateField(view, "hpFillTransform", hpFill);
                SetPrivateField(view, "hpDamageFillImage", hpDamageImage);
                SetPrivateField(view, "hpDamageFillTransform", hpDamageFill);
                SetPrivateField(view, "manaFillImage", manaImage);
                SetPrivateField(view, "manaFillTransform", manaFill);

                view.Bind(1, root.transform, "Swordsman", 10, 10, 0, 20);
                view.SetHealth(4, 10);
                view.SetMana(5, 20);

                Assert.AreEqual(0.4f, hpImage.fillAmount);
                Assert.AreEqual(0.4f, hpFill.localScale.x);
                Assert.AreEqual(1f, hpDamageImage.fillAmount);
                Assert.AreEqual(1f, hpDamageFill.localScale.x);
                Assert.AreEqual(0.25f, manaImage.fillAmount);
                Assert.AreEqual(0.25f, manaFill.localScale.x);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SetHealth_NonLethalDamage_DelaysAndAnimatesDamageFill()
        {
            GameObject root = new GameObject("Overlay", typeof(RectTransform), typeof(UnitStatusOverlayView));
            GameObject hpFillObject = new GameObject("HpFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            GameObject hpDamageFillObject = new GameObject("HpDamageFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hpFillObject.transform.SetParent(root.transform, false);
            hpDamageFillObject.transform.SetParent(root.transform, false);

            try
            {
                UnitStatusOverlayView view = root.GetComponent<UnitStatusOverlayView>();
                Image hpImage = hpFillObject.GetComponent<Image>();
                Image hpDamageImage = hpDamageFillObject.GetComponent<Image>();
                SetPrivateField(view, "hpFillImage", hpImage);
                SetPrivateField(view, "hpFillTransform", hpFillObject.GetComponent<RectTransform>());
                SetPrivateField(view, "hpDamageFillImage", hpDamageImage);
                SetPrivateField(view, "hpDamageFillTransform", hpDamageFillObject.GetComponent<RectTransform>());
                SetPrivateValue(view, "damageFillDelay", 0.1f);
                SetPrivateValue(view, "damageFillDuration", 0.2f);

                view.Bind(1, root.transform, "Swordsman", 10, 10, 0, 20);
                view.SetHealth(4, 10);

                Assert.AreEqual(0.4f, hpImage.fillAmount);
                Assert.AreEqual(1f, hpDamageImage.fillAmount);

                view.TickDamageFill(0.1f);
                Assert.AreEqual(1f, hpDamageImage.fillAmount);

                view.TickDamageFill(0.1f);
                Assert.AreEqual(0.7f, hpDamageImage.fillAmount, 0.001f);

                view.TickDamageFill(0.1f);
                Assert.AreEqual(0.4f, hpDamageImage.fillAmount, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SetHpFillColor_UpdatesHealthBarFillOnly()
        {
            GameObject root = new GameObject("Overlay", typeof(RectTransform), typeof(UnitStatusOverlayView));
            GameObject hpFillObject = new GameObject("HpFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hpFillObject.transform.SetParent(root.transform, false);

            try
            {
                UnitStatusOverlayView view = root.GetComponent<UnitStatusOverlayView>();
                Image hpImage = hpFillObject.GetComponent<Image>();
                SetPrivateField(view, "hpFillImage", hpImage);

                Color enemyColor = new Color(0.88f, 0.22f, 0.24f, 0.96f);
                view.SetHpFillColor(enemyColor);

                Assert.AreEqual(enemyColor, hpImage.color);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SetHealth_HealingLethalDamageAndRebind_AnimatesDamageFill()
        {
            GameObject root = new GameObject("Overlay", typeof(RectTransform), typeof(UnitStatusOverlayView));
            GameObject hpFillObject = new GameObject("HpFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            GameObject hpDamageFillObject = new GameObject("HpDamageFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hpFillObject.transform.SetParent(root.transform, false);
            hpDamageFillObject.transform.SetParent(root.transform, false);

            try
            {
                UnitStatusOverlayView view = root.GetComponent<UnitStatusOverlayView>();
                Image hpDamageImage = hpDamageFillObject.GetComponent<Image>();
                SetPrivateField(view, "hpFillImage", hpFillObject.GetComponent<Image>());
                SetPrivateField(view, "hpFillTransform", hpFillObject.GetComponent<RectTransform>());
                SetPrivateField(view, "hpDamageFillImage", hpDamageImage);
                SetPrivateField(view, "hpDamageFillTransform", hpDamageFillObject.GetComponent<RectTransform>());

                view.Bind(1, root.transform, "Swordsman", 10, 10, 0, 20);
                view.SetHealth(6, 10);
                view.SetHealth(8, 10);
                Assert.AreEqual(0.8f, hpDamageImage.fillAmount);

                SetPrivateValue(view, "damageFillDelay", 0f);
                SetPrivateValue(view, "damageFillDuration", 0.2f);
                view.SetHealth(0, 10);
                Assert.AreEqual(0.8f, hpDamageImage.fillAmount);
                Assert.IsTrue(root.activeSelf);

                view.TickDamageFill(0.2f);
                Assert.AreEqual(0f, hpDamageImage.fillAmount);

                view.Bind(2, root.transform, "Guard", 10, 10, 0, 20);
                Assert.AreEqual(1f, hpDamageImage.fillAmount);
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
            GameObject statusIconPrefab = CreateStatusIconPrefab();
            var statuses = new UnitStatusCollection(8);
            statuses.TryAdd(new StatusInstance { Kind = StatusKind.Stun, Stacks = 1 }, out _);
            statuses.TryAdd(new StatusInstance { Kind = StatusKind.Shield, Stacks = 1, RemainingShield = 3 }, out _);
            statuses.TryAdd(new StatusInstance { Kind = StatusKind.Burn, Stacks = 2 }, out _);
            statuses.TryAdd(new StatusInstance { Kind = StatusKind.Mark, Stacks = 1 }, out _);
            statuses.TryAdd(new StatusInstance { Kind = StatusKind.Empower, Stacks = 1 }, out _);

            try
            {
                UnitStatusOverlayView view = root.GetComponent<UnitStatusOverlayView>();
                SetPrivateField(view, "statusIconPrefab", statusIconPrefab);
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
                Object.DestroyImmediate(statusIconPrefab);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SetPresentationStatuses_UsesPresentationShadowWithoutRuntimeState()
        {
            GameObject root = new GameObject("Overlay", typeof(RectTransform), typeof(UnitStatusOverlayView));
            GameObject statusIconPrefab = CreateStatusIconPrefab();
            var statuses = new List<StatusPresentationState>
            {
                new StatusPresentationState(StatusKind.Stun, 1, 1),
                new StatusPresentationState(StatusKind.Burn, 2, 2)
            };

            try
            {
                UnitStatusOverlayView view = root.GetComponent<UnitStatusOverlayView>();
                SetPrivateField(view, "statusIconPrefab", statusIconPrefab);
                view.SetPresentationStatuses(statuses, 4, null);

                Assert.AreEqual(5, root.GetComponentsInChildren<Image>(true).Length);
                RectTransform shieldBar = root.transform.Find("ShieldBar") as RectTransform;
                Assert.IsNotNull(shieldBar);
                Assert.IsTrue(shieldBar.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(statusIconPrefab);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SetPresentationStatuses_CombinedPresentationEntryShowsConfiguredIcon()
        {
            GameObject root = new GameObject("Overlay", typeof(RectTransform), typeof(UnitStatusOverlayView));
            GameObject statusIconPrefab = CreateStatusIconPrefab();
            StatusPresentationCatalog catalog = ScriptableObject.CreateInstance<StatusPresentationCatalog>();
            Texture2D texture = new Texture2D(1, 1);
            Sprite icon = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));

            try
            {
                SetCatalogEntries(catalog, new[]
                {
                    new StatusPresentationEntry
                    {
                        Kind = StatusKind.Empower,
                        Mode = StatusPresentationMode.IconAndVfx,
                        Icon = icon
                    }
                });

                UnitStatusOverlayView view = root.GetComponent<UnitStatusOverlayView>();
                SetPrivateField(view, "statusIconPrefab", statusIconPrefab);
                view.SetPresentationStatuses(
                    new List<StatusPresentationState> { new StatusPresentationState(StatusKind.Empower, 1, 1) },
                    0,
                    catalog);

                Image[] images = root.GetComponentsInChildren<Image>(true);
                bool hasConfiguredIcon = false;
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i].gameObject.activeSelf && images[i].sprite == icon)
                    {
                        hasConfiguredIcon = true;
                        break;
                    }
                }

                Assert.IsTrue(hasConfiguredIcon);
            }
            finally
            {
                Object.DestroyImmediate(icon);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(statusIconPrefab);
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateStatusIconPrefab()
        {
            GameObject icon = new GameObject("StatusIconPrefab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            GameObject label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            label.transform.SetParent(icon.transform, false);
            return icon;
        }

        private static void SetCatalogEntries(StatusPresentationCatalog catalog, StatusPresentationEntry[] entries)
        {
            FieldInfo field = typeof(StatusPresentationCatalog).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(catalog, entries);
            typeof(StatusPresentationCatalog).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(catalog, null);
        }

        private static void SetPrivateField(object target, string fieldName, Object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field: " + fieldName);
            field.SetValue(target, value);
        }

        private static void SetPrivateValue(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field: " + fieldName);
            field.SetValue(target, value);
        }

    }
}
