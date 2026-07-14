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
        private HexBoard board;

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
            for (int r = 0; r < board.Height; r++)
            {
                for (int q = 0; q < board.Width; q++)
                {
                    HexCoord coord = new HexCoord(q, r);
                    HexTileView tile = Instantiate(tilePrefab, parent);
                    tile.transform.localPosition = board.ToLocalPosition(coord);
                    tile.transform.localRotation = Quaternion.identity;
                    tile.transform.localScale = Vector3.one * hexSize;
                    tile.Initialize(coord, GetDeploymentSide(coord));
                    tiles.Add(tile);
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
