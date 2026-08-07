using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class PreparationHexGridView
    {
        private readonly Transform owner;
        private readonly List<HexTileView> tiles = new List<HexTileView>(32);
        private readonly Dictionary<HexCoord, HexTileView> tileByCoord = new Dictionary<HexCoord, HexTileView>(32);

        private HexTileView tilePrefab;
        private Transform tileRoot;
        private HexTileView hoveredTile;

        public int TileCount
        {
            get { return tiles.Count; }
        }

        public PreparationHexGridView(Transform owner)
        {
            this.owner = owner;
        }

        public void Configure(HexTileView prefab, Transform root)
        {
            tilePrefab = prefab;
            tileRoot = root;
        }

        public void EnsureBuilt(HexBoard board, HexBoardLayout layout)
        {
            if (board == null || layout == null || tilePrefab == null)
            {
                return;
            }

            if (HasMatchingTopology(board, layout))
            {
                return;
            }

            Rebuild(board, layout);
        }

        public HexTileView GetTile(HexCoord coord)
        {
            HexTileView tile;
            tileByCoord.TryGetValue(coord, out tile);
            return tile;
        }

        public void SetTileVisualState(HexCoord coord, PreparationHexVisualState state)
        {
            HexTileView tile = GetTile(coord);
            if (tile != null)
            {
                tile.SetVisualState(state);
            }
        }

        public void SetHoverVisualState(HexTileView tile, PreparationHexVisualState state)
        {
            if (hoveredTile == tile)
            {
                if (hoveredTile != null)
                {
                    hoveredTile.SetVisualState(state);
                }

                return;
            }

            ClearHoverVisualState();
            hoveredTile = tile;
            if (hoveredTile != null)
            {
                hoveredTile.SetVisualState(state);
            }
        }

        public void ClearHoverVisualState()
        {
            if (hoveredTile == null)
            {
                return;
            }

            hoveredTile.SetVisualState(PreparationHexVisualState.Default);
            hoveredTile = null;
        }

        public void ClearAllVisualStates()
        {
            hoveredTile = null;
            for (int i = 0; i < tiles.Count; i++)
            {
                HexTileView tile = tiles[i];
                if (tile != null)
                {
                    tile.SetVisualState(PreparationHexVisualState.Default);
                }
            }
        }

        public void SetVisible(bool visible)
        {
            Transform parent = GetTileParent();
            if (parent != null && parent != owner)
            {
                if (parent.gameObject.activeSelf != visible)
                {
                    parent.gameObject.SetActive(visible);
                }

                return;
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                HexTileView tile = tiles[i];
                if (tile != null && tile.gameObject.activeSelf != visible)
                {
                    tile.gameObject.SetActive(visible);
                }
            }
        }

        private bool HasMatchingTopology(HexBoard board, HexBoardLayout layout)
        {
            int expectedTileCount = board.Width * board.Height;
            if (tiles.Count != expectedTileCount || tileByCoord.Count != expectedTileCount)
            {
                return false;
            }

            Transform parent = GetTileParent();
            if (parent == null || parent.childCount != expectedTileCount)
            {
                return false;
            }

            for (int r = 0; r < board.Height; r++)
            {
                for (int q = 0; q < board.Width; q++)
                {
                    HexCoord coord = new HexCoord(q, r);
                    HexTileView tile;
                    if (!tileByCoord.TryGetValue(coord, out tile) || tile == null || tile.Coord != coord)
                    {
                        return false;
                    }

                    if (tile.transform.localPosition != layout.GetLocalPosition(coord)
                        || tile.transform.localScale != Vector3.one * layout.HexSize)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void Rebuild(HexBoard board, HexBoardLayout layout)
        {
            ClearExistingTiles();

            Transform parent = GetTileParent();
            if (parent == null)
            {
                return;
            }

            Quaternion tileRotation = tilePrefab.transform.localRotation;
            for (int r = 0; r < board.Height; r++)
            {
                for (int q = 0; q < board.Width; q++)
                {
                    HexCoord coord = new HexCoord(q, r);
                    HexTileView tile = Object.Instantiate(tilePrefab, parent);
                    tile.transform.localPosition = layout.GetLocalPosition(coord);
                    tile.transform.localRotation = tileRotation;
                    tile.transform.localScale = Vector3.one * layout.HexSize;
                    tile.Initialize(coord);
                    tiles.Add(tile);
                    tileByCoord.Add(coord, tile);
                }
            }
        }

        private void ClearExistingTiles()
        {
            Transform parent = GetTileParent();
            if (parent != null)
            {
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    DestroyTileObject(parent.GetChild(i).gameObject);
                }
            }

            tiles.Clear();
            tileByCoord.Clear();
            hoveredTile = null;
        }

        private Transform GetTileParent()
        {
            return tileRoot != null ? tileRoot : owner;
        }

        private static void DestroyTileObject(GameObject tileObject)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(tileObject);
            }
            else
            {
                Object.DestroyImmediate(tileObject);
            }
        }
    }
}
