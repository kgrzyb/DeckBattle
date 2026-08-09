using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    // Presentation-only bridge built once from the cards available in a battle.
    // Simulation continues to communicate through stable integer presentation IDs.
    public sealed class BattlePresentationLookup
    {
        private readonly Dictionary<int, UnitView> unitPrefabsById = new Dictionary<int, UnitView>(16);
        private readonly Dictionary<int, ProjectilePresentationData> projectileDataById = new Dictionary<int, ProjectilePresentationData>(8);
        private readonly HashSet<int> ambiguousUnitIds = new HashSet<int>();
        private readonly HashSet<int> ambiguousProjectileIds = new HashSet<int>();

        public void Rebuild(IReadOnlyList<UnitDefinition> definitions, Object context)
        {
            unitPrefabsById.Clear();
            projectileDataById.Clear();
            ambiguousUnitIds.Clear();
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
            return unitPrefabsById.TryGetValue(presentationId, out prefab);
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

        private void AddDefinition(UnitDefinition definition, Object context)
        {
            if (definition == null)
            {
                return;
            }

            AddUnitPrefab(BattlePresentationId.ForUnit(definition), definition.UnitPrefab, definition, context);
            AddProjectile(definition.Projectile, context);
        }

        private void AddUnitPrefab(int presentationId, UnitView prefab, UnitDefinition definition, Object context)
        {
            if (presentationId == 0 || prefab == null || ambiguousUnitIds.Contains(presentationId))
            {
                return;
            }

            if (!unitPrefabsById.TryGetValue(presentationId, out UnitView existing))
            {
                unitPrefabsById.Add(presentationId, prefab);
                return;
            }

            if (existing == prefab)
            {
                return;
            }

            unitPrefabsById.Remove(presentationId);
            ambiguousUnitIds.Add(presentationId);
            Debug.LogError(
                "Unit presentation id " + presentationId + " maps to multiple prefabs. Check UnitId values, including " + definition.name + ".",
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
                definition.HitHeight);
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
                "Projectile presentation id " + presentationId + " maps to multiple prefabs or launch heights. Check ProjectileId values, including " + definition.name + ".",
                context);
        }

        private readonly struct ProjectilePresentationData
        {
            public readonly ProjectileView Prefab;
            public readonly float SpawnHeight;
            public readonly float HitHeight;

            public ProjectilePresentationData(ProjectileView prefab, float spawnHeight, float hitHeight)
            {
                Prefab = prefab;
                SpawnHeight = spawnHeight;
                HitHeight = hitHeight;
            }

            public bool Matches(ProjectilePresentationData other)
            {
                return Prefab == other.Prefab
                    && Mathf.Approximately(SpawnHeight, other.SpawnHeight)
                    && Mathf.Approximately(HitHeight, other.HitHeight);
            }
        }
    }
}
