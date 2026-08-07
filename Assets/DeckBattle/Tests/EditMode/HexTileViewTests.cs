using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class HexTileViewTests
    {
        [Test]
        public void SetVisualState_UpdatesSpriteRendererColor()
        {
            GameObject tileObject = new GameObject("Tile");
            GameObject spriteObject = new GameObject("Sprite", typeof(SpriteRenderer));
            spriteObject.transform.SetParent(tileObject.transform, false);

            try
            {
                HexTileView tile = tileObject.AddComponent<HexTileView>();
                SpriteRenderer spriteRenderer = spriteObject.GetComponent<SpriteRenderer>();
                tile.Initialize(new HexCoord(2, 3));

                Assert.AreEqual(GetColor(tile, "defaultColor"), spriteRenderer.color);

                tile.SetVisualState(PreparationHexVisualState.Legal);
                Assert.AreEqual(GetColor(tile, "legalHighlightColor"), spriteRenderer.color);

                tile.SetVisualState(PreparationHexVisualState.Blocked);
                Assert.AreEqual(GetColor(tile, "blockedHighlightColor"), spriteRenderer.color);

                tile.SetVisualState(PreparationHexVisualState.Selected);
                Assert.AreEqual(GetColor(tile, "selectedHighlightColor"), spriteRenderer.color);
            }
            finally
            {
                Object.DestroyImmediate(tileObject);
            }
        }

        private static Color GetColor(HexTileView tile, string fieldName)
        {
            return (Color)typeof(HexTileView)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(tile);
        }
    }
}
