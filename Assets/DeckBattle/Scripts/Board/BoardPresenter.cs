using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class BoardPresenter : MonoBehaviour
    {
        [SerializeField] private HexTileView tilePrefab;
        [SerializeField] private Transform tileRoot;
        [SerializeField] private float hexSize = 1f;

        private readonly List<HexTileView> tiles = new List<HexTileView>(32);
        private readonly Dictionary<HexCoord, HexTileView> tileByCoord = new Dictionary<HexCoord, HexTileView>(32);
        private HexBoard board;
        private HexTileView highlightedTile;

        public HexBoard Board
        {
            get { return board; }
        }

        public void Build(HexBoard sourceBoard)
        {
            board = sourceBoard;
            ClearExistingTiles();

            if (board == null || tilePrefab == null)
            {
                Debug.LogError("BoardPresenter requires a board and tile prefab.", this);
                return;
            }

            Transform parent = tileRoot != null ? tileRoot : transform;
            Quaternion tileRotation = tilePrefab.transform.localRotation;
            for (int r = 0; r < board.Height; r++)
            {
                for (int q = 0; q < board.Width; q++)
                {
                    HexCoord coord = new HexCoord(q, r);
                    HexTileView tile = Instantiate(tilePrefab, parent);
                    tile.transform.localPosition = board.ToLocalPosition(coord);
                    tile.transform.localRotation = tileRotation;
                    tile.transform.localScale = Vector3.one * hexSize;
                    tile.Initialize(coord, GetDeploymentSide(coord));
                    tiles.Add(tile);
                    tileByCoord.Add(coord, tile);
                }
            }
        }

        public Vector3 GetWorldPosition(HexCoord coord)
        {
            if (board == null)
            {
                return transform.position;
            }

            return transform.TransformPoint(board.ToLocalPosition(coord));
        }

        public HexTileView GetTileView(HexCoord coord)
        {
            HexTileView tile;
            tileByCoord.TryGetValue(coord, out tile);
            return tile;
        }

        public void HighlightSingleTile(HexTileView tile, bool isLegal)
        {
            if (highlightedTile == tile)
            {
                if (highlightedTile != null)
                {
                    if (isLegal)
                    {
                        highlightedTile.SetLegalHighlight();
                    }
                    else
                    {
                        highlightedTile.SetBlockedHighlight();
                    }
                }

                return;
            }

            ClearHoverHighlight();

            highlightedTile = tile;
            if (highlightedTile == null)
            {
                return;
            }

            if (isLegal)
            {
                highlightedTile.SetLegalHighlight();
            }
            else
            {
                highlightedTile.SetBlockedHighlight();
            }
        }

        public void HighlightFormationTiles(BattleState state, PlayerBattleState player, RuntimeUnit selectedUnit)
        {
            ClearHoverHighlight();

            if (state == null || player == null || selectedUnit == null || board == null)
            {
                return;
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                HexTileView tile = tiles[i];
                bool legal = board.IsDeploymentCoord(player.Side, tile.Coord) && !FormationService.IsOccupied(player, tile.Coord, selectedUnit);
                if (legal)
                {
                    tile.SetLegalHighlight();
                }
                else
                {
                    tile.ClearHighlight();
                }
            }

            HexTileView selectedTile = GetTileView(selectedUnit.FormationCoord);
            if (selectedTile != null)
            {
                selectedTile.SetSelectedHighlight();
            }
        }

        public void ClearAllHighlights()
        {
            highlightedTile = null;
            for (int i = 0; i < tiles.Count; i++)
            {
                tiles[i].ClearHighlight();
            }
        }

        public void ClearHoverHighlight()
        {
            if (highlightedTile == null)
            {
                return;
            }

            highlightedTile.ClearHighlight();
            highlightedTile = null;
        }

        private BattleSide? GetDeploymentSide(HexCoord coord)
        {
            bool player = board.IsDeploymentCoord(BattleSide.Player, coord);
            bool enemy = board.IsDeploymentCoord(BattleSide.Enemy, coord);

            if (player && !enemy)
            {
                return BattleSide.Player;
            }

            if (enemy && !player)
            {
                return BattleSide.Enemy;
            }

            return null;
        }

        private void ClearExistingTiles()
        {
            for (int i = tiles.Count - 1; i >= 0; i--)
            {
                if (tiles[i] != null)
                {
                    DestroyTileObject(tiles[i].gameObject);
                }
            }

            tiles.Clear();
            tileByCoord.Clear();
            highlightedTile = null;

            Transform parent = tileRoot != null ? tileRoot : transform;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                DestroyTileObject(parent.GetChild(i).gameObject);
            }
        }

        private static void DestroyTileObject(GameObject tileObject)
        {
            if (Application.isPlaying)
            {
                Destroy(tileObject);
            }
            else
            {
                DestroyImmediate(tileObject);
            }
        }
    }
}
