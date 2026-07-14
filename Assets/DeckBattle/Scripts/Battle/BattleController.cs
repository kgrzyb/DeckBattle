using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class BattleController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private BattleConfig battleConfig;
        [SerializeField] private List<UnitDefinition> playerDeck = new List<UnitDefinition>(8);
        [SerializeField] private List<UnitDefinition> enemyDeck = new List<UnitDefinition>(8);
        [SerializeField] private int seed = 12345;

        [Header("Presentation")]
        [SerializeField] private BoardPresenter boardPresenter;
        [SerializeField] private UnitView unitPrefab;
        [SerializeField] private Transform unitRoot;

        private readonly List<UnitView> unitViews = new List<UnitView>(16);
        private BattleState state;

        public BattleState State
        {
            get { return state; }
        }

        private void Start()
        {
            StartTestBattle();
        }

        public void StartTestBattle()
        {
            if (battleConfig == null || boardPresenter == null || unitPrefab == null)
            {
                Debug.LogError("BattleController is missing required references.", this);
                return;
            }

            state = BattleState.Create(battleConfig, playerDeck, enemyDeck, seed);
            boardPresenter.Build(state.Board);
            PlayInitialVisibleUnits();
            RefreshUnits();
        }

        private void PlayInitialVisibleUnits()
        {
            TryPlayFirstAvailable(state.Player, new HexCoord(1, 0));
            TryPlayFirstAvailable(state.Player, new HexCoord(3, 0));
            TryPlayFirstAvailable(state.Enemy, new HexCoord(1, battleConfig.BoardHeight - 1));
            TryPlayFirstAvailable(state.Enemy, new HexCoord(3, battleConfig.BoardHeight - 1));
        }

        private void TryPlayFirstAvailable(PlayerBattleState player, HexCoord coord)
        {
            if (player.Hand.Count == 0)
            {
                return;
            }

            for (int i = 0; i < player.Hand.Count; i++)
            {
                CardRuntimeState card = player.Hand[i];
                if (UnitPlayService.ValidatePlay(state, player, card, coord) != PlayUnitFailReason.None)
                {
                    continue;
                }

                UnitPlayService.PlayUnit(state, player, card, coord);
                return;
            }
        }

        private void RefreshUnits()
        {
            ClearUnitViews();
            CreateUnitViews(state.Player.Units);
            CreateUnitViews(state.Enemy.Units);
        }

        private void CreateUnitViews(List<RuntimeUnit> units)
        {
            Transform parent = unitRoot != null ? unitRoot : transform;
            for (int i = 0; i < units.Count; i++)
            {
                RuntimeUnit unit = units[i];
                UnitView view = Instantiate(unitPrefab, parent);
                view.Bind(unit, boardPresenter.GetWorldPosition(unit.FormationCoord));
                unitViews.Add(view);
            }
        }

        private void ClearUnitViews()
        {
            for (int i = unitViews.Count - 1; i >= 0; i--)
            {
                if (unitViews[i] != null)
                {
                    Destroy(unitViews[i].gameObject);
                }
            }

            unitViews.Clear();
        }
    }
}
