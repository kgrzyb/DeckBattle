using UnityEngine;

namespace DeckBattle
{
    public sealed class BattleUnitPresenter
    {
        private readonly BoardPresenter boardPresenter;
        private readonly UnitViewRegistry unitViews;
        private readonly UnitStatusOverlayController statusOverlayController;
        private readonly UnitStatusVfxController statusVfxController;
        private readonly FloatingDamageTextController floatingDamageTextController;
        private readonly float defaultTickDuration;

        public BattleUnitPresenter(
            BoardPresenter boardPresenter,
            UnitViewRegistry unitViews,
            UnitStatusOverlayController statusOverlayController,
            UnitStatusVfxController statusVfxController,
            FloatingDamageTextController floatingDamageTextController,
            float defaultTickDuration)
        {
            this.boardPresenter = boardPresenter;
            this.unitViews = unitViews;
            this.statusOverlayController = statusOverlayController;
            this.statusVfxController = statusVfxController;
            this.floatingDamageTextController = floatingDamageTextController;
            this.defaultTickDuration = defaultTickDuration;
        }

        public void BindInitial(UnitPresentationState state)
        {
            UnitView view = unitViews.GetOrCreate(state);
            if (view == null)
            {
                return;
            }

            view.Bind(state, boardPresenter.GetWorldPosition(state.Hex));
            view.FaceWorldPosition(boardPresenter.GetWorldCenter(), true);
            statusOverlayController?.BindPresentationUnit(state, view);
            statusVfxController?.BindPresentationUnit(state.UnitId, view);
        }

        public void HandleMoved(BattleEvent battleEvent)
        {
            if (!unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                return;
            }

            float duration = CalculatePresentationMovementDuration(battleEvent.Duration, defaultTickDuration);
            view.MoveToWorldPosition(boardPresenter.GetWorldPosition(battleEvent.To), duration);
        }

        internal static float CalculatePresentationMovementDuration(float movementDuration, float tickDuration)
        {
            float safeTickDuration = Mathf.Max(BattleTiming.MinCombatTickDuration, tickDuration);
            float safeMovementDuration = movementDuration > 0f ? movementDuration : safeTickDuration;
            return Mathf.Ceil(safeMovementDuration / safeTickDuration) * safeTickDuration;
        }

        public void HandleDamaged(BattleEvent battleEvent)
        {
            if (unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                view.PlayDamage(battleEvent.RemainingHp);
                ShowDamageText(view.transform.position, battleEvent);
                return;
            }

            ShowDamageText(boardPresenter.GetWorldPosition(battleEvent.To), battleEvent);
        }

        public void HandleDied(BattleEvent battleEvent)
        {
            if (unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                view.PlayDeath();
            }

            statusVfxController?.Release(battleEvent.UnitId);
        }

        public void HandleManaChanged(BattleEvent battleEvent, UnitPresentationState state)
        {
            statusOverlayController?.SetMana(state.UnitId, battleEvent.CurrentMana, state.MaxMana);
        }

        public void HandleTargetChanged(BattleEvent battleEvent)
        {
            if (!unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                return;
            }

            if (battleEvent.TargetUnitId > 0)
            {
                view.SetTargetWorldPosition(boardPresenter.GetWorldPosition(battleEvent.To));
            }
            else
            {
                view.ClearTargetWorldPosition();
            }
        }

        public void HandleAttackWindupStarted(BattleEvent battleEvent)
        {
            if (!unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                return;
            }

            view.SetTargetWorldPosition(boardPresenter.GetWorldPosition(battleEvent.To));
            view.BeginAttackWindup(battleEvent.SequenceId, battleEvent.Duration);
        }

        public void HandleAttackWindupCancelled(BattleEvent battleEvent)
        {
            if (unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                view.CancelAttackWindup(battleEvent.SequenceId);
            }
        }

        public void HandleAttackFired(BattleEvent battleEvent)
        {
            if (!unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                return;
            }

            view.SetTargetWorldPosition(boardPresenter.GetWorldPosition(battleEvent.To));
            view.PlayAttackFire(battleEvent.SequenceId);
        }

        public void HandleSpecialWindupStarted(BattleEvent battleEvent)
        {
            if (unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                if (battleEvent.TargetUnitId > 0)
                {
                    view.SetTargetWorldPosition(boardPresenter.GetWorldPosition(battleEvent.To));
                }

                view.BeginSpecialWindup(battleEvent.SequenceId, battleEvent.SpecialKind, battleEvent.Duration);
            }
        }

        public void HandleSpecialWindupCancelled(BattleEvent battleEvent)
        {
            if (unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                view.CancelSpecialWindup(battleEvent.SequenceId);
            }
        }

        public void HandleSpecialActivated(BattleEvent battleEvent)
        {
            if (unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                view.CompleteSpecialWindup(battleEvent.SequenceId);
            }
        }

        public void HandleSpecialCastStarted(BattleEvent battleEvent)
        {
            if (unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                view.SetTargetWorldPosition(boardPresenter.GetWorldPosition(battleEvent.To));
                view.BeginSpecialCast(battleEvent.SequenceId);
            }
        }

        public void HandleSpecialStrikeFired(BattleEvent battleEvent)
        {
            if (unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                view.SetTargetWorldPosition(boardPresenter.GetWorldPosition(battleEvent.To));
                view.PlaySpecialStrike(battleEvent.SequenceId);
            }
        }

        private void ShowDamageText(Vector3 worldPosition, BattleEvent battleEvent)
        {
            if (battleEvent.Amount <= 0 || floatingDamageTextController == null)
            {
                return;
            }

            floatingDamageTextController.Show(
                worldPosition,
                battleEvent.Amount,
                battleEvent.IsCritical ? FloatingDamageTextType.Critical : FloatingDamageTextType.Normal);
        }
    }
}
