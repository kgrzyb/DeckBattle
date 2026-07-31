using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class UnitStatusVfxControllerTests
    {
        [Test]
        public void HandleStatusEvent_AppliedStatusCreatesConfiguredActiveVfx()
        {
            GameObject controllerObject = new GameObject("StatusVfxController", typeof(UnitStatusVfxController));
            GameObject unitObject = new GameObject("Unit", typeof(UnitView));
            GameObject prefabObject = new GameObject("ActiveVfx", typeof(StatusVfxView));
            StatusPresentationCatalog catalog = ScriptableObject.CreateInstance<StatusPresentationCatalog>();
            try
            {
                StatusVfxView activeVfxPrefab = prefabObject.GetComponent<StatusVfxView>();
                SetEntries(catalog, new[]
                {
                    new StatusPresentationEntry
                    {
                        Kind = StatusKind.Haste,
                        Mode = StatusPresentationMode.Vfx,
                        ActiveVfxPrefab = activeVfxPrefab
                    }
                });

                UnitStatusVfxController controller = controllerObject.GetComponent<UnitStatusVfxController>();
                controller.Initialize(catalog);
                controller.BindPresentationUnit(1, unitObject.GetComponent<UnitView>());

                controller.HandleStatusEvent(BattleEvent.StatusApplied(1, 2, StatusKind.Haste, 1, 3f));

                Assert.AreEqual(1, unitObject.transform.childCount);
                Assert.IsNotNull(unitObject.GetComponentInChildren<StatusVfxView>());
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(prefabObject);
                Object.DestroyImmediate(unitObject);
                Object.DestroyImmediate(controllerObject);
            }
        }

        private static void SetEntries(StatusPresentationCatalog catalog, StatusPresentationEntry[] entries)
        {
            typeof(StatusPresentationCatalog)
                .GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(catalog, entries);
            typeof(StatusPresentationCatalog)
                .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(catalog, null);
        }
    }
}
