using UnityEngine;

namespace DeckBattle
{
    public sealed class BoardPresenter : MonoBehaviour
    {
        [SerializeField] private HexTileView tilePrefab;
        [SerializeField] private Transform tileRoot;

        private HexBoard board;
        private HexBoardLayout layout;
        private PreparationHexGridView preparationHexGridView;

        public HexBoard Board
        {
            get { return board; }
        }

        public int TileCount
        {
            get
            {
                EnsurePreparationHexGridView();
                return preparationHexGridView.TileCount;
            }
        }

        private void Awake()
        {
            EnsurePreparationHexGridView();
            preparationHexGridView.SetVisible(false);
        }

        public void EnsureBuilt(HexBoard sourceBoard)
        {
            if (sourceBoard == null || tilePrefab == null)
            {
                Debug.LogError("BoardPresenter requires a board and tile prefab.", this);
                return;
            }

            board = sourceBoard;
            if (layout == null || !layout.Matches(sourceBoard))
            {
                layout = new HexBoardLayout(sourceBoard);
            }

            EnsurePreparationHexGridView();
            preparationHexGridView.EnsureBuilt(sourceBoard, layout);
        }

        public Vector3 GetWorldPosition(HexCoord coord)
        {
            EnsureLayout();
            if (layout == null)
            {
                return transform.position;
            }

            return transform.TransformPoint(layout.GetLocalPosition(coord));
        }

        public Vector3 GetWorldCenter()
        {
            EnsureLayout();
            if (layout == null)
            {
                return transform.position;
            }

            return transform.TransformPoint(layout.GetLocalCenter());
        }

        public HexTileView GetTileView(HexCoord coord)
        {
            EnsurePreparationHexGridView();
            return preparationHexGridView.GetTile(coord);
        }

        public void SetTileVisualState(HexCoord coord, PreparationHexVisualState state)
        {
            EnsurePreparationHexGridView();
            preparationHexGridView.SetTileVisualState(coord, state);
        }

        public void SetHoverVisualState(HexTileView tile, PreparationHexVisualState state)
        {
            EnsurePreparationHexGridView();
            preparationHexGridView.SetHoverVisualState(tile, state);
        }

        public void ClearHoverVisualState()
        {
            EnsurePreparationHexGridView();
            preparationHexGridView.ClearHoverVisualState();
        }

        public void ClearAllVisualStates()
        {
            EnsurePreparationHexGridView();
            preparationHexGridView.ClearAllVisualStates();
        }

        public void SetPreparationHexesVisible(bool visible)
        {
            EnsurePreparationHexGridView();
            if (!visible)
            {
                preparationHexGridView.ClearAllVisualStates();
            }

            preparationHexGridView.SetVisible(visible);
        }

        private void EnsurePreparationHexGridView()
        {
            if (preparationHexGridView == null)
            {
                preparationHexGridView = new PreparationHexGridView(transform);
            }

            preparationHexGridView.Configure(tilePrefab, tileRoot);
        }

        private void EnsureLayout()
        {
            if (board != null && (layout == null || !layout.Matches(board)))
            {
                layout = new HexBoardLayout(board);
            }
        }
    }
}
