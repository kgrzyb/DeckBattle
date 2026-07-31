using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "BattlePresentationCatalog", menuName = "Deck Battle/Battle Presentation Catalog")]
    public sealed class BattlePresentationCatalog : ScriptableObject
    {
        [SerializeField] private UnitPresentationEntry[] units = Array.Empty<UnitPresentationEntry>();
        [SerializeField] private ProjectilePresentationEntry[] projectiles = Array.Empty<ProjectilePresentationEntry>();

        [NonSerialized] private Dictionary<int, UnitView> unitPrefabsById;
        [NonSerialized] private Dictionary<int, ProjectilePresentationEntry> projectileEntriesById;

        public bool TryGetUnitPrefab(int presentationId, out UnitView prefab)
        {
            EnsureLookup();
            return unitPrefabsById.TryGetValue(presentationId, out prefab);
        }

        public bool TryGetProjectile(int presentationId, out ProjectileView prefab, out float spawnHeight, out float hitHeight)
        {
            EnsureLookup();
            if (projectileEntriesById.TryGetValue(presentationId, out ProjectilePresentationEntry entry))
            {
                prefab = entry.Prefab;
                spawnHeight = entry.SpawnHeight;
                hitHeight = entry.HitHeight;
                return prefab != null;
            }

            prefab = null;
            spawnHeight = 0f;
            hitHeight = 0f;
            return false;
        }

        private void OnEnable() { BuildLookup(); }
        private void OnValidate() { BuildLookup(); }

        private void EnsureLookup()
        {
            if (unitPrefabsById == null || projectileEntriesById == null)
            {
                BuildLookup();
            }
        }

        private void BuildLookup()
        {
            unitPrefabsById = new Dictionary<int, UnitView>(units != null ? units.Length : 0);
            projectileEntriesById = new Dictionary<int, ProjectilePresentationEntry>(projectiles != null ? projectiles.Length : 0);
            AddUnits();
            AddProjectiles();
        }

        private void AddUnits()
        {
            if (units == null) return;
            for (int i = 0; i < units.Length; i++)
            {
                UnitPresentationEntry entry = units[i];
                if (entry == null || entry.PresentationId == 0 || entry.Prefab == null) continue;
                if (!unitPrefabsById.TryAdd(entry.PresentationId, entry.Prefab))
                {
                    Debug.LogError("Duplicate unit presentation id " + entry.PresentationId + ".", this);
                }
            }
        }

        private void AddProjectiles()
        {
            if (projectiles == null) return;
            for (int i = 0; i < projectiles.Length; i++)
            {
                ProjectilePresentationEntry entry = projectiles[i];
                if (entry == null || entry.PresentationId == 0 || entry.Prefab == null) continue;
                if (!projectileEntriesById.TryAdd(entry.PresentationId, entry))
                {
                    Debug.LogError("Duplicate projectile presentation id " + entry.PresentationId + ".", this);
                }
            }
        }
    }

    [Serializable]
    public sealed class UnitPresentationEntry
    {
        public int PresentationId;
        public UnitView Prefab;
    }

    [Serializable]
    public sealed class ProjectilePresentationEntry
    {
        public int PresentationId;
        public ProjectileView Prefab;
        public float SpawnHeight;
        public float HitHeight;
    }
}
