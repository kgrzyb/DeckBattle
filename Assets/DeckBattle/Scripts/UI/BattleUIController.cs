using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DeckBattle
{
    public sealed class BattleUIController : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";

        [Header("Battle")]
        [SerializeField] private BattleController battleController;
        [SerializeField] private BattleInputController inputController;

        [Header("Hud")]
        [SerializeField] private TextMeshProUGUI playerHpText;
        [SerializeField] private TextMeshProUGUI enemyHpText;
        [SerializeField] private TextMeshProUGUI apText;
        [SerializeField] private TextMeshProUGUI roundText;
        [FormerlySerializedAs("slotsText")]
        [SerializeField] private TextMeshProUGUI unitLimitText;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private Button readyButton;
        [SerializeField] private BattleCombatRunner combatRunner;
        [SerializeField] private GameObject roundTimer;
        [SerializeField] private RectTransform roundTimerProgressBar;

        [Header("Result")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private Button mainMenuButton;

        [Header("Hand")]
        [SerializeField] private RectTransform handRoot;
        [SerializeField] private CardView cardViewPrefab;
        [SerializeField] private float handSidePadding = 18f;
        [SerializeField] private float preferredHandSpacing = 14f;
        [SerializeField] private float selectedCardLift = 22f;

        [Header("Card Details")]
        [SerializeField] private CardDetailsPopupView cardDetailsPopup;

        [Header("Card Drag")]
        [SerializeField] private float dragHexOffsetUi = 72f;

        private readonly List<CardView> cardViews = new List<CardView>(8);
        private readonly List<CardRuntimeState> shownHand = new List<CardRuntimeState>(8);
        private CardRuntimeState selectedCard;
        private Canvas uiCanvas;
        private Vector3 roundTimerProgressBarBaseScale = Vector3.one;

        private int shownPlayerHp = int.MinValue;
        private int shownEnemyHp = int.MinValue;
        private int shownAp = int.MinValue;
        private int shownMaxAp = int.MinValue;
        private int shownRound = int.MinValue;
        private int shownUnits = int.MinValue;
        private int shownMaxUnits = int.MinValue;
        private BattlePhase shownPhase = BattlePhase.None;
        private BattleSide shownActivePreparationSide = (BattleSide)(-1);
        private bool shownPlayerReady;
        private bool shownEnemyReady;
        private bool shownResultPanelActive;
        private string shownResultText;
        private bool isRoundTimerVisible;
        private bool shouldTrackRoundTimer;

        private void Awake()
        {
            uiCanvas = GetComponent<Canvas>();

            if (readyButton != null)
            {
                readyButton.onClick.AddListener(HandleReadyClicked);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(HandleMainMenuClicked);
            }

            if (roundTimerProgressBar != null)
            {
                roundTimerProgressBarBaseScale = roundTimerProgressBar.localScale;
            }

            SetRoundTimerVisible(false);
            HideCardDetails();
        }

        private void OnValidate()
        {
            handSidePadding = Mathf.Max(0f, handSidePadding);
            preferredHandSpacing = Mathf.Max(0f, preferredHandSpacing);
            selectedCardLift = Mathf.Max(0f, selectedCardLift);
            dragHexOffsetUi = Mathf.Max(0f, dragHexOffsetUi);
        }

        private void OnRectTransformDimensionsChange()
        {
            LayoutHand();
        }

        private void Update()
        {
            if (shouldTrackRoundTimer)
            {
                RefreshRoundTimer();
            }
        }

        private void OnEnable()
        {
            if (battleController != null)
            {
                battleController.StateChanged += Refresh;
            }
        }

        private void Start()
        {
            Refresh();
        }

        private void OnDisable()
        {
            if (battleController != null)
            {
                battleController.StateChanged -= Refresh;
            }
        }

        public void Refresh()
        {
            BattleState state = battleController != null ? battleController.State : null;
            if (state == null)
            {
                return;
            }

            RefreshHud(state);
            RefreshResult(state);
            RefreshHand(state.Player.Hand);
        }

        public void BeginCardDragVisual(CardView cardView, Vector2 screenPosition)
        {
            if (cardView == null)
            {
                return;
            }

            cardView.transform.SetParent(transform, true);
            cardView.transform.SetAsLastSibling();
            MoveCardDragVisual(cardView, screenPosition);
        }

        public void MoveCardDragVisual(CardView cardView, Vector2 screenPosition)
        {
            if (cardView == null)
            {
                return;
            }

            cardView.transform.position = screenPosition;
        }

        public void EndCardDragVisual(CardView cardView)
        {
            if (cardView == null || handRoot == null)
            {
                return;
            }

            cardView.transform.SetParent(handRoot, false);
            LayoutHand();
        }

        public Vector2 GetCardDragHexScreenPosition(Vector2 screenPosition)
        {
            float scaleFactor = uiCanvas != null ? uiCanvas.scaleFactor : 1f;
            return screenPosition + Vector2.up * dragHexOffsetUi * scaleFactor;
        }

        public void ShowCardDetails(CardRuntimeState card)
        {
            if (cardDetailsPopup != null)
            {
                cardDetailsPopup.Show(card);
            }
        }

        public void ShowUnitDetails(UnitDefinition unitDefinition)
        {
            if (cardDetailsPopup != null)
            {
                cardDetailsPopup.Show(unitDefinition);
            }
        }

        public void HideCardDetails()
        {
            if (cardDetailsPopup != null)
            {
                cardDetailsPopup.Hide();
            }
        }

        public void SetSelectedCard(CardRuntimeState card)
        {
            selectedCard = card;
            for (int i = 0; i < cardViews.Count; i++)
            {
                CardView view = cardViews[i];
                if (view != null)
                {
                    view.SetSelected(card != null && view.Card == card);
                }
            }

            LayoutHand();
        }

        private void RefreshHud(BattleState state)
        {
            PlayerBattleState player = state.Player;
            PlayerBattleState enemy = state.Enemy;

            SetTextIfChanged(playerHpText, ref shownPlayerHp, player.Hp, string.Empty);
            SetTextIfChanged(enemyHpText, ref shownEnemyHp, enemy.Hp, string.Empty);
            SetApTextIfChanged(player.Ap, state.CurrentRoundAp);
            SetTextIfChanged(roundText, ref shownRound, state.RoundNumber, "Runda ");

            int units = player.Units.Count;
            int maxUnits = state.Config.MaxUnitsPerSide;
            if (unitLimitText != null && (shownUnits != units || shownMaxUnits != maxUnits))
            {
                shownUnits = units;
                shownMaxUnits = maxUnits;
                unitLimitText.text = "Jednostki " + units + "/" + maxUnits;
            }

            if (phaseText != null
                && (shownPhase != state.Phase
                    || shownActivePreparationSide != state.ActivePreparationSide
                    || shownPlayerReady != state.Player.IsReady
                    || shownEnemyReady != state.Enemy.IsReady))
            {
                shownPhase = state.Phase;
                shownActivePreparationSide = state.ActivePreparationSide;
                shownPlayerReady = state.Player.IsReady;
                shownEnemyReady = state.Enemy.IsReady;
                if (state.Phase == BattlePhase.Preparation && state.Player.IsReady)
                {
                    phaseText.text = "Gotowy - oczekiwanie na przeciwnika";
                }
                else if (state.Phase == BattlePhase.Preparation && state.ActivePreparationSide == BattleSide.Player)
                {
                    phaseText.text = "Twoje przygotowanie";
                }
                else if (state.Phase == BattlePhase.Preparation)
                {
                    phaseText.text = "Przeciwnik sie przygotowuje";
                }
                else
                {
                    phaseText.text = state.Phase.ToString();
                }
            }

            if (readyButton != null)
            {
                readyButton.interactable = PreparationTurnService.CanPlayerPrepare(state);
            }

            shouldTrackRoundTimer = state.Phase == BattlePhase.Combat;
            if (!shouldTrackRoundTimer)
            {
                SetRoundTimerVisible(false);
            }
        }

        private void RefreshRoundTimer()
        {
            if (combatRunner == null
                || !combatRunner.IsRunning
                || !combatRunner.IsCombatAccelerationEnabled
                || combatRunner.CurrentCombatSpeed > 1f)
            {
                SetRoundTimerVisible(false);
                return;
            }

            float normalizedElapsed = Mathf.Clamp01(combatRunner.CombatElapsedTime / combatRunner.CombatAccelerationDelay);
            SetRoundTimerProgress(1f - normalizedElapsed);
            SetRoundTimerVisible(true);
        }

        private void SetRoundTimerVisible(bool visible)
        {
            if (roundTimer == null)
            {
                return;
            }

            if (isRoundTimerVisible == visible && roundTimer.activeSelf == visible)
            {
                return;
            }

            isRoundTimerVisible = visible;
            roundTimer.SetActive(visible);
        }

        private void SetRoundTimerProgress(float normalizedProgress)
        {
            if (roundTimerProgressBar == null)
            {
                return;
            }

            float progress = Mathf.Clamp01(normalizedProgress);
            roundTimerProgressBar.localScale = new Vector3(
                roundTimerProgressBarBaseScale.x * progress,
                roundTimerProgressBarBaseScale.y,
                roundTimerProgressBarBaseScale.z);
        }

        private void RefreshResult(BattleState state)
        {
            bool showResult = state.Phase == BattlePhase.MatchEnd;
            SetResultPanelActive(showResult);
            if (!showResult)
            {
                SetResultText(string.Empty);
                return;
            }

            RoundResolutionResult result = battleController != null ? battleController.LastRoundResolutionResult : null;
            if (result == null || !result.HasWinner)
            {
                SetResultText("Draw");
                return;
            }

            SetResultText(result.Winner == BattleSide.Player ? "Victory" : "Defeat");
        }

        private void SetResultPanelActive(bool active)
        {
            if (resultPanel == null || shownResultPanelActive == active)
            {
                return;
            }

            shownResultPanelActive = active;
            resultPanel.SetActive(active);
        }

        private void SetResultText(string value)
        {
            if (resultText == null || shownResultText == value)
            {
                return;
            }

            shownResultText = value;
            resultText.text = value;
        }

        private void RefreshHand(List<CardRuntimeState> hand)
        {
            if (handRoot == null || cardViewPrefab == null || IsSameHand(hand))
            {
                return;
            }

            HideCardDetailsIfMissingFromHand(hand);
            ClearSelectedCardIfMissingFromHand(hand);

            EnsureCardViewCount(hand.Count);
            shownHand.Clear();

            for (int i = 0; i < cardViews.Count; i++)
            {
                bool active = i < hand.Count;
                cardViews[i].gameObject.SetActive(active);
                if (!active)
                {
                    cardViews[i].SetSelected(false);
                    continue;
                }

                CardRuntimeState card = hand[i];
                shownHand.Add(card);
                cardViews[i].Bind(card, inputController);
                cardViews[i].SetSelected(card == selectedCard);
            }

            LayoutHand();
        }

        private void HideCardDetailsIfMissingFromHand(List<CardRuntimeState> hand)
        {
            if (cardDetailsPopup == null || !cardDetailsPopup.IsShowingCardDetails)
            {
                return;
            }

            for (int i = 0; i < hand.Count; i++)
            {
                if (cardDetailsPopup.IsShownFor(hand[i]))
                {
                    return;
                }
            }

            HideCardDetails();
        }

        private void ClearSelectedCardIfMissingFromHand(List<CardRuntimeState> hand)
        {
            if (selectedCard == null || ContainsCard(hand, selectedCard))
            {
                return;
            }

            SetSelectedCard(null);
        }

        private static bool ContainsCard(List<CardRuntimeState> hand, CardRuntimeState card)
        {
            if (hand == null)
            {
                return false;
            }

            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] == card)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSameHand(List<CardRuntimeState> hand)
        {
            if (hand == null || shownHand.Count != hand.Count)
            {
                return false;
            }

            for (int i = 0; i < hand.Count; i++)
            {
                if (shownHand[i] != hand[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void EnsureCardViewCount(int count)
        {
            while (cardViews.Count < count)
            {
                CardView view = Instantiate(cardViewPrefab, handRoot);
                cardViews.Add(view);
            }
        }

        private void LayoutHand()
        {
            if (handRoot == null)
            {
                return;
            }

            int activeCount = 0;
            float cardWidth = 0f;
            for (int i = 0; i < cardViews.Count; i++)
            {
                CardView view = cardViews[i];
                if (view == null || !view.gameObject.activeSelf || view.transform.parent != handRoot)
                {
                    continue;
                }

                if (activeCount == 0)
                {
                    RectTransform cardRect = view.transform as RectTransform;
                    if (cardRect != null)
                    {
                        cardWidth = cardRect.rect.width;
                    }
                }

                activeCount++;
            }

            if (activeCount == 0 || cardWidth <= 0f)
            {
                return;
            }

            float availableWidth = Mathf.Max(0f, handRoot.rect.width - handSidePadding * 2f);
            float step = 0f;
            if (activeCount > 1)
            {
                float maximumStep = Mathf.Max(0f, (availableWidth - cardWidth) / (activeCount - 1));
                step = Mathf.Min(cardWidth + preferredHandSpacing, maximumStep);
            }

            float span = cardWidth + step * (activeCount - 1);
            float firstCardX = -span * 0.5f + cardWidth * 0.5f;
            int layoutIndex = 0;
            CardView selectedView = null;

            for (int i = 0; i < cardViews.Count; i++)
            {
                CardView view = cardViews[i];
                if (view == null || !view.gameObject.activeSelf || view.transform.parent != handRoot)
                {
                    continue;
                }

                RectTransform cardRect = view.transform as RectTransform;
                if (cardRect == null)
                {
                    continue;
                }

                bool isSelected = selectedCard != null && view.Card == selectedCard;
                cardRect.anchoredPosition = new Vector2(firstCardX + step * layoutIndex, isSelected ? selectedCardLift : 0f);
                view.transform.SetSiblingIndex(layoutIndex);
                if (isSelected)
                {
                    selectedView = view;
                }

                layoutIndex++;
            }

            if (selectedView != null)
            {
                selectedView.transform.SetAsLastSibling();
            }
        }

        private void SetTextIfChanged(TextMeshProUGUI text, ref int cachedValue, int value, string prefix)
        {
            if (text == null || cachedValue == value)
            {
                return;
            }

            cachedValue = value;
            text.text = prefix + value;
        }

        private void SetApTextIfChanged(int currentAp, int maxAp)
        {
            if (apText == null || (shownAp == currentAp && shownMaxAp == maxAp))
            {
                return;
            }

            shownAp = currentAp;
            shownMaxAp = maxAp;
            apText.SetText("{0}/{1}", currentAp, maxAp);
        }

        private void HandleReadyClicked()
        {
            if (battleController != null)
            {
                battleController.ConfirmReady();
            }
        }

        private void HandleMainMenuClicked()
        {
            SceneManager.LoadScene(MainMenuSceneName);
        }
    }
}
