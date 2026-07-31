using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class UnitViewRegistry
    {
        private readonly BattlePresentationCatalog presentationCatalog;
        private readonly Transform parent;
        private readonly UnityEngine.Object context;
        private readonly Dictionary<int, UnitView> viewsByUnitId = new Dictionary<int, UnitView>(16);

        public UnitViewRegistry(BattlePresentationCatalog presentationCatalog, Transform parent, UnityEngine.Object context)
        {
            this.presentationCatalog = presentationCatalog;
            this.parent = parent;
            this.context = context;
        }

        public UnitView GetOrCreate(UnitPresentationState state)
        {
            if (TryGet(state.UnitId, out UnitView view))
            {
                return view;
            }

            if (presentationCatalog == null || !presentationCatalog.TryGetUnitPrefab(state.PresentationId, out UnitView prefab))
            {
                Debug.LogError("Battle presentation catalog is missing UnitView prefab for id " + state.PresentationId + ".", context);
                return null;
            }

            view = UnityEngine.Object.Instantiate(prefab, parent);
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

        internal void RegisterExisting(int unitId, UnitView view)
        {
            if (unitId <= 0) throw new ArgumentOutOfRangeException(nameof(unitId));
            if (view == null) throw new ArgumentNullException(nameof(view));

            if (TryGet(unitId, out UnitView existing) && existing != view)
            {
                throw new InvalidOperationException("A UnitView is already registered for unit " + unitId + ".");
            }

            viewsByUnitId[unitId] = view;
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
