using UnityEngine;

namespace DeckBattle
{
    public sealed class BattleUnitPresenter
    {
        private readonly BoardPresenter boardPresenter;
        private readonly UnitViewRegistry unitViews;
        private readonly UnitStatusOverlayController statusOverlayController;
        private readonly UnitStatusVfxController statusVfxController;
        private readonly float defaultTickDuration;

        public BattleUnitPresenter(
            BoardPresenter boardPresenter,
            UnitViewRegistry unitViews,
            UnitStatusOverlayController statusOverlayController,
            UnitStatusVfxController statusVfxController,
            float defaultTickDuration)
        {
            this.boardPresenter = boardPresenter;
            this.unitViews = unitViews;
            this.statusOverlayController = statusOverlayController;
            this.statusVfxController = statusVfxController;
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
            statusOverlayController?.BindPresentationUnit(state, view);
            statusVfxController?.BindPresentationUnit(state.UnitId, view);
        }

        public void HandleMoved(BattleEvent battleEvent)
        {
            if (!unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                return;
            }

            float duration = battleEvent.Duration > 0f ? battleEvent.Duration : defaultTickDuration;
            view.MoveToWorldPosition(boardPresenter.GetWorldPosition(battleEvent.To), duration);
        }

        public void HandleDamaged(BattleEvent battleEvent)
        {
            if (unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                view.PlayDamage(battleEvent.RemainingHp);
            }
        }

        public void HandleDied(BattleEvent battleEvent)
        {
            if (unitViews.TryGet(battleEvent.UnitId, out UnitView view))
            {
                view.PlayDeath();
            }

            statusOverlayController?.Release(battleEvent.UnitId);
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
                view.BeginSpecialWindup(battleEvent.SequenceId, battleEvent.Duration);
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
    }
}
