using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeckBattle
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string battleSceneName = "Battle";

        [Header("Battle Start")]
        [SerializeField] private CardCatalog catalog;
        [SerializeField] private int minDeckSize = 1;
        [SerializeField] private int maxDeckSize = 8;
        [SerializeField] private bool randomizeBattleSeed = true;
        [SerializeField] private int battleSeed = 12345;

        [Header("Buttons")]
        [SerializeField] private Button startBattleButton;
        [SerializeField] private Button collectionButton;
        [SerializeField] private Button deckButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button resetSaveButton;

        [Header("Views")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject placeholderPanel;
        [SerializeField] private GameObject deckBuilderPanel;
        [SerializeField] private DeckBuilderController deckBuilderController;
        [SerializeField] private TMP_Text placeholderTitleText;
        [SerializeField] private TMP_Text placeholderBodyText;
        [SerializeField] private Button deckBuilderBackButton;

        private readonly BattleStartDataBuilder battleStartDataBuilder = new BattleStartDataBuilder();
        private PlayerProfileStore profileStore;
        private TMP_Text resetSaveButtonLabel;
        private bool resetConfirmationPending;

        private void Awake()
        {
            CreateResetSaveButtonIfNeeded();
            ShowMenu();
        }

        private void OnEnable()
        {
            if (startBattleButton != null)
            {
                startBattleButton.onClick.AddListener(StartBattle);
            }

            if (collectionButton != null)
            {
                collectionButton.onClick.AddListener(ShowCollection);
            }

            if (deckButton != null)
            {
                deckButton.onClick.AddListener(ShowDeck);
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(ShowMenu);
            }

            if (resetSaveButton != null)
            {
                resetSaveButton.onClick.AddListener(HandleResetSaveClicked);
            }

            if (deckBuilderBackButton != null)
            {
                deckBuilderBackButton.onClick.AddListener(ShowMenuFromDeckBuilder);
            }
        }

        private void OnDisable()
        {
            if (startBattleButton != null)
            {
                startBattleButton.onClick.RemoveListener(StartBattle);
            }

            if (collectionButton != null)
            {
                collectionButton.onClick.RemoveListener(ShowCollection);
            }

            if (deckButton != null)
            {
                deckButton.onClick.RemoveListener(ShowDeck);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(ShowMenu);
            }

            if (resetSaveButton != null)
            {
                resetSaveButton.onClick.RemoveListener(HandleResetSaveClicked);
            }

            if (deckBuilderBackButton != null)
            {
                deckBuilderBackButton.onClick.RemoveListener(ShowMenuFromDeckBuilder);
            }
        }

        public void StartBattle()
        {
            if (string.IsNullOrWhiteSpace(battleSceneName))
            {
                Debug.LogError("MainMenuController requires a battle scene name.", this);
                return;
            }

            PrepareBattleSession();
            SceneManager.LoadScene(battleSceneName);
        }

        public void ShowMenu()
        {
            SetPanelActive(menuPanel, true);
            SetPanelActive(placeholderPanel, false);
            SetPanelActive(deckBuilderPanel, false);
        }

        public void ShowCollection()
        {
            ShowPlaceholder("Collection", "Card collection placeholder");
        }

        public void ShowDeck()
        {
            SetPanelActive(menuPanel, false);
            SetPanelActive(placeholderPanel, false);
            SetPanelActive(deckBuilderPanel, true);

            if (deckBuilderController != null)
            {
                deckBuilderController.Show();
            }
        }

        private void ShowPlaceholder(string title, string body)
        {
            if (placeholderTitleText != null)
            {
                placeholderTitleText.text = title;
            }

            if (placeholderBodyText != null)
            {
                placeholderBodyText.text = body;
            }

            SetPanelActive(menuPanel, false);
            SetPanelActive(placeholderPanel, true);
            SetPanelActive(deckBuilderPanel, false);
        }

        private void ShowMenuFromDeckBuilder()
        {
            if (deckBuilderController != null && !deckBuilderController.TrySaveBeforeClose())
            {
                return;
            }

            ShowMenu();
        }

        private void CreateResetSaveButtonIfNeeded()
        {
            if (resetSaveButton != null || deckButton == null || menuPanel == null)
            {
                return;
            }

            resetSaveButton = Instantiate(deckButton, menuPanel.transform);
            resetSaveButton.name = "ResetSaveButton";

            RectTransform buttonTransform = resetSaveButton.transform as RectTransform;
            if (buttonTransform != null)
            {
                buttonTransform.anchorMin = new Vector2(0.5f, 0.15f);
                buttonTransform.anchorMax = new Vector2(0.5f, 0.15f);
                buttonTransform.anchoredPosition = Vector2.zero;
            }

            Image buttonImage = resetSaveButton.targetGraphic as Image;
            if (buttonImage != null)
            {
                buttonImage.color = new Color(0.55f, 0.2f, 0.2f, 1f);
            }

            resetSaveButtonLabel = resetSaveButton.GetComponentInChildren<TMP_Text>();
            SetResetSaveButtonLabel("Reset Save");
        }

        private void HandleResetSaveClicked()
        {
            if (!resetConfirmationPending)
            {
                resetConfirmationPending = true;
                SetResetSaveButtonLabel("Tap Again to Reset");
                CancelInvoke(nameof(CancelResetConfirmation));
                Invoke(nameof(CancelResetConfirmation), 3f);
                return;
            }

            CancelInvoke(nameof(CancelResetConfirmation));
            resetConfirmationPending = false;

            if (profileStore == null)
            {
                profileStore = new PlayerProfileStore();
            }

            profileStore.ResetProfile();
            BattleSession.Clear();
            if (deckBuilderController != null)
            {
                deckBuilderController.ResetSavedProfile();
            }

            SetResetSaveButtonLabel("Save Reset");
            Invoke(nameof(RestoreResetSaveButtonLabel), 2f);
        }

        private void CancelResetConfirmation()
        {
            resetConfirmationPending = false;
            SetResetSaveButtonLabel("Reset Save");
        }

        private void RestoreResetSaveButtonLabel()
        {
            SetResetSaveButtonLabel("Reset Save");
        }

        private void SetResetSaveButtonLabel(string value)
        {
            if (resetSaveButtonLabel == null && resetSaveButton != null)
            {
                resetSaveButtonLabel = resetSaveButton.GetComponentInChildren<TMP_Text>();
            }

            if (resetSaveButtonLabel != null)
            {
                resetSaveButtonLabel.text = value;
            }
        }

        private void PrepareBattleSession()
        {
            BattleSession.Clear();
            if (catalog == null)
            {
                Debug.LogWarning("MainMenuController has no CardCatalog. Battle will use scene fallback decks.", this);
                return;
            }

            if (profileStore == null)
            {
                profileStore = new PlayerProfileStore();
            }

            PlayerProfile profile = profileStore.LoadOrCreateDefault(catalog);
            if (PlayerProfileStore.Validate(profile, catalog))
            {
                profileStore.Save(profile);
            }

            var rules = new BattleStartRules(minDeckSize, maxDeckSize);
            BattleStartData startData = battleStartDataBuilder.Build(profile, catalog, rules, ResolveBattleSeed());
            BattleSession.PendingStartData = startData;
        }

        private int ResolveBattleSeed()
        {
            return randomizeBattleSeed && Application.isPlaying ? GeneratePlaySeed() : battleSeed;
        }

        private int GeneratePlaySeed()
        {
            unchecked
            {
                long ticks = DateTime.UtcNow.Ticks;
                int generatedSeed = (int)ticks;
                generatedSeed = (generatedSeed * 397) ^ (int)(ticks >> 32);
                generatedSeed = (generatedSeed * 397) ^ GetInstanceID();
                generatedSeed = (generatedSeed * 397) ^ Time.frameCount;
                return generatedSeed;
            }
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null && panel.activeSelf != active)
            {
                panel.SetActive(active);
            }
        }
    }
}
