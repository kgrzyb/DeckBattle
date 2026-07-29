using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class StatusPresentationCatalogTests
    {
        [Test]
        public void TryGet_ReturnsEntryForConfiguredKind()
        {
            StatusPresentationCatalog catalog = ScriptableObject.CreateInstance<StatusPresentationCatalog>();
            try
            {
                SetEntries(catalog, new[]
                {
                    new StatusPresentationEntry { Kind = StatusKind.Stun, Mode = StatusPresentationMode.Icon },
                    new StatusPresentationEntry { Kind = StatusKind.Burn, Mode = StatusPresentationMode.Vfx }
                });

                Assert.IsTrue(catalog.TryGet(StatusKind.Stun, out StatusPresentationEntry stun));
                Assert.AreEqual(StatusPresentationMode.Icon, stun.Mode);
                Assert.IsTrue(catalog.TryGet(StatusKind.Burn, out StatusPresentationEntry burn));
                Assert.AreEqual(StatusPresentationMode.Vfx, burn.Mode);
                Assert.IsFalse(catalog.TryGet(StatusKind.Shield, out _));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        private static void SetEntries(StatusPresentationCatalog catalog, StatusPresentationEntry[] entries)
        {
            FieldInfo field = typeof(StatusPresentationCatalog).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(catalog, entries);
            typeof(StatusPresentationCatalog).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(catalog, null);
        }
    }
}
