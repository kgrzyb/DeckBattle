using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class BattleView : MonoBehaviour
    {
        [Header("Presentation")]
        [SerializeField] private BoardPresenter boardPresenter;
        [SerializeField] private Transform unitRoot;
        [SerializeField] private UnitStatusOverlayController statusOverlayController;
        [SerializeField] private StatusPresentationCatalog statusPresentationCatalog;
        [SerializeField] private UnitStatusVfxController statusVfxController;
        [SerializeField] private BattlePresentationCatalog presentationCatalog;
        [SerializeField] private PooledBattleEffect attackEffectPrefab;
        [SerializeField] private PooledBattleEffect damageEffectPrefab;
        [SerializeField] private Transform effectRoot;

        private UnitViewRegistry unitViewRegistry;
        private BattleUnitPresenter unitPresenter;
        private BattleProjectilePresenter projectilePresenter;
        private BattleEffectPresenter effectPresenter;

        private readonly Dictionary<int, UnitPresentationState> presentationStateByUnitId = new Dictionary<int, UnitPresentationState>(16);
        private readonly Dictionary<int, List<StatusPresentationState>> statusStatesByUnitId = new Dictionary<int, List<StatusPresentationState>>(16);
        private readonly Dictionary<int, int> shieldByUnitId = new Dictionary<int, int>(16);
        private float presentationTickDuration = BattleTiming.DefaultCombatTickDuration;
        private float combatSpeed = 1f;

        public BoardPresenter BoardPresenter
        {
            get { return boardPresenter; }
        }

        public UnitViewRegistry UnitViews
        {
            get { return EnsureUnitViewRegistry(); }
        }

        public void SetPresentationTickDuration(float tickDuration)
        {
            float safeDuration = Mathf.Max(BattleTiming.MinCombatTickDuration, tickDuration);
            if (Mathf.Approximately(presentationTickDuration, safeDuration))
            {
                return;
            }

            presentationTickDuration = safeDuration;
            unitPresenter = null;
        }

        public void SetCombatSpeed(float speed)
        {
            float safeSpeed = BattleTiming.ResolveAcceleratedCombatSpeed(speed);
            if (Mathf.Approximately(combatSpeed, safeSpeed))
            {
                return;
            }

            combatSpeed = safeSpeed;
            unitViewRegistry?.SetCombatSpeed(combatSpeed);
            projectilePresenter?.SetCombatSpeed(combatSpeed);
            effectPresenter?.SetCombatSpeed(combatSpeed);
            statusOverlayController?.SetCombatSpeed(combatSpeed);
            statusVfxController?.SetCombatSpeed(combatSpeed);
        }

        private void Awake()
        {
            if (statusOverlayController != null)
            {
                statusOverlayController.SetPresentationCatalog(statusPresentationCatalog);
            }

            if (statusVfxController != null)
            {
                statusVfxController.Initialize(statusPresentationCatalog);
            }
        }

        private void Update()
        {
            EnsurePresenters();
            effectPresenter.Tick();
            projectilePresenter.Tick();
        }

        public void BindInitialState(BattlePresentationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (boardPresenter == null)
            {
                Debug.LogError("BattleView is missing required presentation references.", this);
                return;
            }

            if (statusOverlayController != null)
            {
                statusOverlayController.ReleaseAll();
            }

            if (statusVfxController != null)
            {
                statusVfxController.ReleaseAll();
            }

            presentationStateByUnitId.Clear();
            statusStatesByUnitId.Clear();
            shieldByUnitId.Clear();
            EnsurePresenters();
            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                UnitPresentationState state = snapshot.Units[i];
                presentationStateByUnitId[state.UnitId] = state;
                unitPresenter.BindInitial(state);
            }
        }

        public void ClearBattle()
        {
            ClearBattle(true);
        }

        public void ClearBattle(bool releaseUnitViews)
        {
            SetCombatSpeed(1f);
            presentationStateByUnitId.Clear();
            statusStatesByUnitId.Clear();
            shieldByUnitId.Clear();
            if (statusVfxController != null)
            {
                statusVfxController.ReleaseAll();
            }

            if (releaseUnitViews)
            {
                if (statusOverlayController != null)
                {
                    statusOverlayController.ReleaseAll();
                }

                EnsureUnitViewRegistry().ReleaseAll();
            }

            EnsurePresenters();
            effectPresenter.Clear();
            projectilePresenter.Clear();
        }

        public void ProcessCombatTick(BattleTickResult tickResult, IReadOnlyList<BattleEvent> events)
        {
            if (events == null)
            {
                return;
            }

            EnsurePresenters();
            for (int i = 0; i < events.Count; i++)
            {
                BattleEvent battleEvent = events[i];
                switch (battleEvent.Type)
                {
                    case BattleEventType.UnitMoved:
                        unitPresenter.HandleMoved(battleEvent);
                        break;
                    case BattleEventType.UnitAttackStarted:
                        // Kept for legacy consumers; phase events drive this view.
                        break;
                    case BattleEventType.AttackWindupStarted:
                        unitPresenter.HandleAttackWindupStarted(battleEvent);
                        break;
                    case BattleEventType.AttackWindupCancelled:
                        unitPresenter.HandleAttackWindupCancelled(battleEvent);
                        break;
                    case BattleEventType.AttackFired:
                        unitPresenter.HandleAttackFired(battleEvent);
                        effectPresenter.PlayAttack(boardPresenter.GetWorldPosition(battleEvent.From));
                        break;
                    case BattleEventType.SpecialWindupStarted:
                        unitPresenter.HandleSpecialWindupStarted(battleEvent);
                        break;
                    case BattleEventType.SpecialWindupCancelled:
                        unitPresenter.HandleSpecialWindupCancelled(battleEvent);
                        break;
                    case BattleEventType.UnitSpecialActivated:
                        unitPresenter.HandleSpecialActivated(battleEvent);
                        break;
                    case BattleEventType.UnitDamaged:
                        HandleUnitDamaged(battleEvent);
                        break;
                    case BattleEventType.UnitDied:
                        HandleUnitDied(battleEvent);
                        break;
                    case BattleEventType.BattleEnded:
                        HandleBattleEnded();
                        break;
                    case BattleEventType.UnitManaChanged:
                        HandleUnitManaChanged(battleEvent);
                        break;
                    case BattleEventType.UnitTargetChanged:
                        unitPresenter.HandleTargetChanged(battleEvent);
                        break;
                    case BattleEventType.StatusApplied:
                        HandleStatusPresentationEvent(battleEvent);
                        HandleStatusChanged(battleEvent);
                        break;
                    case BattleEventType.StatusRefreshed:
                    case BattleEventType.StatusStackChanged:
                        HandleStatusPresentationEvent(battleEvent);
                        HandleStatusChanged(battleEvent);
                        break;
                    case BattleEventType.StatusRemoved:
                        HandleStatusPresentationEvent(battleEvent);
                        HandleStatusChanged(battleEvent);
                        break;
                    case BattleEventType.ShieldChanged:
                        HandleStatusChanged(battleEvent);
                        break;
                    case BattleEventType.ProjectileLaunched:
                        projectilePresenter.HandleLaunched(battleEvent);
                        break;
                    case BattleEventType.ProjectileResolved:
                        projectilePresenter.HandleResolved(battleEvent);
                        break;
                }
            }

        }

        private void HandleUnitDamaged(BattleEvent battleEvent)
        {
            unitPresenter.HandleDamaged(battleEvent);
            if (presentationStateByUnitId.TryGetValue(battleEvent.UnitId, out UnitPresentationState state))
            {
                statusOverlayController?.SetHealth(state.UnitId, battleEvent.RemainingHp, state.MaxHp);
                effectPresenter.PlayDamage(boardPresenter.GetWorldPosition(battleEvent.To));
            }
        }

        private void HandleUnitDied(BattleEvent battleEvent)
        {
            unitPresenter.HandleDied(battleEvent);
            statusOverlayController?.ReleaseAfterDamageAnimation(battleEvent.UnitId);

            statusStatesByUnitId.Remove(battleEvent.UnitId);
            shieldByUnitId.Remove(battleEvent.UnitId);
        }

        private void HandleUnitManaChanged(BattleEvent battleEvent)
        {
            if (!presentationStateByUnitId.TryGetValue(battleEvent.UnitId, out UnitPresentationState state))
            {
                return;
            }

            unitPresenter.HandleManaChanged(battleEvent, state);
        }

        private void HandleBattleEnded()
        {
            if (statusVfxController != null)
            {
                statusVfxController.ReleaseAll();
            }
        }

        private void HandleStatusChanged(BattleEvent battleEvent)
        {
            switch (battleEvent.Type)
            {
                case BattleEventType.StatusApplied:
                case BattleEventType.StatusRefreshed:
                case BattleEventType.StatusStackChanged:
                    UpdatePresentationStatus(
                        battleEvent.UnitId,
                        battleEvent.TargetUnitId,
                        battleEvent.StatusKind,
                        battleEvent.StatusStackCount);
                    break;
                case BattleEventType.StatusRemoved:
                    RemovePresentationStatus(battleEvent.UnitId, battleEvent.TargetUnitId, battleEvent.StatusKind);
                    break;
                case BattleEventType.ShieldChanged:
                    shieldByUnitId[battleEvent.UnitId] = Mathf.Max(0, battleEvent.Amount);
                    break;
            }

            if (statusOverlayController == null)
            {
                return;
            }

            statusStatesByUnitId.TryGetValue(battleEvent.UnitId, out List<StatusPresentationState> statuses);
            shieldByUnitId.TryGetValue(battleEvent.UnitId, out int totalShield);
            statusOverlayController.SetPresentationStatuses(battleEvent.UnitId, statuses, totalShield);
        }

        private void UpdatePresentationStatus(int unitId, int sourceUnitId, StatusKind kind, int stacks)
        {
            if (kind == StatusKind.None)
            {
                return;
            }

            if (!statusStatesByUnitId.TryGetValue(unitId, out List<StatusPresentationState> statuses))
            {
                statuses = new List<StatusPresentationState>(4);
                statusStatesByUnitId.Add(unitId, statuses);
            }

            for (int i = 0; i < statuses.Count; i++)
            {
                StatusPresentationState status = statuses[i];
                if (status.Kind != kind || status.SourceUnitId != sourceUnitId)
                {
                    continue;
                }

                statuses[i] = new StatusPresentationState(kind, sourceUnitId, stacks);
                return;
            }

            statuses.Add(new StatusPresentationState(kind, sourceUnitId, stacks));
        }

        private void RemovePresentationStatus(int unitId, int sourceUnitId, StatusKind kind)
        {
            if (!statusStatesByUnitId.TryGetValue(unitId, out List<StatusPresentationState> statuses))
            {
                return;
            }

            for (int i = statuses.Count - 1; i >= 0; i--)
            {
                StatusPresentationState status = statuses[i];
                if (status.Kind == kind && status.SourceUnitId == sourceUnitId)
                {
                    statuses.RemoveAt(i);
                }
            }

            if (statuses.Count == 0)
            {
                statusStatesByUnitId.Remove(unitId);
            }
        }

        private void HandleStatusPresentationEvent(BattleEvent battleEvent)
        {
            if (statusVfxController != null)
            {
                statusVfxController.HandleStatusEvent(battleEvent);
            }
        }

        private UnitViewRegistry EnsureUnitViewRegistry()
        {
            if (unitViewRegistry == null)
            {
                unitViewRegistry = new UnitViewRegistry(
                    presentationCatalog,
                    unitRoot != null ? unitRoot : transform,
                    this);
                unitViewRegistry.SetCombatSpeed(combatSpeed);
            }

            return unitViewRegistry;
        }

        private void EnsurePresenters()
        {
            UnitViewRegistry unitViews = EnsureUnitViewRegistry();
            if (unitPresenter == null)
            {
                unitPresenter = new BattleUnitPresenter(
                    boardPresenter,
                    unitViews,
                    statusOverlayController,
                    statusVfxController,
                    presentationTickDuration);
            }

            if (projectilePresenter == null)
            {
                projectilePresenter = new BattleProjectilePresenter(
                    boardPresenter,
                    presentationCatalog,
                    unitViews,
                    effectRoot != null ? effectRoot : transform);
                projectilePresenter.SetCombatSpeed(combatSpeed);
            }

            if (effectPresenter == null)
            {
                effectPresenter = new BattleEffectPresenter(
                    attackEffectPrefab,
                    damageEffectPrefab,
                    effectRoot != null ? effectRoot : transform);
                effectPresenter.SetCombatSpeed(combatSpeed);
            }
        }

    }
}
