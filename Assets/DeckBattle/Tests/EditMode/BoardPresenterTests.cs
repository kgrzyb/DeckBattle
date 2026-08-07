using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class BoardPresenterTests
    {
        [Test]
        public void EnsureBuilt_WithMatchingTopology_ReusesTileInstances()
        {
            GameObject presenterObject = new GameObject("BoardPresenter", typeof(BoardPresenter));
            GameObject tilePrefabObject = new GameObject("TilePrefab", typeof(HexTileView));
            GameObject tileRootObject = new GameObject("TileRoot");

            try
            {
                BoardPresenter presenter = presenterObject.GetComponent<BoardPresenter>();
                SetPrivateField(presenter, "tilePrefab", tilePrefabObject.GetComponent<HexTileView>());
                SetPrivateField(presenter, "tileRoot", tileRootObject.transform);

                presenter.EnsureBuilt(new HexBoard(3, 2, 1f));
                List<HexTileView> initialTiles = new List<HexTileView>(GetTiles(presenter));

                presenter.EnsureBuilt(new HexBoard(3, 2, 1f));

                Assert.AreEqual(6, tileRootObject.transform.childCount);
                CollectionAssert.AreEqual(initialTiles, GetTiles(presenter));
            }
            finally
            {
                Object.DestroyImmediate(tileRootObject);
                Object.DestroyImmediate(tilePrefabObject);
                Object.DestroyImmediate(presenterObject);
            }
        }

        [Test]
        public void EnsureBuilt_WhenTopologyChanges_RebuildsExpectedTileCount()
        {
            GameObject presenterObject = new GameObject("BoardPresenter", typeof(BoardPresenter));
            GameObject tilePrefabObject = new GameObject("TilePrefab", typeof(HexTileView));
            GameObject tileRootObject = new GameObject("TileRoot");

            try
            {
                BoardPresenter presenter = presenterObject.GetComponent<BoardPresenter>();
                SetPrivateField(presenter, "tilePrefab", tilePrefabObject.GetComponent<HexTileView>());
                SetPrivateField(presenter, "tileRoot", tileRootObject.transform);

                presenter.EnsureBuilt(new HexBoard(2, 2, 1f));
                presenter.EnsureBuilt(new HexBoard(4, 2, 1f));

                Assert.AreEqual(8, tileRootObject.transform.childCount);
                Assert.AreEqual(8, GetTiles(presenter).Count);
                Assert.IsNotNull(presenter.GetTileView(new HexCoord(3, 1)));
            }
            finally
            {
                Object.DestroyImmediate(tileRootObject);
                Object.DestroyImmediate(tilePrefabObject);
                Object.DestroyImmediate(presenterObject);
            }
        }

        [Test]
        public void EnsureBuilt_WhenHexSizeChanges_RebuildsTilesAtConfiguredScale()
        {
            GameObject presenterObject = new GameObject("BoardPresenter", typeof(BoardPresenter));
            GameObject tilePrefabObject = new GameObject("TilePrefab", typeof(HexTileView));
            GameObject tileRootObject = new GameObject("TileRoot");

            try
            {
                BoardPresenter presenter = presenterObject.GetComponent<BoardPresenter>();
                SetPrivateField(presenter, "tilePrefab", tilePrefabObject.GetComponent<HexTileView>());
                SetPrivateField(presenter, "tileRoot", tileRootObject.transform);

                presenter.EnsureBuilt(new HexBoard(3, 2, 1f));
                HexTileView firstTile = GetTiles(presenter)[0];
                var resizedBoard = new HexBoard(3, 2, 1.5f);

                presenter.EnsureBuilt(resizedBoard);

                HexTileView resizedTile = presenter.GetTileView(new HexCoord(2, 1));
                Assert.AreNotSame(firstTile, GetTiles(presenter)[0]);
                Assert.AreEqual(Vector3.one * resizedBoard.HexSize, resizedTile.transform.localScale);
                Assert.AreEqual(resizedBoard.ToLocalPosition(new HexCoord(2, 1)), resizedTile.transform.localPosition);
            }
            finally
            {
                Object.DestroyImmediate(tileRootObject);
                Object.DestroyImmediate(tilePrefabObject);
                Object.DestroyImmediate(presenterObject);
            }
        }

        private static List<HexTileView> GetTiles(BoardPresenter presenter)
        {
            return (List<HexTileView>)typeof(BoardPresenter)
                .GetField("tiles", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(presenter);
        }

        private static void SetPrivateField(object target, string fieldName, Object value)
        {
            typeof(BoardPresenter)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }
    }
}
