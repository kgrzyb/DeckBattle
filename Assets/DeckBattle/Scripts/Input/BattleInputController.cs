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
            PressingUnit,
            DraggingUnit,
            SelectedUnit,
            PendingBoardTap,
            PanningCamera,
            PinchingCamera
        }

        [SerializeField] private BattleController battleController;
        [SerializeField] private BattleUIController uiController;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private BattleCameraController battleCameraController;
        [SerializeField] private LayerMask boardRaycastMask = ~0;
        [SerializeField] private float raycastDistance = 80f;
        [SerializeField] private float unitDragThresholdPixels = 18f;
        [SerializeField] private float cameraDragThresholdPixels = 18f;

        private readonly RaycastHit[] raycastHits = new RaycastHit[8];

        private InputMode mode;
        private CardRuntimeState draggedCard;
        private CardView draggedCardView;
        private HexTileView currentDragTile;
        private RuntimeUnit currentDragSpellTargetUnit;
        private bool currentDragTileLegal;
        private CardRuntimeState selectedCard;
        private RuntimeUnit selectedUnit;
        private RuntimeUnit pressedUnit;
        private UnitView pressedUnitView;
        private int pressedPointerId;
        private Vector2 pressedUnitStartScreenPosition;
        private Vector2 pressedUnitLastScreenPosition;
        private HexCoord currentUnitDragCoord;
        private HexTileView currentUnitDragTile;
        private bool currentUnitDragHasTarget;
        private bool currentUnitDragTileLegal;
        private InputMode modeBeforeCameraGesture;
        private int cameraPointerId = -1;
        private Vector2 cameraGestureStartScreenPosition;
        private Vector2 cameraGestureLastScreenPosition;
        private int pinchFirstPointerId = -1;
        private int pinchSecondPointerId = -1;
        private float lastPinchDistance;

        private void OnValidate()
        {
            unitDragThresholdPixels = Mathf.Max(1f, unitDragThresholdPixels);
            cameraDragThresholdPixels = Mathf.Max(1f, cameraDragThresholdPixels);
        }

        private void Awake()
        {
            if (battleCamera == null)
            {
                battleCamera = Camera.main;
            }

            if (battleCameraController == null && battleCamera != null)
            {
                battleCameraController = battleCamera.GetComponent<BattleCameraController>();
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
            RestoreDraggedCardView();
            CancelCameraGesture();

            if (battleController != null)
            {
                battleController.StateChanged -= HandleBattleStateChanged;
            }
        }

        private void Update()
        {
            UpdateMouseWheelZoom();

            if (mode == InputMode.DraggingCard)
            {
                return;
            }

            if (mode == InputMode.PressingUnit)
            {
                UpdatePressedUnit();
                return;
            }

            if (mode == InputMode.DraggingUnit)
            {
                UpdateUnitDragInput();
                return;
            }

            if (mode == InputMode.PendingBoardTap)
            {
                UpdatePendingBoardTap();
                return;
            }

            if (mode == InputMode.PanningCamera)
            {
                UpdateCameraPan();
                return;
            }

            if (mode == InputMode.PinchingCamera)
            {
                UpdateCameraPinch();
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

            HandleBoardPointerDown(screenPosition, pointerId);
        }

        public bool BeginCardDrag(CardView cardView, CardRuntimeState card, Vector2 screenPosition)
        {
            BattleState state = battleController != null ? battleController.State : null;
            if (!CanPreparePlayableCard(state, card))
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
                uiController.BeginCardDragVisual(cardView, screenPosition);
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
                uiController.MoveCardDragVisual(cardView, screenPosition);
            }

            Vector2 hexScreenPosition = uiController != null
                ? uiController.GetCardDragHexScreenPosition(screenPosition)
                : screenPosition;
            HexTileView tile = RaycastForTile(hexScreenPosition);
            bool legal = false;
            RuntimeUnit spellTargetUnit = null;
            BattleState state = battleController != null ? battleController.State : null;
            if (tile != null && state != null)
            {
                legal = IsCardDragTargetLegal(state, draggedCard, tile, out spellTargetUnit);
            }

            currentDragTile = tile;
            currentDragSpellTargetUnit = spellTargetUnit;
            currentDragTileLegal = legal;

            BoardPresenter boardPresenter = battleController != null ? battleController.BoardPresenter : null;
            if (boardPresenter != null)
            {
                if (draggedCard != null
                    && draggedCard.SpellDefinition != null
                    && draggedCard.SpellDefinition.TargetingKind == SpellTargetingKind.None)
                {
                    boardPresenter.ClearHoverVisualState();
                }
                else
                {
                    boardPresenter.SetHoverVisualState(
                        tile,
                        legal ? PreparationHexVisualState.Legal : PreparationHexVisualState.Blocked);
                }
            }
        }

        public void EndCardDrag(CardView cardView, Vector2 screenPosition)
        {
            if (mode != InputMode.DraggingCard || cardView != draggedCardView)
            {
                return;
            }

            UpdateCardDrag(cardView, screenPosition);

            RestoreDraggedCardView();

            if (currentDragTile != null && currentDragTileLegal && battleController != null)
            {
                PlayDraggedCard();
            }

            if (uiController != null)
            {
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

            bool canSelectForPlay = CanPreparePlayableCard(state, card);
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
                    boardPresenter.ClearAllVisualStates();
                }
            }
        }

        public void HideCardDetails()
        {
            ClearSelection();
        }

        private void HandleBoardPointerDown(Vector2 screenPosition, int pointerId)
        {
            if (TryBeginPlayerUnitPress(screenPosition, pointerId))
            {
                return;
            }

            BeginPendingBoardTap(screenPosition, pointerId);
        }

        private bool TryBeginPlayerUnitPress(Vector2 screenPosition, int pointerId)
        {
            if (mode == InputMode.SelectedCard)
            {
                return false;
            }

            UnitView unitView = RaycastForUnit(screenPosition);
            RuntimeUnit unit = unitView != null ? unitView.Unit : null;
            if (mode == InputMode.SelectedUnit && selectedUnit != null && unit != selectedUnit)
            {
                return false;
            }

            BattleState state = battleController != null ? battleController.State : null;
            if (unit == null || unit.Side != BattleSide.Player || !PreparationTurnService.CanPlayerPrepare(state))
            {
                return false;
            }

            BeginUnitPress(unitView, screenPosition, pointerId);
            return true;
        }

        private void BeginPendingBoardTap(Vector2 screenPosition, int pointerId)
        {
            modeBeforeCameraGesture = mode;
            mode = InputMode.PendingBoardTap;
            cameraPointerId = pointerId;
            cameraGestureStartScreenPosition = screenPosition;
            cameraGestureLastScreenPosition = screenPosition;
        }

        private void UpdatePendingBoardTap()
        {
            if (TryBeginCameraPinch())
            {
                return;
            }

            Vector2 screenPosition;
            bool isPressed;
            bool isReleased;
            bool isCanceled;
            GetPointerState(
                cameraPointerId,
                cameraGestureLastScreenPosition,
                out screenPosition,
                out isPressed,
                out isReleased,
                out isCanceled);
            cameraGestureLastScreenPosition = screenPosition;

            if (isCanceled || (!isPressed && !isReleased))
            {
                RestoreModeBeforeCameraGesture();
                return;
            }

            if (isReleased)
            {
                int pointerId = cameraPointerId;
                RestoreModeBeforeCameraGesture();
                HandleBoardTap(screenPosition, pointerId);
                return;
            }

            float threshold = cameraDragThresholdPixels * cameraDragThresholdPixels;
            if ((screenPosition - cameraGestureStartScreenPosition).sqrMagnitude <= threshold || battleCameraController == null)
            {
                return;
            }

            mode = InputMode.PanningCamera;
        }

        private void UpdateCameraPan()
        {
            if (TryBeginCameraPinch())
            {
                return;
            }

            Vector2 screenPosition;
            bool isPressed;
            bool isReleased;
            bool isCanceled;
            GetPointerState(
                cameraPointerId,
                cameraGestureLastScreenPosition,
                out screenPosition,
                out isPressed,
                out isReleased,
                out isCanceled);

            if (isCanceled || isReleased || !isPressed)
            {
                RestoreModeBeforeCameraGesture();
                return;
            }

            Vector2 screenDelta = screenPosition - cameraGestureLastScreenPosition;
            cameraGestureLastScreenPosition = screenPosition;
            if (screenDelta.sqrMagnitude > 0f)
            {
                battleCameraController.Pan(screenDelta / GetScreenGestureScale());
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelCameraGesture();
            }
        }

        private bool TryBeginCameraPinch()
        {
            if (battleCameraController == null || cameraPointerId < 0 || Input.touchCount < 2)
            {
                return false;
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId == cameraPointerId
                    || touch.phase == TouchPhase.Ended
                    || touch.phase == TouchPhase.Canceled
                    || IsPointerOverUi(touch.fingerId))
                {
                    continue;
                }

                pinchFirstPointerId = cameraPointerId;
                pinchSecondPointerId = touch.fingerId;
                lastPinchDistance = Vector2.Distance(cameraGestureLastScreenPosition, touch.position);
                mode = InputMode.PinchingCamera;
                return true;
            }

            return false;
        }

        private void UpdateCameraPinch()
        {
            Vector2 firstPosition;
            Vector2 secondPosition;
            bool firstActive;
            bool secondActive;
            bool wasCanceled;
            GetTouchPosition(pinchFirstPointerId, out firstPosition, out firstActive, out wasCanceled);
            bool secondCanceled;
            GetTouchPosition(pinchSecondPointerId, out secondPosition, out secondActive, out secondCanceled);

            if (wasCanceled || secondCanceled || !firstActive || !secondActive)
            {
                RestoreModeBeforeCameraGesture();
                return;
            }

            float currentDistance = Vector2.Distance(firstPosition, secondPosition);
            float normalizedPinchDelta = (currentDistance - lastPinchDistance) / GetScreenGestureScale();
            lastPinchDistance = currentDistance;
            if (!Mathf.Approximately(normalizedPinchDelta, 0f))
            {
                battleCameraController.Zoom(normalizedPinchDelta);
            }
        }

        private void RestoreModeBeforeCameraGesture()
        {
            InputMode restoredMode = modeBeforeCameraGesture;
            ResetCameraGestureState();
            mode = restoredMode;
        }

        private void CancelCameraGesture()
        {
            if (mode == InputMode.PendingBoardTap
                || mode == InputMode.PanningCamera
                || mode == InputMode.PinchingCamera)
            {
                RestoreModeBeforeCameraGesture();
                return;
            }

            ResetCameraGestureState();
        }

        private void ResetCameraGestureState()
        {
            modeBeforeCameraGesture = InputMode.Idle;
            cameraPointerId = -1;
            cameraGestureStartScreenPosition = Vector2.zero;
            cameraGestureLastScreenPosition = Vector2.zero;
            pinchFirstPointerId = -1;
            pinchSecondPointerId = -1;
            lastPinchDistance = 0f;
        }

        private void UpdateMouseWheelZoom()
        {
            if (battleCameraController == null
                || Input.touchCount > 0
                || mode == InputMode.DraggingCard
                || mode == InputMode.PressingUnit
                || mode == InputMode.DraggingUnit
                || IsPointerOverUi(-1))
            {
                return;
            }

            float scrollDelta = Input.mouseScrollDelta.y;
            if (!Mathf.Approximately(scrollDelta, 0f))
            {
                battleCameraController.Zoom(scrollDelta / GetScreenGestureScale());
            }
        }

        private static float GetScreenGestureScale()
        {
            return Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));
        }

        private void HandleBoardTap(Vector2 screenPosition, int pointerId)
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

            if (mode == InputMode.SelectedUnit && selectedUnit != null)
            {
                UnitView selectedUnitView = RaycastForUnit(screenPosition);
                if (selectedUnitView != null && selectedUnitView.Unit == selectedUnit)
                {
                    BeginUnitPress(selectedUnitView, screenPosition, pointerId);
                    return;
                }

                if (TryShowUnitDetails(selectedUnitView))
                {
                    ClearSelection(false);
                    return;
                }

                HexCoord targetCoord;
                HexTileView targetTile;
                if (TryGetFormationTarget(screenPosition, out targetCoord, out targetTile) && battleController != null)
                {
                    battleController.TryMovePlayerUnit(selectedUnit, targetCoord);
                }

                ClearSelection();
                return;
            }

            UnitView unitView = RaycastForUnit(screenPosition);
            if (unitView != null && unitView.Unit != null && unitView.Unit.Side == BattleSide.Player)
            {
                BeginUnitPress(unitView, screenPosition, pointerId);
                return;
            }

            if (TryShowUnitDetails(unitView))
            {
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
                HighlightFormationTiles(state, state.Player, selectedUnit, boardPresenter);
            }
        }

        private void BeginUnitPress(UnitView unitView, Vector2 screenPosition, int pointerId)
        {
            RuntimeUnit unit = unitView != null ? unitView.Unit : null;
            BattleState state = battleController != null ? battleController.State : null;
            if (!PreparationTurnService.CanPlayerPrepare(state) || unit == null || unit.Side != BattleSide.Player)
            {
                ClearSelection();
                return;
            }

            ClearSelection();
            mode = InputMode.PressingUnit;
            pressedUnit = unit;
            pressedUnitView = unitView;
            pressedPointerId = pointerId;
            pressedUnitStartScreenPosition = screenPosition;
            pressedUnitLastScreenPosition = screenPosition;
        }

        private void UpdatePressedUnit()
        {
            Vector2 screenPosition;
            bool isPressed;
            bool isReleased;
            TryGetTrackedPointer(out screenPosition, out isPressed, out isReleased);
            pressedUnitLastScreenPosition = screenPosition;

            if (isReleased)
            {
                if (IsUnitTap(screenPosition))
                {
                    RuntimeUnit unit = pressedUnit;
                    ClearSelection(false);
                    SelectUnit(unit);
                    ShowUnitDetails(unit != null ? unit.Definition : null);
                }
                else
                {
                    BeginUnitDrag(screenPosition);
                    EndUnitDrag(screenPosition);
                }

                return;
            }

            if (!isPressed)
            {
                RestorePressedUnitView();
                ClearSelection();
                return;
            }

            if (!IsUnitTap(screenPosition))
            {
                BeginUnitDrag(screenPosition);
            }
        }

        private void BeginUnitDrag(Vector2 screenPosition)
        {
            if (pressedUnit == null)
            {
                ClearSelection();
                return;
            }

            BattleState state = battleController != null ? battleController.State : null;
            if (!PreparationTurnService.CanPlayerPrepare(state))
            {
                ClearSelection();
                return;
            }

            mode = InputMode.DraggingUnit;
            UpdateUnitDrag(screenPosition);
        }

        private void UpdateUnitDragInput()
        {
            Vector2 screenPosition;
            bool isPressed;
            bool isReleased;
            TryGetTrackedPointer(out screenPosition, out isPressed, out isReleased);
            pressedUnitLastScreenPosition = screenPosition;

            if (isReleased)
            {
                EndUnitDrag(screenPosition);
                return;
            }

            if (!isPressed)
            {
                RestorePressedUnitView();
                ClearSelection();
                return;
            }

            UpdateUnitDrag(screenPosition);
        }

        private void UpdateUnitDrag(Vector2 screenPosition)
        {
            HexCoord targetCoord;
            HexTileView targetTile;
            bool hasTarget = TryGetFormationTarget(screenPosition, out targetCoord, out targetTile);
            bool legal = false;
            BattleState state = battleController != null ? battleController.State : null;
            if (hasTarget)
            {
                legal = IsUnitFormationTargetLegal(state, pressedUnit, targetCoord);
            }

            currentUnitDragCoord = targetCoord;
            currentUnitDragTile = targetTile;
            currentUnitDragHasTarget = hasTarget;
            currentUnitDragTileLegal = legal;

            BoardPresenter boardPresenter = battleController != null ? battleController.BoardPresenter : null;
            if (boardPresenter != null)
            {
                boardPresenter.SetHoverVisualState(
                    targetTile,
                    legal ? PreparationHexVisualState.Legal : PreparationHexVisualState.Blocked);
            }

            MovePressedUnitView(screenPosition);
        }

        private void EndUnitDrag(Vector2 screenPosition)
        {
            UpdateUnitDrag(screenPosition);

            bool moved = false;
            if (currentUnitDragHasTarget && currentUnitDragTileLegal && battleController != null)
            {
                moved = battleController.TryMovePlayerUnit(pressedUnit, currentUnitDragCoord);
            }

            if (!moved)
            {
                RestorePressedUnitView();
            }

            ClearSelection();
        }

        private void MovePressedUnitView(Vector2 screenPosition)
        {
            if (pressedUnitView == null)
            {
                return;
            }

            Vector3 worldPosition;
            if (TryGetBoardWorldPosition(screenPosition, out worldPosition))
            {
                pressedUnitView.SetWorldPosition(worldPosition);
            }
        }

        private void RestorePressedUnitView()
        {
            if (pressedUnit == null || pressedUnitView == null || battleController == null || battleController.BoardPresenter == null)
            {
                return;
            }

            pressedUnitView.SetWorldPosition(battleController.BoardPresenter.GetWorldPosition(pressedUnit.BattleCoord));
        }

        private bool TryGetBoardWorldPosition(Vector2 screenPosition, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (battleCamera == null || battleController == null || battleController.BoardPresenter == null)
            {
                return false;
            }

            Ray ray = battleCamera.ScreenPointToRay(screenPosition);
            Transform boardTransform = battleController.BoardPresenter.transform;
            var boardPlane = new Plane(boardTransform.up, boardTransform.position);
            float enter;
            if (!boardPlane.Raycast(ray, out enter))
            {
                return false;
            }

            worldPosition = ray.GetPoint(enter);
            return true;
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

            bool played = TryPlaySelectedCardAtTile(state, tile);
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
            if (boardPresenter == null)
            {
                return;
            }

            boardPresenter.ClearAllVisualStates();
            if (state == null || state.Player == null || selectedCard == null || selectedCard.Definition == null)
            {
                return;
            }

            if (selectedCard.Definition.CardKind == CardKind.Unit && selectedCard.UnitDefinition != null)
            {
                HighlightUnitPlayableTiles(state, state.Player, selectedCard, boardPresenter);
                return;
            }

            if (selectedCard.Definition.CardKind == CardKind.Spell && selectedCard.SpellDefinition != null)
            {
                HighlightSpellTargetTiles(state, state.Player, selectedCard, boardPresenter);
            }
        }

        private bool TryPlaySelectedCardAtTile(BattleState state, HexTileView tile)
        {
            if (battleController == null || state == null || tile == null || selectedCard == null)
            {
                return false;
            }

            if (selectedCard.UnitDefinition != null && selectedCard.Definition.CardKind == CardKind.Unit)
            {
                return battleController.TryPlayPlayerCard(selectedCard, tile.Coord);
            }

            SpellDefinition spellDefinition = selectedCard.SpellDefinition;
            if (spellDefinition == null || selectedCard.Definition.CardKind != CardKind.Spell)
            {
                return false;
            }

            if (spellDefinition.TargetingKind == SpellTargetingKind.None)
            {
                return battleController.TryPlayPlayerSpell(selectedCard, SpellTarget.None());
            }

            if (spellDefinition.TargetingKind == SpellTargetingKind.FriendlyUnit)
            {
                RuntimeUnit unit;
                if (!SpellTargetingUtility.TryFindFriendlyUnitAtCoord(state.Player, tile.Coord, out unit))
                {
                    return false;
                }

                return battleController.TryPlayPlayerSpell(selectedCard, SpellTarget.ForUnit(unit));
            }

            return false;
        }

        private void PlayDraggedCard()
        {
            if (battleController == null || draggedCard == null || currentDragTile == null)
            {
                return;
            }

            if (draggedCard.UnitDefinition != null && draggedCard.Definition.CardKind == CardKind.Unit)
            {
                battleController.TryPlayPlayerCard(draggedCard, currentDragTile.Coord);
                return;
            }

            SpellDefinition spellDefinition = draggedCard.SpellDefinition;
            if (spellDefinition == null || draggedCard.Definition.CardKind != CardKind.Spell)
            {
                return;
            }

            if (spellDefinition.TargetingKind == SpellTargetingKind.None)
            {
                battleController.TryPlayPlayerSpell(draggedCard, SpellTarget.None());
                return;
            }

            if (spellDefinition.TargetingKind == SpellTargetingKind.FriendlyUnit && currentDragSpellTargetUnit != null)
            {
                battleController.TryPlayPlayerSpell(draggedCard, SpellTarget.ForUnit(currentDragSpellTargetUnit));
            }
        }

        private static bool IsCardDragTargetLegal(BattleState state, CardRuntimeState card, HexTileView tile, out RuntimeUnit spellTargetUnit)
        {
            spellTargetUnit = null;
            if (state == null || state.Player == null || card == null || tile == null)
            {
                return false;
            }

            if (card.UnitDefinition != null && card.Definition.CardKind == CardKind.Unit)
            {
                return UnitPlayService.ValidatePlay(state, state.Player, card, tile.Coord) == PlayUnitFailReason.None;
            }

            SpellDefinition spellDefinition = card.SpellDefinition;
            if (spellDefinition == null || card.Definition.CardKind != CardKind.Spell)
            {
                return false;
            }

            if (spellDefinition.TargetingKind == SpellTargetingKind.None)
            {
                return SpellPlayService.ValidatePlay(state, state.Player, card, SpellTarget.None()) == PlaySpellFailReason.None;
            }

            if (spellDefinition.TargetingKind == SpellTargetingKind.FriendlyUnit)
            {
                if (!SpellTargetingUtility.TryFindFriendlyUnitAtCoord(state.Player, tile.Coord, out spellTargetUnit))
                {
                    return false;
                }

                return SpellPlayService.ValidatePlay(state, state.Player, card, SpellTarget.ForUnit(spellTargetUnit)) == PlaySpellFailReason.None;
            }

            return false;
        }

        private bool TryGetFormationTarget(Vector2 screenPosition, out HexCoord targetCoord, out HexTileView targetTile)
        {
            targetCoord = default(HexCoord);
            targetTile = null;

            int count = Raycast(screenPosition);
            for (int i = 0; i < count; i++)
            {
                UnitView unitView = raycastHits[i].collider.GetComponentInParent<UnitView>();
                if (unitView != null && unitView.Unit != null && unitView.Unit != pressedUnit && unitView.Unit.Side == BattleSide.Player)
                {
                    targetCoord = unitView.Unit.FormationCoord;
                    BoardPresenter boardPresenter = battleController != null ? battleController.BoardPresenter : null;
                    targetTile = boardPresenter != null ? boardPresenter.GetTileView(targetCoord) : null;
                    return true;
                }

                if (targetTile == null)
                {
                    targetTile = raycastHits[i].collider.GetComponentInParent<HexTileView>();
                }
            }

            if (targetTile == null)
            {
                return false;
            }

            targetCoord = targetTile.Coord;
            return true;
        }

        private static bool IsUnitFormationTargetLegal(BattleState state, RuntimeUnit unit, HexCoord targetCoord)
        {
            PlayerBattleState player = state != null ? state.Player : null;
            return PreparationTurnService.CanPlayerPrepare(state)
                && player != null
                && unit != null
                && unit.Side == player.Side
                && player.Units.Contains(unit)
                && state.Board != null
                && state.Board.IsDeploymentCoord(player.Side, targetCoord);
        }

        private static bool CanPreparePlayableCard(BattleState state, CardRuntimeState card)
        {
            PlayerBattleState player = state != null ? state.Player : null;
            return PreparationTurnService.CanPlayerPrepare(state)
                && card != null
                && card.Definition != null
                && HandService.IsInHand(player, card)
                && ((card.Definition.CardKind == CardKind.Unit && card.UnitDefinition != null)
                    || (card.Definition.CardKind == CardKind.Spell && card.SpellDefinition != null));
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

            if ((mode == InputMode.SelectedUnit || mode == InputMode.PressingUnit || mode == InputMode.DraggingUnit)
                && !PreparationTurnService.CanPlayerPrepare(state))
            {
                RestorePressedUnitView();
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

            RestorePressedUnitView();
            RestoreDraggedCardView();

            mode = InputMode.Idle;
            draggedCard = null;
            draggedCardView = null;
            currentDragTile = null;
            currentDragSpellTargetUnit = null;
            currentDragTileLegal = false;
            selectedCard = null;
            selectedUnit = null;
            pressedUnit = null;
            pressedUnitView = null;
            pressedPointerId = -1;
            pressedUnitStartScreenPosition = Vector2.zero;
            pressedUnitLastScreenPosition = Vector2.zero;
            currentUnitDragCoord = default(HexCoord);
            currentUnitDragTile = null;
            currentUnitDragHasTarget = false;
            currentUnitDragTileLegal = false;
            ResetCameraGestureState();

            if (uiController != null)
            {
                uiController.SetSelectedCard(null);
            }

            BoardPresenter boardPresenter = battleController != null ? battleController.BoardPresenter : null;
            if (boardPresenter != null)
            {
                boardPresenter.ClearAllVisualStates();
            }
        }

        private static void HighlightFormationTiles(
            BattleState state,
            PlayerBattleState player,
            RuntimeUnit unit,
            BoardPresenter boardPresenter)
        {
            boardPresenter.ClearAllVisualStates();
            if (state == null || player == null || unit == null || state.Board == null)
            {
                return;
            }

            HexBoard board = state.Board;
            for (int r = 0; r < board.Height; r++)
            {
                for (int q = 0; q < board.Width; q++)
                {
                    HexCoord coord = new HexCoord(q, r);
                    if (board.IsDeploymentCoord(player.Side, coord))
                    {
                        boardPresenter.SetTileVisualState(coord, PreparationHexVisualState.Legal);
                    }
                }
            }

            boardPresenter.SetTileVisualState(unit.FormationCoord, PreparationHexVisualState.Selected);
        }

        private static void HighlightUnitPlayableTiles(
            BattleState state,
            PlayerBattleState player,
            CardRuntimeState card,
            BoardPresenter boardPresenter)
        {
            HexBoard board = state.Board;
            if (board == null)
            {
                return;
            }

            for (int r = 0; r < board.Height; r++)
            {
                for (int q = 0; q < board.Width; q++)
                {
                    HexCoord coord = new HexCoord(q, r);
                    if (UnitPlayService.ValidatePlay(state, player, card, coord) == PlayUnitFailReason.None)
                    {
                        boardPresenter.SetTileVisualState(coord, PreparationHexVisualState.Legal);
                    }
                }
            }
        }

        private static void HighlightSpellTargetTiles(
            BattleState state,
            PlayerBattleState player,
            CardRuntimeState card,
            BoardPresenter boardPresenter)
        {
            SpellDefinition spellDefinition = card.SpellDefinition;
            if (spellDefinition == null || spellDefinition.TargetingKind != SpellTargetingKind.FriendlyUnit)
            {
                return;
            }

            for (int i = 0; i < player.Units.Count; i++)
            {
                RuntimeUnit unit = player.Units[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                if (SpellPlayService.ValidatePlay(state, player, card, SpellTarget.ForUnit(unit)) == PlaySpellFailReason.None)
                {
                    boardPresenter.SetTileVisualState(unit.BattleCoord, PreparationHexVisualState.Legal);
                }
            }
        }

        private void RestoreDraggedCardView()
        {
            if (draggedCardView == null)
            {
                return;
            }

            CardView cardView = draggedCardView;
            draggedCardView = null;
            if (uiController != null)
            {
                uiController.EndCardDragVisual(cardView);
            }
        }

        private void HideShownCardDetails()
        {
            if (uiController != null)
            {
                uiController.HideCardDetails();
            }
        }

        private bool TryShowUnitDetails(UnitView unitView)
        {
            if (unitView == null)
            {
                return false;
            }

            RuntimeUnit preparationUnit = unitView.Unit;
            if (preparationUnit != null && preparationUnit.IsAlive)
            {
                ShowUnitDetails(preparationUnit.Definition);
                return true;
            }

            return false;
        }

        private void ShowUnitDetails(UnitDefinition unitDefinition)
        {
            if (uiController != null && unitDefinition != null)
            {
                uiController.ShowUnitDetails(unitDefinition);
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
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                {
                    continue;
                }

                screenPosition = touch.position;
                pointerId = touch.fingerId;
                return true;
            }

            screenPosition = Input.mousePosition;
            pointerId = -1;
            return Input.GetMouseButtonDown(0);
        }

        private static void GetPointerState(
            int pointerId,
            Vector2 fallbackPosition,
            out Vector2 screenPosition,
            out bool isPressed,
            out bool isReleased,
            out bool isCanceled)
        {
            if (pointerId >= 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.fingerId != pointerId)
                    {
                        continue;
                    }

                    screenPosition = touch.position;
                    isCanceled = touch.phase == TouchPhase.Canceled;
                    isReleased = touch.phase == TouchPhase.Ended;
                    isPressed = !isCanceled && !isReleased;
                    return;
                }

                screenPosition = fallbackPosition;
                isPressed = false;
                isReleased = false;
                isCanceled = true;
                return;
            }

            screenPosition = Input.mousePosition;
            isReleased = Input.GetMouseButtonUp(0);
            isPressed = Input.GetMouseButton(0);
            isCanceled = !isPressed && !isReleased;
        }

        private static void GetTouchPosition(int pointerId, out Vector2 screenPosition, out bool isActive, out bool isCanceled)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId != pointerId)
                {
                    continue;
                }

                screenPosition = touch.position;
                isCanceled = touch.phase == TouchPhase.Canceled;
                isActive = touch.phase != TouchPhase.Ended && !isCanceled;
                return;
            }

            screenPosition = Vector2.zero;
            isActive = false;
            isCanceled = true;
        }

        private bool TryGetTrackedPointer(out Vector2 screenPosition, out bool isPressed, out bool isReleased)
        {
            if (pressedPointerId >= 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.fingerId != pressedPointerId)
                    {
                        continue;
                    }

                    screenPosition = touch.position;
                    isReleased = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                    isPressed = !isReleased;
                    return true;
                }

                screenPosition = pressedUnitLastScreenPosition;
                isPressed = false;
                isReleased = true;
                return true;
            }

            screenPosition = Input.mousePosition;
            isReleased = Input.GetMouseButtonUp(0);
            isPressed = Input.GetMouseButton(0);
            return isPressed || isReleased;
        }

        private bool IsUnitTap(Vector2 screenPosition)
        {
            float threshold = unitDragThresholdPixels * unitDragThresholdPixels;
            return (screenPosition - pressedUnitStartScreenPosition).sqrMagnitude <= threshold;
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
