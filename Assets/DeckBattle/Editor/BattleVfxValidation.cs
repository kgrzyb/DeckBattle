#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeckBattle.Editor
{
    internal static class BattleVfxValidation
    {
        [MenuItem("Deck Battle/Validation/Validate Battle VFX")]
        private static void ValidateAll()
        {
            int errors = ValidateDefinitions();
            errors += ValidateProfiles();
            errors += ValidateStatusCatalogs();
            if (errors == 0)
            {
                Debug.Log("Battle VFX validation completed without errors.");
                return;
            }

            Debug.LogError("Battle VFX validation found " + errors + " error(s). See the entries above.");
        }

        private static int ValidateDefinitions()
        {
            int errors = 0;
            string[] guids = AssetDatabase.FindAssets("t:VfxDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                VfxDefinition definition = AssetDatabase.LoadAssetAtPath<VfxDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (definition == null)
                {
                    continue;
                }

                if (definition.Prefab == null)
                {
                    LogError("VFX definition is missing its pooled prefab.", definition);
                    errors++;
                    continue;
                }

                if (definition.PrewarmCount > definition.MaxActiveCount)
                {
                    LogError("PrewarmCount cannot exceed MaxActiveCount.", definition);
                    errors++;
                }

                if (definition.MaxRetainedCount > definition.MaxActiveCount)
                {
                    LogError("MaxRetainedCount cannot exceed MaxActiveCount.", definition);
                    errors++;
                }

                if (definition.LifetimeMode != VfxLifetimeMode.ParticleSystemAlive)
                {
                    continue;
                }

                ParticleSystem[] particles = definition.Prefab.GetComponentsInChildren<ParticleSystem>(true);
                if (particles.Length == 0)
                {
                    LogError("ParticleSystemAlive requires at least one ParticleSystem on the prefab.", definition);
                    errors++;
                    continue;
                }

                for (int particleIndex = 0; particleIndex < particles.Length; particleIndex++)
                {
                    ParticleSystem particleSystem = particles[particleIndex];
                    if (particleSystem != null && particleSystem.main.loop)
                    {
                        LogError("ParticleSystemAlive cannot be used with a looping ParticleSystem.", definition);
                        errors++;
                        break;
                    }
                }
            }

            return errors;
        }

        private static int ValidateProfiles()
        {
            int errors = 0;
            string[] guids = AssetDatabase.FindAssets("t:BattleVfxProfile");
            for (int i = 0; i < guids.Length; i++)
            {
                BattleVfxProfile profile = AssetDatabase.LoadAssetAtPath<BattleVfxProfile>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (profile == null)
                {
                    continue;
                }

                var configuredCues = new HashSet<BattleVfxCue>();
                BattleVfxBinding[] bindings = profile.Bindings;
                for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                {
                    BattleVfxBinding binding = bindings[bindingIndex];
                    if (binding.Cue == BattleVfxCue.None)
                    {
                        LogError("A VFX profile contains a binding with cue None.", profile);
                        errors++;
                        continue;
                    }

                    if (!configuredCues.Add(binding.Cue))
                    {
                        LogError("A VFX profile contains a duplicate cue: " + binding.Cue + ".", profile);
                        errors++;
                    }

                    if (binding.Effect == null || binding.Effect.Prefab == null)
                    {
                        LogError("A VFX profile binding is missing a usable effect prefab.", profile);
                        errors++;
                        continue;
                    }

                    if ((binding.Subject == VfxSpawnSubject.Source || binding.Subject == VfxSpawnSubject.Target)
                        && !IsUnitAnchor(binding.Anchor))
                    {
                        LogError("A unit-attached VFX binding has an invalid anchor.", profile);
                        errors++;
                    }

                    if (binding.Effect.LifetimeMode == VfxLifetimeMode.Manual
                        && binding.Cue != BattleVfxCue.AttackWindup
                        && binding.Cue != BattleVfxCue.SpecialCast)
                    {
                        LogError("Manual VFX are currently supported only for AttackWindup and SpecialCast cues.", profile);
                        errors++;
                    }
                }
            }

            return errors;
        }

        private static int ValidateStatusCatalogs()
        {
            int errors = 0;
            string[] guids = AssetDatabase.FindAssets("t:StatusPresentationCatalog");
            for (int i = 0; i < guids.Length; i++)
            {
                StatusPresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<StatusPresentationCatalog>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (catalog == null)
                {
                    continue;
                }

                StatusPresentationEntry[] entries = catalog.Entries;
                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    StatusPresentationEntry entry = entries[entryIndex];
                    if (entry == null || entry.Mode != StatusPresentationMode.Vfx)
                    {
                        continue;
                    }

                    errors += ValidateStatusDefinition(entry.ApplyVfxDefinition, entry.ApplyAnchor, "Apply Vfx Definition", false, catalog);
                    errors += ValidateStatusDefinition(entry.ActiveVfxDefinition, entry.ActiveAnchor, "Active Vfx Definition", true, catalog);
                    errors += ValidateStatusDefinition(entry.RemoveVfxDefinition, entry.RemoveAnchor, "Remove Vfx Definition", false, catalog);
                }
            }

            return errors + ValidateUnitAnchors();
        }

        private static int ValidateStatusDefinition(
            VfxDefinition definition,
            UnitVfxAnchor anchor,
            string fieldName,
            bool requiresManualLifetime,
            Object context)
        {
            if (definition == null)
            {
                return 0;
            }

            if (!IsUnitAnchor(anchor))
            {
                LogError(fieldName + " has an invalid unit VFX anchor.", context);
                return 1;
            }

            if (definition.Prefab == null)
            {
                LogError(fieldName + " is missing a usable effect prefab.", context);
                return 1;
            }

            bool isManual = definition.LifetimeMode == VfxLifetimeMode.Manual;
            if (requiresManualLifetime == isManual)
            {
                return 0;
            }

            string requiredLifetime = requiresManualLifetime ? "Manual" : "Duration or ParticleSystemAlive";
            LogError(fieldName + " must use " + requiredLifetime + ".", context);
            return 1;
        }

        private static int ValidateUnitAnchors()
        {
            int errors = 0;
            var projectileUnitPrefabs = new HashSet<GameObject>();
            string[] unitDefinitionGuids = AssetDatabase.FindAssets("t:UnitDefinition");
            for (int i = 0; i < unitDefinitionGuids.Length; i++)
            {
                UnitDefinition definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(unitDefinitionGuids[i]));
                if (definition != null
                    && definition.UnitPrefab != null
                    && (definition.Projectile != null || (definition.Special != null && definition.Special.Projectile != null)))
                {
                    projectileUnitPrefabs.Add(definition.UnitPrefab.gameObject);
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (prefab == null || prefab.GetComponent<UnitView>() == null)
                {
                    continue;
                }

                UnitVfxAnchors anchors = prefab.GetComponentInChildren<UnitVfxAnchors>(true);
                if (anchors == null)
                {
                    LogError("A UnitView prefab is missing UnitVfxAnchors.", prefab);
                    errors++;
                    continue;
                }

                if (!anchors.HasAnchor(UnitVfxAnchor.Ground))
                {
                    LogError("A UnitView prefab is missing its Ground VFX anchor.", prefab);
                    errors++;
                }

                if (!anchors.HasAnchor(UnitVfxAnchor.Body))
                {
                    LogError("A UnitView prefab is missing its Body VFX anchor.", prefab);
                    errors++;
                }

                if (!anchors.HasAnchor(UnitVfxAnchor.Overhead))
                {
                    LogError("A UnitView prefab is missing its Overhead VFX anchor.", prefab);
                    errors++;
                }

                if (projectileUnitPrefabs.Contains(prefab) && !anchors.TryGetProjectileLaunch(out Transform _))
                {
                    LogError("A projectile UnitView prefab is missing its Projectile Launch anchor.", prefab);
                    errors++;
                }
            }

            return errors;
        }

        private static bool IsUnitAnchor(UnitVfxAnchor anchor)
        {
            return anchor == UnitVfxAnchor.Ground
                || anchor == UnitVfxAnchor.Body
                || anchor == UnitVfxAnchor.Overhead;
        }

        private static void LogError(string message, Object context)
        {
            Debug.LogError("Battle VFX validation: " + message, context);
        }
    }
}
#endif
