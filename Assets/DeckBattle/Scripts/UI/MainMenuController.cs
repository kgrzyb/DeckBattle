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

        [Header("Buttons")]
        [SerializeField] private Button startBattleButton;
        [SerializeField] private Button collectionButton;
        [SerializeField] private Button deckButton;
        [SerializeField] private Button backButton;

        [Header("Views")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject placeholderPanel;
        [SerializeField] private TMP_Text placeholderTitleText;
        [SerializeField] private TMP_Text placeholderBodyText;

        private void Awake()
        {
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
        }

        public void StartBattle()
        {
            if (string.IsNullOrWhiteSpace(battleSceneName))
            {
                Debug.LogError("MainMenuController requires a battle scene name.", this);
                return;
            }

            SceneManager.LoadScene(battleSceneName);
        }

        public void ShowMenu()
        {
            SetPanelActive(menuPanel, true);
            SetPanelActive(placeholderPanel, false);
        }

        public void ShowCollection()
        {
            ShowPlaceholder("Collection", "Card collection placeholder");
        }

        public void ShowDeck()
        {
            ShowPlaceholder("Deck", "Deck management placeholder");
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
