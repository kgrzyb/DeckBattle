using UnityEngine;
using UnityEngine.EventSystems;

namespace DeckBattle
{
    public sealed class BattleInputController : MonoBehaviour
    {
        private enum InputMode
        {
            Idle,
            DraggingCard,
            SelectedCard,
            SelectedUnit
        }

        [SerializeField] private BattleController battleController;
        [SerializeField] private BattleUIController uiController;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private LayerMask boardRaycastMask = ~0;
        [SerializeField] private float raycastDistance = 80f;

        private readonly RaycastHit[] raycastHits = new RaycastHit[8];

        private InputMode mode;
        private CardRuntimeState draggedCard;
        private CardView draggedCardView;
        private HexTileView currentDragTile;
        private bool currentDragTileLegal;
        private CardRuntimeState selectedCard;
        private RuntimeUnit selectedUnit;

        private void Awake()
        {
            if (battleCamera == null)
            {
                battleCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            if (battleController != null)
            {
                battleController.StateChanged += HandleBattleStateChanged;
            }
        }

        private void OnDisable()
        {
            if (battleController != null)
            {
                battleController.StateChanged -= HandleBattleStateChanged;
            }
        }

        private void Update()
        {
            if (mode == InputMode.DraggingCard)
            {
                return;
            }

            Vector2 screenPosition;
            int pointerId;
            if (!TryGetPointerDown(out screenPosition, out pointerId))
            {
                return;
            }

            if (IsPointerOverUi(pointerId))
            {
                return;
            }

            HandleBoardTap(screenPosition);
        }

        public bool BeginCardDrag(CardView cardView, CardRuntimeState card, Vector2 screenPosition)
        {
            BattleState state = battleController != null ? battleController.State : null;
            if (!PreparationTurnService.CanPlayerPrepare(state) || card == null)
            {
                return false;
            }

            ClearSelection();
            mode = InputMode.DraggingCard;
            draggedCard = card;
            draggedCardView = cardView;

            if (uiController != null)
            {
                uiController.ShowCardDetails(card);
                uiController.ShowCardGhost(card, screenPosition);
            }

            UpdateCardDrag(cardView, screenPosition);
            return true;
        }

        public void UpdateCardDrag(CardView cardView, Vector2 screenPosition)
        {
            if (mode != InputMode.DraggingCard || cardView != draggedCardView)
            {
                return;
            }

            if (uiController != null)
            {
                uiController.MoveCardGhost(screenPosition);
            }

            HexTileView tile = RaycastForTile(screenPosition);
            bool legal = false;
            BattleState state = battleController != null ? battleController.State : null;
            if (tile != null && state != null)
            {
                legal = UnitPlayService.ValidatePlay(state, state.Player, draggedCard, tile.Coord) == PlayUnitFailReason.None;
            }

            currentDragTile = tile;
            currentDragTileLegal = legal;

            BoardPresenter boardPresenter = battleController != null ? battleController.BoardPresenter : null;
            if (boardPresenter != null)
            {
                boardPresenter.HighlightSingleTile(tile, legal);
            }
        }

        public void EndCardDrag(CardView cardView, Vector2 screenPosition)
        {
            if (mode != InputMode.DraggingCard || cardView != draggedCardView)
            {
                return;
            }

            UpdateCardDrag(cardView, screenPosition);

            if (currentDragTile != null && currentDragTileLegal && battleController != null)
            {
                battleController.TryPlayPlayerCard(draggedCard, currentDragTile.Coord);
            }

            if (uiController != null)
            {
                uiController.HideCardGhost();
                uiController.HideCardDetails();
            }

            ClearSelection();
        }

        public void HandleCardTap(CardRuntimeState card)
        {
            BattleState state = battleController != null ? battleController.State : null;
            if (card == null)
            {
                return;
            }

            bool canSelectForPlay = PreparationTurnService.CanPlayerPrepare(state);
            ClearSelection(false);

            selectedCard = card;
            mode = canSelectForPlay ? InputMode.SelectedCard : InputMode.Idle;

            if (uiController != null)
            {
                uiController.ShowCardDetails(card);
                uiController.SetSelectedCard(card);
            }

            if (canSelectForPlay)
            {
                HighlightSelectedCardPlayableTiles();
            }
            else
            {
                BoardPresenter boardPresenter = battleController != null ? battleController.BoardPresenter : null;
                if (boardPresenter != null)
                {
                    boardPresenter.ClearAllHighlights();
                }
            }
        }

        public void HideCardDetails()
        {
            ClearSelection();
        }

        private void HandleBoardTap(Vector2 screenPosition)
        {
            if (mode == InputMode.SelectedCard && selectedCard != null)
            {
                HandleSelectedCardBoardTap(screenPosition);
                return;
            }

            if (selectedCard != null)
            {
                ClearSelection();
                return;
            }

            HideShownCardDetails();

            UnitView unitView = RaycastForUnit(screenPosition);
            if (unitView != null && unitView.Unit != null && unitView.Unit.Side == BattleSide.Player)
            {
                ClearSelection();
                SelectUnit(unitView.Unit);
                return;
            }

            HexTileView tile = RaycastForTile(screenPosition);
            if (mode == InputMode.SelectedUnit && selectedUnit != null)
            {
                if (tile != null && battleController != null)
                {
                    battleController.TryMovePlayerUnit(selectedUnit, tile.Coord);
                }

                ClearSelection();
                return;
            }

            ClearSelection();
        }

        private void SelectUnit(RuntimeUnit unit)
        {
            BattleState state = battleController != null ? battleController.State : null;
            if (!PreparationTurnService.CanPlayerPrepare(state))
            {
                return;
            }

            mode = InputMode.SelectedUnit;
            selectedUnit = unit;

            BoardPresenter boardPresenter = battleController.BoardPresenter;
            if (boardPresenter != null)
            {
                boardPresenter.HighlightFormationTiles(state, state.Player, selectedUnit);
            }
        }

        private void HandleSelectedCardBoardTap(Vector2 screenPosition)
        {
            BattleState state = battleController != null ? battleController.State : null;
            if (!PreparationTurnService.CanPlayerPrepare(state))
            {
                ClearSelection();
                return;
            }

            HexTileView tile = RaycastForTile(screenPosition);
            if (tile == null)
            {
                ClearSelection();
                return;
            }

            bool played = battleController != null && battleController.TryPlayPlayerCard(selectedCard, tile.Coord);
            if (played)
            {
                ClearSelection();
                return;
            }

            HighlightSelectedCardPlayableTiles();
        }

        private void HighlightSelectedCardPlayableTiles()
        {
            BattleState state = battleController != null ? battleController.State : null;
            BoardPresenter boardPresenter = battleController != null ? battleController.BoardPresenter : null;
            if (boardPresenter != null)
            {
                boardPresenter.HighlightPlayableTiles(state, state != null ? state.Player : null, selectedCard);
            }
        }

        private void HandleBattleStateChanged()
        {
            BattleState state = battleController != null ? battleController.State : null;
            if (selectedCard != null && !IsCardInPlayerHand(state, selectedCard))
            {
                ClearSelection();
                return;
            }

            if (mode == InputMode.SelectedCard && !PreparationTurnService.CanPlayerPrepare(state))
            {
                ClearSelection();
                return;
            }

            if (mode == InputMode.SelectedCard)
            {
                HighlightSelectedCardPlayableTiles();
            }
        }

        private bool IsCardInPlayerHand(BattleState state, CardRuntimeState card)
        {
            if (state == null || state.Player == null || card == null)
            {
                return false;
            }

            for (int i = 0; i < state.Player.Hand.Count; i++)
            {
                if (state.Player.Hand[i] == card)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearSelection(bool hideCardDetails = true)
        {
            if (hideCardDetails)
            {
                HideShownCardDetails();
            }

            mode = InputMode.Idle;
            draggedCard = null;
            draggedCardView = null;
            currentDragTile = null;
            currentDragTileLegal = false;
            selectedCard = null;
            selectedUnit = null;

            if (uiController != null)
            {
                uiController.SetSelectedCard(null);
            }

            BoardPresenter boardPresenter = battleController != null ? battleController.BoardPresenter : null;
            if (boardPresenter != null)
            {
                boardPresenter.ClearAllHighlights();
            }
        }

        private void HideShownCardDetails()
        {
            if (uiController != null)
            {
                uiController.HideCardDetails();
            }
        }

        private HexTileView RaycastForTile(Vector2 screenPosition)
        {
            int count = Raycast(screenPosition);
            for (int i = 0; i < count; i++)
            {
                HexTileView tile = raycastHits[i].collider.GetComponentInParent<HexTileView>();
                if (tile != null)
                {
                    return tile;
                }
            }

            return null;
        }

        private UnitView RaycastForUnit(Vector2 screenPosition)
        {
            int count = Raycast(screenPosition);
            for (int i = 0; i < count; i++)
            {
                UnitView unit = raycastHits[i].collider.GetComponentInParent<UnitView>();
                if (unit != null)
                {
                    return unit;
                }
            }

            return null;
        }

        private int Raycast(Vector2 screenPosition)
        {
            if (battleCamera == null)
            {
                return 0;
            }

            Ray ray = battleCamera.ScreenPointToRay(screenPosition);
            return Physics.RaycastNonAlloc(ray, raycastHits, raycastDistance, boardRaycastMask, QueryTriggerInteraction.Ignore);
        }

        private static bool TryGetPointerDown(out Vector2 screenPosition, out int pointerId)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                screenPosition = touch.position;
                pointerId = touch.fingerId;
                return touch.phase == TouchPhase.Began;
            }

            screenPosition = Input.mousePosition;
            pointerId = -1;
            return Input.GetMouseButtonDown(0);
        }

        private static bool IsPointerOverUi(int pointerId)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            return pointerId >= 0 ? EventSystem.current.IsPointerOverGameObject(pointerId) : EventSystem.current.IsPointerOverGameObject();
        }
    }
}
