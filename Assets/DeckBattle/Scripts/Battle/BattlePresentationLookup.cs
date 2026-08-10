using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    // Presentation-only bridge built once from the cards available in a battle.
    // Simulation continues to communicate through stable integer presentation IDs.
    public sealed class BattlePresentationLookup
    {
        private readonly Dictionary<int, UnitViewPresentationData> unitViewDataById = new Dictionary<int, UnitViewPresentationData>(16);
        private readonly Dictionary<int, UnitVfxPresentationData> unitVfxDataById = new Dictionary<int, UnitVfxPresentationData>(16);
        private readonly Dictionary<int, ProjectilePresentationData> projectileDataById = new Dictionary<int, ProjectilePresentationData>(8);
        private readonly HashSet<int> ambiguousUnitIds = new HashSet<int>();
        private readonly HashSet<int> ambiguousUnitVfxIds = new HashSet<int>();
        private readonly HashSet<int> ambiguousProjectileIds = new HashSet<int>();

        public void Rebuild(IReadOnlyList<UnitDefinition> definitions, Object context)
        {
            unitViewDataById.Clear();
            unitVfxDataById.Clear();
            projectileDataById.Clear();
            ambiguousUnitIds.Clear();
            ambiguousUnitVfxIds.Clear();
            ambiguousProjectileIds.Clear();

            if (definitions == null)
            {
                return;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                AddDefinition(definitions[i], context);
            }
        }

        public bool TryGetUnitPrefab(int presentationId, out UnitView prefab)
        {
            if (unitViewDataById.TryGetValue(presentationId, out UnitViewPresentationData data))
            {
                prefab = data.Prefab;
                return prefab != null;
            }

            prefab = null;
            return false;
        }

        public bool TryGetUnitViewData(int presentationId, out UnitView prefab, out float runAnimationSpeedMultiplier)
        {
            if (unitViewDataById.TryGetValue(presentationId, out UnitViewPresentationData data))
            {
                prefab = data.Prefab;
                runAnimationSpeedMultiplier = data.RunAnimationSpeedMultiplier;
                return prefab != null;
            }

            prefab = null;
            runAnimationSpeedMultiplier = 1f;
            return false;
        }

        public bool TryGetUnitVfxProfiles(
            int presentationId,
            out BattleVfxProfile unitProfile,
            out BattleVfxProfile specialProfile)
        {
            if (unitVfxDataById.TryGetValue(presentationId, out UnitVfxPresentationData data))
            {
                unitProfile = data.UnitProfile;
                specialProfile = data.SpecialProfile;
                return unitProfile != null || specialProfile != null;
            }

            unitProfile = null;
            specialProfile = null;
            return false;
        }

        public bool TryGetProjectile(int presentationId, out ProjectileView prefab, out float spawnHeight, out float hitHeight)
        {
            if (projectileDataById.TryGetValue(presentationId, out ProjectilePresentationData data))
            {
                prefab = data.Prefab;
                spawnHeight = data.SpawnHeight;
                hitHeight = data.HitHeight;
                return prefab != null;
            }

            prefab = null;
            spawnHeight = 0f;
            hitHeight = 0f;
            return false;
        }

        public bool TryGetProjectileVfx(int presentationId, out VfxDefinition launchVfx, out VfxDefinition impactVfx)
        {
            if (projectileDataById.TryGetValue(presentationId, out ProjectilePresentationData data))
            {
                launchVfx = data.LaunchVfx;
                impactVfx = data.ImpactVfx;
                return launchVfx != null || impactVfx != null;
            }

            launchVfx = null;
            impactVfx = null;
            return false;
        }

        // Called at battle setup, never from the simulation or frame hot path.
        public void CollectVfxDefinitions(List<VfxDefinition> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            definitions.Clear();
            foreach (KeyValuePair<int, UnitVfxPresentationData> pair in unitVfxDataById)
            {
                AddProfileDefinitions(pair.Value.UnitProfile, definitions);
                AddProfileDefinitions(pair.Value.SpecialProfile, definitions);
            }

            foreach (KeyValuePair<int, ProjectilePresentationData> pair in projectileDataById)
            {
                ProjectilePresentationData data = pair.Value;
                if (data.LaunchVfx != null)
                {
                    definitions.Add(data.LaunchVfx);
                }

                if (data.ImpactVfx != null)
                {
                    definitions.Add(data.ImpactVfx);
                }
            }
        }

        private void AddDefinition(UnitDefinition definition, Object context)
        {
            if (definition == null)
            {
                return;
            }

            int unitPresentationId = BattlePresentationId.ForUnit(definition);
            AddUnitViewData(unitPresentationId, definition.UnitPrefab, definition.RunAnimationSpeedMultiplier, definition, context);
            AddUnitVfxProfiles(unitPresentationId, definition.VfxProfile, definition.Special != null ? definition.Special.VfxProfile : null, definition, context);
            AddProjectile(definition.Projectile, context);
        }

        private static void AddProfileDefinitions(BattleVfxProfile profile, List<VfxDefinition> definitions)
        {
            if (profile == null)
            {
                return;
            }

            BattleVfxBinding[] bindings = profile.Bindings;
            for (int i = 0; i < bindings.Length; i++)
            {
                VfxDefinition effect = bindings[i].Effect;
                if (effect != null)
                {
                    definitions.Add(effect);
                }
            }
        }

        private void AddUnitViewData(
            int presentationId,
            UnitView prefab,
            float runAnimationSpeedMultiplier,
            UnitDefinition definition,
            Object context)
        {
            if (presentationId == 0 || prefab == null || ambiguousUnitIds.Contains(presentationId))
            {
                return;
            }

            var data = new UnitViewPresentationData(prefab, runAnimationSpeedMultiplier);
            if (!unitViewDataById.TryGetValue(presentationId, out UnitViewPresentationData existing))
            {
                unitViewDataById.Add(presentationId, data);
                return;
            }

            if (existing.Matches(data))
            {
                return;
            }

            unitViewDataById.Remove(presentationId);
            ambiguousUnitIds.Add(presentationId);
            Debug.LogError(
                "Unit presentation id " + presentationId + " maps to multiple prefabs or run animation speed multipliers. Check UnitId values, including " + definition.name + ".",
                context);
        }

        private void AddUnitVfxProfiles(
            int presentationId,
            BattleVfxProfile unitProfile,
            BattleVfxProfile specialProfile,
            UnitDefinition definition,
            Object context)
        {
            if (presentationId == 0
                || (unitProfile == null && specialProfile == null)
                || ambiguousUnitVfxIds.Contains(presentationId))
            {
                return;
            }

            var data = new UnitVfxPresentationData(unitProfile, specialProfile);
            if (!unitVfxDataById.TryGetValue(presentationId, out UnitVfxPresentationData existing))
            {
                unitVfxDataById.Add(presentationId, data);
                return;
            }

            if (existing.Matches(data))
            {
                return;
            }

            unitVfxDataById.Remove(presentationId);
            ambiguousUnitVfxIds.Add(presentationId);
            Debug.LogError(
                "Unit presentation id " + presentationId + " maps to multiple VFX profiles. Check UnitId values, including " + definition.name + ".",
                context);
        }

        private void AddProjectile(ProjectileDefinition definition, Object context)
        {
            if (definition == null || definition.ProjectilePrefab == null)
            {
                return;
            }

            int presentationId = BattlePresentationId.ForProjectile(definition);
            if (presentationId == 0 || ambiguousProjectileIds.Contains(presentationId))
            {
                return;
            }

            var data = new ProjectilePresentationData(
                definition.ProjectilePrefab,
                definition.SpawnHeight,
                definition.HitHeight,
                definition.LaunchVfx,
                definition.ImpactVfx);
            if (!projectileDataById.TryGetValue(presentationId, out ProjectilePresentationData existing))
            {
                projectileDataById.Add(presentationId, data);
                return;
            }

            if (existing.Matches(data))
            {
                return;
            }

            projectileDataById.Remove(presentationId);
            ambiguousProjectileIds.Add(presentationId);
            Debug.LogError(
                "Projectile presentation id " + presentationId + " maps to multiple prefabs, launch settings, or VFX. Check ProjectileId values, including " + definition.name + ".",
                context);
        }

        private readonly struct UnitVfxPresentationData
        {
            public readonly BattleVfxProfile UnitProfile;
            public readonly BattleVfxProfile SpecialProfile;

            public UnitVfxPresentationData(BattleVfxProfile unitProfile, BattleVfxProfile specialProfile)
            {
                UnitProfile = unitProfile;
                SpecialProfile = specialProfile;
            }

            public bool Matches(UnitVfxPresentationData other)
            {
                return UnitProfile == other.UnitProfile && SpecialProfile == other.SpecialProfile;
            }
        }

        private readonly struct UnitViewPresentationData
        {
            public readonly UnitView Prefab;
            public readonly float RunAnimationSpeedMultiplier;

            public UnitViewPresentationData(UnitView prefab, float runAnimationSpeedMultiplier)
            {
                Prefab = prefab;
                RunAnimationSpeedMultiplier = UnitView.ResolveRunAnimationSpeedMultiplier(runAnimationSpeedMultiplier);
            }

            public bool Matches(UnitViewPresentationData other)
            {
                return Prefab == other.Prefab
                    && Mathf.Approximately(RunAnimationSpeedMultiplier, other.RunAnimationSpeedMultiplier);
            }
        }

        private readonly struct ProjectilePresentationData
        {
            public readonly ProjectileView Prefab;
            public readonly float SpawnHeight;
            public readonly float HitHeight;
            public readonly VfxDefinition LaunchVfx;
            public readonly VfxDefinition ImpactVfx;

            public ProjectilePresentationData(
                ProjectileView prefab,
                float spawnHeight,
                float hitHeight,
                VfxDefinition launchVfx,
                VfxDefinition impactVfx)
            {
                Prefab = prefab;
                SpawnHeight = spawnHeight;
                HitHeight = hitHeight;
                LaunchVfx = launchVfx;
                ImpactVfx = impactVfx;
            }

            public bool Matches(ProjectilePresentationData other)
            {
                return Prefab == other.Prefab
                    && Mathf.Approximately(SpawnHeight, other.SpawnHeight)
                    && Mathf.Approximately(HitHeight, other.HitHeight)
                    && LaunchVfx == other.LaunchVfx
                    && ImpactVfx == other.ImpactVfx;
            }
        }
    }
}
