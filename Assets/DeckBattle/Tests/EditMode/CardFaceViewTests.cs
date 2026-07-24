using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DeckBattle.Tests
{
    public sealed class CardFaceViewTests
    {
        [TearDown]
        public void TearDown()
        {
            TestDefinitions.DestroyCreatedObjects();
        }

        [Test]
        public void BindDefinition_ShowsDisplayNameAndApCostOnly()
        {
            GameObject root = CreateCardFace(out CardFaceView view, out _, out TMP_Text nameText, out TMP_Text costText);

            try
            {
                UnitDefinition definition = TestDefinitions.CreateUnit("swordsman", 2);
                definition.DisplayName = "Swordsman";

                view.Bind(definition);

                Assert.AreEqual("Swordsman", nameText.text);
                Assert.AreEqual("2", costText.text);
                Assert.IsNull(root.transform.Find("Stats"));
                Assert.IsNull(root.transform.Find("Type"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SetVisualState_UpdatesBackgroundColor()
        {
            GameObject root = CreateCardFace(out CardFaceView view, out Image background, out _, out _);

            try
            {
                view.SetVisualState(CardVisualState.InDeck);

                Assert.AreEqual(CardVisualState.InDeck, view.VisualState);
                Assert.AreEqual(new Color(0.40f, 0.32f, 0.16f, 0.96f), background.color);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeckBuilderCardItemView_BindDelegatesPresentationAndKeepsClickBehavior()
        {
            GameObject root = new GameObject("DeckBuilderItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(DeckBuilderCardItemView));
            GameObject faceRoot = CreateCardFace(out CardFaceView faceView, out _, out TMP_Text nameText, out TMP_Text costText);
            faceRoot.transform.SetParent(root.transform, false);

            try
            {
                Button button = root.GetComponent<Button>();
                DeckBuilderCardItemView itemView = root.GetComponent<DeckBuilderCardItemView>();
                SerializedObject serializedItem = new SerializedObject(itemView);
                serializedItem.FindProperty("button").objectReferenceValue = button;
                serializedItem.FindProperty("faceView").objectReferenceValue = faceView;
                serializedItem.ApplyModifiedPropertiesWithoutUndo();
                itemView.SendMessage("Awake");
                itemView.SendMessage("OnEnable");

                string clickedCardId = null;
                UnitDefinition definition = TestDefinitions.CreateUnit("guard", 3);
                definition.DisplayName = "Guard";

                itemView.Bind(definition, true, true, cardId => clickedCardId = cardId);
                button.onClick.Invoke();

                Assert.IsTrue(button.interactable);
                Assert.AreEqual("guard", clickedCardId);
                Assert.AreEqual("Guard", nameText.text);
                Assert.AreEqual("3", costText.text);
                Assert.AreEqual(CardVisualState.InDeck, faceView.VisualState);

                clickedCardId = null;
                itemView.Bind(definition, false, false, cardId => clickedCardId = cardId);
                button.onClick.Invoke();

                Assert.IsFalse(button.interactable);
                Assert.IsNull(clickedCardId);
                Assert.AreEqual(CardVisualState.Locked, faceView.VisualState);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateCardFace(out CardFaceView view, out Image background, out TMP_Text nameText, out TMP_Text costText)
        {
            GameObject root = new GameObject("CardFace", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CardFaceView));
            background = root.GetComponent<Image>();
            GameObject nameObject = CreateText("Name", root.transform);
            GameObject costObject = CreateText("Cost", root.transform);
            nameText = nameObject.GetComponent<TMP_Text>();
            costText = costObject.GetComponent<TMP_Text>();
            view = root.GetComponent<CardFaceView>();

            SerializedObject serializedView = new SerializedObject(view);
            serializedView.FindProperty("background").objectReferenceValue = background;
            serializedView.FindProperty("nameText").objectReferenceValue = nameText;
            serializedView.FindProperty("costText").objectReferenceValue = costText;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject CreateText(string name, Transform parent)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            textObject.GetComponent<TextMeshProUGUI>().raycastTarget = false;
            return textObject;
        }
    }
}
