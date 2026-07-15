using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeckBattle
{
    public sealed class BattleUIController : MonoBehaviour
    {
        [Header("Battle")]
        [SerializeField] private BattleController battleController;
        [SerializeField] private BattleInputController inputController;

        [Header("Hud")]
        [SerializeField] private TextMeshProUGUI playerHpText;
        [SerializeField] private TextMeshProUGUI enemyHpText;
        [SerializeField] private TextMeshProUGUI apText;
        [SerializeField] private TextMeshProUGUI roundText;
        [SerializeField] private TextMeshProUGUI slotsText;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private Button readyButton;

        [Header("Hand")]
        [SerializeField] private RectTransform handRoot;
        [SerializeField] private CardView cardViewPrefab;

        [Header("Drag Ghost")]
        [SerializeField] private RectTransform cardGhostRoot;
        [SerializeField] private TextMeshProUGUI ghostNameText;
        [SerializeField] private TextMeshProUGUI ghostCostText;
        [SerializeField] private CanvasGroup ghostCanvasGroup;

        private readonly List<CardView> cardViews = new List<CardView>(8);
        private readonly List<CardRuntimeState> shownHand = new List<CardRuntimeState>(8);

        private int shownPlayerHp = int.MinValue;
        private int shownEnemyHp = int.MinValue;
        private int shownAp = int.MinValue;
        private int shownRound = int.MinValue;
        private int shownUnits = int.MinValue;
        private int shownSlots = int.MinValue;
        private BattlePhase shownPhase = BattlePhase.None;

        private void Awake()
        {
            if (readyButton != null)
            {
                readyButton.onClick.AddListener(HandleReadyClicked);
            }

            HideCardGhost();
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
            RefreshHand(state.Player.Hand);
        }

        public void ShowCardGhost(CardRuntimeState card, Vector2 screenPosition)
        {
            if (cardGhostRoot == null)
            {
                return;
            }

            UnitDefinition definition = card != null ? card.Definition : null;
            if (ghostNameText != null)
            {
                ghostNameText.text = definition != null ? definition.DisplayName : string.Empty;
            }

            if (ghostCostText != null)
            {
                ghostCostText.text = definition != null ? definition.ApCost.ToString() : string.Empty;
            }

            if (ghostCanvasGroup != null)
            {
                ghostCanvasGroup.alpha = 0.86f;
                ghostCanvasGroup.blocksRaycasts = false;
            }

            cardGhostRoot.gameObject.SetActive(true);
            MoveCardGhost(screenPosition);
        }

        public void MoveCardGhost(Vector2 screenPosition)
        {
            if (cardGhostRoot == null)
            {
                return;
            }

            cardGhostRoot.position = screenPosition;
        }

        public void HideCardGhost()
        {
            if (cardGhostRoot != null)
            {
                cardGhostRoot.gameObject.SetActive(false);
            }
        }

        private void RefreshHud(BattleState state)
        {
            PlayerBattleState player = state.Player;
            PlayerBattleState enemy = state.Enemy;

            SetTextIfChanged(playerHpText, ref shownPlayerHp, player.Hp, "HP ");
            SetTextIfChanged(enemyHpText, ref shownEnemyHp, enemy.Hp, "AI ");
            SetTextIfChanged(apText, ref shownAp, player.Ap, "AP ");
            SetTextIfChanged(roundText, ref shownRound, state.RoundNumber, "Runda ");

            int units = player.Units.Count;
            int slots = player.DeploymentSlots;
            if (slotsText != null && (shownUnits != units || shownSlots != slots))
            {
                shownUnits = units;
                shownSlots = slots;
                slotsText.text = "Sloty " + units + "/" + slots;
            }

            if (phaseText != null && shownPhase != state.Phase)
            {
                shownPhase = state.Phase;
                phaseText.text = state.Phase.ToString();
            }

            if (readyButton != null)
            {
                readyButton.interactable = state.Phase == BattlePhase.Preparation;
            }
        }

        private void RefreshHand(List<CardRuntimeState> hand)
        {
            if (handRoot == null || cardViewPrefab == null || IsSameHand(hand))
            {
                return;
            }

            EnsureCardViewCount(hand.Count);
            shownHand.Clear();

            for (int i = 0; i < cardViews.Count; i++)
            {
                bool active = i < hand.Count;
                cardViews[i].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                CardRuntimeState card = hand[i];
                shownHand.Add(card);
                cardViews[i].Bind(card, inputController);
            }
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

        private void SetTextIfChanged(TextMeshProUGUI text, ref int cachedValue, int value, string prefix)
        {
            if (text == null || cachedValue == value)
            {
                return;
            }

            cachedValue = value;
            text.text = prefix + value;
        }

        private void HandleReadyClicked()
        {
            if (battleController != null)
            {
                battleController.ConfirmReady();
            }
        }
    }
}
