using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class BattleProjectilePresenter
    {
        private readonly BoardPresenter boardPresenter;
        private readonly BattlePresentationLookup presentationLookup;
        private readonly UnitViewRegistry unitViews;
        private readonly Transform effectRoot;
        private readonly List<ProjectileView> activeProjectileViews = new List<ProjectileView>(8);
        private readonly Dictionary<int, ProjectileView> projectileViewById = new Dictionary<int, ProjectileView>(8);
        private readonly Dictionary<ProjectileView, Stack<ProjectileView>> pooledProjectileViews = new Dictionary<ProjectileView, Stack<ProjectileView>>(4);
        private float combatSpeed = 1f;

        public BattleProjectilePresenter(
            BoardPresenter boardPresenter,
            BattlePresentationLookup presentationLookup,
            UnitViewRegistry unitViews,
            Transform effectRoot)
        {
            this.boardPresenter = boardPresenter;
            this.presentationLookup = presentationLookup;
            this.unitViews = unitViews;
            this.effectRoot = effectRoot;
        }

        public void Tick()
        {
            for (int i = activeProjectileViews.Count - 1; i >= 0; i--)
            {
                ProjectileView projectileView = activeProjectileViews[i];
                if (projectileView != null && projectileView.IsPlaying)
                {
                    continue;
                }

                if (projectileView != null)
                {
                    projectileView.Release();
                    ReturnToPool(projectileView);
                }

                activeProjectileViews.RemoveAt(i);
            }
        }

        public void SetCombatSpeed(float speed)
        {
            float safeSpeed = BattleTiming.ResolveAcceleratedCombatSpeed(speed);
            if (Mathf.Approximately(combatSpeed, safeSpeed))
            {
                return;
            }

            combatSpeed = safeSpeed;
            for (int i = 0; i < activeProjectileViews.Count; i++)
            {
                ProjectileView projectileView = activeProjectileViews[i];
                if (projectileView != null)
                {
                    projectileView.SetCombatSpeed(combatSpeed);
                }
            }
        }

        public void HandleLaunched(BattleEvent battleEvent)
        {
            if (presentationLookup == null
                || !presentationLookup.TryGetProjectile(
                    battleEvent.PresentationId,
                    out ProjectileView projectilePrefab,
                    out float spawnHeight,
                    out float hitHeight))
            {
                return;
            }

            Vector3 fallbackLaunchPosition = boardPresenter.GetWorldPosition(battleEvent.From);
            fallbackLaunchPosition.y += spawnHeight;
            UnitView sourceView = unitViews.TryGet(battleEvent.UnitId, out UnitView resolvedSourceView)
                ? resolvedSourceView
                : null;
            Vector3 from = ResolveLaunchPosition(sourceView, fallbackLaunchPosition);
            Vector3 fallbackTarget = boardPresenter.GetWorldPosition(battleEvent.To);
            fallbackTarget.y += hitHeight;
            Transform targetTransform = unitViews.TryGet(battleEvent.TargetUnitId, out UnitView targetView)
                ? targetView.transform
                : null;

            ProjectileView projectileView = GetFromPool(projectilePrefab);
            projectileView.SetCombatSpeed(combatSpeed);
            projectileView.Play(from, targetTransform, fallbackTarget, battleEvent.Duration);
            activeProjectileViews.Add(projectileView);
            projectileViewById[battleEvent.ProjectileId] = projectileView;
        }

        internal static Vector3 ResolveLaunchPosition(UnitView sourceView, Vector3 fallbackLaunchPosition)
        {
            return sourceView != null && sourceView.TryGetProjectileLaunchAnchor(out Transform launchAnchor)
                ? launchAnchor.position
                : fallbackLaunchPosition;
        }

        public void HandleResolved(BattleEvent battleEvent)
        {
            if (projectileViewById.TryGetValue(battleEvent.ProjectileId, out ProjectileView view) && view != null)
            {
                view.Resolve();
                projectileViewById.Remove(battleEvent.ProjectileId);
            }
        }

        public void Clear()
        {
            for (int i = activeProjectileViews.Count - 1; i >= 0; i--)
            {
                ProjectileView projectileView = activeProjectileViews[i];
                if (projectileView != null)
                {
                    projectileView.Release();
                    ReturnToPool(projectileView);
                }
            }

            activeProjectileViews.Clear();
            projectileViewById.Clear();
        }

        private ProjectileView GetFromPool(ProjectileView prefab)
        {
            if (!pooledProjectileViews.TryGetValue(prefab, out Stack<ProjectileView> pool))
            {
                pool = new Stack<ProjectileView>(4);
                pooledProjectileViews.Add(prefab, pool);
            }

            ProjectileView view = pool.Count > 0 ? pool.Pop() : Object.Instantiate(prefab, effectRoot);
            view.SetPoolPrefab(prefab);
            return view;
        }

        private void ReturnToPool(ProjectileView projectileView)
        {
            projectileView.SetCombatSpeed(1f);
            ProjectileView prefab = projectileView.PoolPrefab;
            if (prefab == null)
            {
                Object.Destroy(projectileView.gameObject);
                return;
            }

            if (!pooledProjectileViews.TryGetValue(prefab, out Stack<ProjectileView> pool))
            {
                pool = new Stack<ProjectileView>(4);
                pooledProjectileViews.Add(prefab, pool);
            }

            pool.Push(projectileView);
        }
    }
}
