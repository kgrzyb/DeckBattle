using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    [Category("Vfx")]
    public sealed class BattleVfxProfileTests
    {
        [Test]
        public void TryGet_ReturnsBindingForConfiguredCueWithNormalizedScale()
        {
            BattleVfxProfile profile = ScriptableObject.CreateInstance<BattleVfxProfile>();
            VfxDefinition effect = ScriptableObject.CreateInstance<VfxDefinition>();
            try
            {
                SetBindings(profile, new[]
                {
                    new BattleVfxBinding
                    {
                        Cue = BattleVfxCue.AttackFired,
                        Effect = effect,
                        Subject = VfxSpawnSubject.Source,
                        Anchor = UnitVfxAnchor.Overhead,
                        LocalScale = Vector3.zero
                    }
                });

                Assert.IsTrue(profile.TryGet(BattleVfxCue.AttackFired, out BattleVfxBinding binding));
                Assert.AreSame(effect, binding.Effect);
                Assert.AreEqual(UnitVfxAnchor.Overhead, binding.Anchor);
                Assert.AreEqual(Vector3.one, binding.ResolvedLocalScale);
                Assert.IsFalse(profile.TryGet(BattleVfxCue.Death, out _));
            }
            finally
            {
                Object.DestroyImmediate(effect);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void PresentationLookup_MapsUnitSpecialAndProjectileVfxByStableId()
        {
            GameObject unitPrefabObject = new GameObject("UnitPrefab", typeof(UnitView));
            GameObject projectilePrefabObject = new GameObject("ProjectilePrefab", typeof(ProjectileView));
            UnitDefinition unit = ScriptableObject.CreateInstance<UnitDefinition>();
            UnitSpecialDefinition special = ScriptableObject.CreateInstance<UnitSpecialDefinition>();
            ProjectileDefinition projectile = ScriptableObject.CreateInstance<ProjectileDefinition>();
            BattleVfxProfile unitProfile = ScriptableObject.CreateInstance<BattleVfxProfile>();
            BattleVfxProfile specialProfile = ScriptableObject.CreateInstance<BattleVfxProfile>();
            VfxDefinition launchVfx = ScriptableObject.CreateInstance<VfxDefinition>();
            VfxDefinition impactVfx = ScriptableObject.CreateInstance<VfxDefinition>();
            try
            {
                unit.UnitId = "vfx-unit";
                unit.UnitPrefab = unitPrefabObject.GetComponent<UnitView>();
                unit.VfxProfile = unitProfile;
                unit.Special = special;
                projectile.ProjectileId = "vfx-projectile";
                projectile.ProjectilePrefab = projectilePrefabObject.GetComponent<ProjectileView>();
                projectile.LaunchVfx = launchVfx;
                projectile.ImpactVfx = impactVfx;
                unit.Projectile = projectile;
                special.VfxProfile = specialProfile;
                SetBindings(unitProfile, new[]
                {
                    new BattleVfxBinding
                    {
                        Cue = BattleVfxCue.AttackFired,
                        Effect = launchVfx,
                        LocalScale = Vector3.one
                    }
                });
                SetBindings(specialProfile, new[]
                {
                    new BattleVfxBinding
                    {
                        Cue = BattleVfxCue.SpecialCast,
                        Effect = impactVfx,
                        LocalScale = Vector3.one
                    }
                });

                var lookup = new BattlePresentationLookup();
                lookup.Rebuild(new[] { unit }, null);

                int unitId = BattlePresentationId.ForUnit(unit);
                int projectileId = BattlePresentationId.ForProjectile(projectile);
                Assert.IsTrue(lookup.TryGetUnitVfxProfiles(unitId, out BattleVfxProfile mappedUnit, out BattleVfxProfile mappedSpecial));
                Assert.AreSame(unitProfile, mappedUnit);
                Assert.AreSame(specialProfile, mappedSpecial);
                Assert.IsTrue(lookup.TryGetProjectileVfx(projectileId, out VfxDefinition mappedLaunch, out VfxDefinition mappedImpact));
                Assert.AreSame(launchVfx, mappedLaunch);
                Assert.AreSame(impactVfx, mappedImpact);

                var definitions = new System.Collections.Generic.List<VfxDefinition>();
                lookup.CollectVfxDefinitions(definitions);
                Assert.AreEqual(4, definitions.Count);
                Assert.AreSame(launchVfx, definitions[0]);
                Assert.AreSame(impactVfx, definitions[1]);
                Assert.AreSame(launchVfx, definitions[2]);
                Assert.AreSame(impactVfx, definitions[3]);
            }
            finally
            {
                Object.DestroyImmediate(impactVfx);
                Object.DestroyImmediate(launchVfx);
                Object.DestroyImmediate(specialProfile);
                Object.DestroyImmediate(unitProfile);
                Object.DestroyImmediate(projectile);
                Object.DestroyImmediate(special);
                Object.DestroyImmediate(unit);
                Object.DestroyImmediate(projectilePrefabObject);
                Object.DestroyImmediate(unitPrefabObject);
            }
        }

        [Test]
        public void UnitView_ResolvesConfiguredAnchorAndFallsBackToRoot()
        {
            GameObject unitObject = new GameObject("Unit");
            unitObject.SetActive(false);
            GameObject groundObject = new GameObject("Ground");
            groundObject.transform.SetParent(unitObject.transform, false);
            GameObject bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(unitObject.transform, false);
            GameObject overheadObject = new GameObject("Overhead");
            overheadObject.transform.SetParent(unitObject.transform, false);
            UnitVfxAnchors anchors = unitObject.AddComponent<UnitVfxAnchors>();
            SetPrivateField(anchors, "ground", groundObject.transform);
            SetPrivateField(anchors, "body", bodyObject.transform);
            SetPrivateField(anchors, "overhead", overheadObject.transform);
            UnitView unitView = unitObject.AddComponent<UnitView>();
            try
            {
                unitObject.SetActive(true);

                Assert.AreSame(groundObject.transform, unitView.ResolveVfxAnchor(UnitVfxAnchor.Ground));
                Assert.AreSame(bodyObject.transform, unitView.ResolveVfxAnchor(UnitVfxAnchor.Body));
                Assert.AreSame(overheadObject.transform, unitView.ResolveVfxAnchor(UnitVfxAnchor.Overhead));
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
            }
        }

        private static void SetBindings(BattleVfxProfile profile, BattleVfxBinding[] bindings)
        {
            SetPrivateField(profile, "bindings", bindings);
            typeof(BattleVfxProfile)
                .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(profile, null);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }
    }
}
