using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class UnitViewRegistry
    {
        private readonly BattlePresentationLookup presentationLookup;
        private readonly Transform parent;
        private readonly UnityEngine.Object context;
        private readonly Dictionary<int, UnitView> viewsByUnitId = new Dictionary<int, UnitView>(16);
        private float combatSpeed = 1f;
        private float animationCrossFadeDuration = BattleTiming.DefaultAnimationCrossFadeDuration;

        public UnitViewRegistry(BattlePresentationLookup presentationLookup, Transform parent, UnityEngine.Object context)
        {
            this.presentationLookup = presentationLookup;
            this.parent = parent;
            this.context = context;
        }

        public UnitView GetOrCreate(UnitPresentationState state)
        {
            if (TryGet(state.UnitId, out UnitView view))
            {
                return view;
            }

            if (presentationLookup == null
                || !presentationLookup.TryGetUnitViewData(
                    state.PresentationId,
                    out UnitView prefab,
                    out float runAnimationSpeedMultiplier))
            {
                Debug.LogError("Battle presentation lookup is missing UnitView prefab for id " + state.PresentationId + ".", context);
                return null;
            }

            view = UnityEngine.Object.Instantiate(prefab, parent);
            view.SetRunAnimationSpeedMultiplier(runAnimationSpeedMultiplier);
            view.SetCombatSpeed(combatSpeed);
            view.SetAnimationCrossFadeDuration(animationCrossFadeDuration);
            viewsByUnitId.Add(state.UnitId, view);
            return view;
        }

        public bool TryGet(int unitId, out UnitView view)
        {
            if (!viewsByUnitId.TryGetValue(unitId, out view))
            {
                return false;
            }

            if (view != null)
            {
                return true;
            }

            viewsByUnitId.Remove(unitId);
            return false;
        }

        public void Release(int unitId)
        {
            if (!viewsByUnitId.TryGetValue(unitId, out UnitView view))
            {
                return;
            }

            viewsByUnitId.Remove(unitId);
            ReleaseView(view);
        }

        public void ReleaseAll()
        {
            foreach (KeyValuePair<int, UnitView> entry in viewsByUnitId)
            {
                ReleaseView(entry.Value);
            }

            viewsByUnitId.Clear();
        }

        public void SetCombatSpeed(float speed)
        {
            float safeSpeed = BattleTiming.ResolveAcceleratedCombatSpeed(speed);
            if (Mathf.Approximately(combatSpeed, safeSpeed))
            {
                return;
            }

            combatSpeed = safeSpeed;
            foreach (KeyValuePair<int, UnitView> entry in viewsByUnitId)
            {
                if (entry.Value != null)
                {
                    entry.Value.SetCombatSpeed(combatSpeed);
                }
            }
        }

        public void SetAnimationCrossFadeDuration(float duration)
        {
            float safeDuration = BattleTiming.ResolveAnimationCrossFadeDuration(duration);
            if (Mathf.Approximately(animationCrossFadeDuration, safeDuration))
            {
                return;
            }

            animationCrossFadeDuration = safeDuration;
            foreach (KeyValuePair<int, UnitView> entry in viewsByUnitId)
            {
                if (entry.Value != null)
                {
                    entry.Value.SetAnimationCrossFadeDuration(animationCrossFadeDuration);
                }
            }
        }

        internal void RegisterExisting(int unitId, UnitView view)
        {
            if (unitId <= 0) throw new ArgumentOutOfRangeException(nameof(unitId));
            if (view == null) throw new ArgumentNullException(nameof(view));

            if (TryGet(unitId, out UnitView existing) && existing != view)
            {
                throw new InvalidOperationException("A UnitView is already registered for unit " + unitId + ".");
            }

            viewsByUnitId[unitId] = view;
            view.SetCombatSpeed(combatSpeed);
            view.SetAnimationCrossFadeDuration(animationCrossFadeDuration);
        }

        private static void ReleaseView(UnitView view)
        {
            if (view == null)
            {
                return;
            }

            view.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(view.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }
    }
}
