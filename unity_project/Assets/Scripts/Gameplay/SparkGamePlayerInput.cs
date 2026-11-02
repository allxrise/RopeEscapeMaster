using UnityEngine;
using UnityEngine.EventSystems;

namespace RopeEscape.Gameplay
{
    /// <summary>
    /// Handles mouse and touch drag-and-drop interaction with Climber pins on the 3D board
    /// (mirrors original SparkGamePlayerInput).
    /// </summary>
    public class SparkGamePlayerInput : MonoBehaviour
    {
        private SparkyGameNode _board;
        private Camera _mainCamera;
        private Climber _draggedClimber;
        private GridCell _originCell;
        private GridCell _highlightedCell;
        private Plane _boardPlane = new Plane(Vector3.back, Vector3.zero);

        public void Initialize(SparkyGameNode board)
        {
            _board = board;
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            if (_board == null || !_board.IsGameActive)
            {
                CancelActiveDrag();
                return;
            }

            if (_mainCamera == null)
                _mainCamera = Camera.main;
            if (_mainCamera == null)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                HandlePointerDown(Input.mousePosition);
            }
            else if (Input.GetMouseButton(0) && _draggedClimber != null)
            {
                HandlePointerDrag(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0) && _draggedClimber != null)
            {
                HandlePointerUp(Input.mousePosition);
            }
        }

        private void HandlePointerDown(Vector3 screenPos)
        {
            Ray ray = _mainCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                Climber climber = hit.collider.GetComponentInParent<Climber>();
                if (climber != null && climber.CanDrag)
                {
                    if (_board.IsCutRopeMode && climber.ParentGroup != null)
                    {
                        _board.IsCutRopeMode = false;
                        climber.ParentGroup.EliminateImmediate();
                        return;
                    }

                    _draggedClimber = climber;
                    _originCell = climber.CurrentCell;
                    _draggedClimber.BeginDrag();
                }
            }
        }

        private void HandlePointerDrag(Vector3 screenPos)
        {
            Ray ray = _mainCamera.ScreenPointToRay(screenPos);
            if (_boardPlane.Raycast(ray, out float enter))
            {
                Vector3 hitWorld = ray.GetPoint(enter);
                _draggedClimber.UpdateDragPosition(hitWorld);

                GridCell nearest = _board.FindNearestAvailableCell(hitWorld, _originCell);
                if (nearest != _highlightedCell)
                {
                    if (_highlightedCell != null)
                        _highlightedCell.SetHighlight(false);
                    _highlightedCell = nearest;
                    if (_highlightedCell != null)
                        _highlightedCell.SetHighlight(true, true);
                }
            }
        }

        private void HandlePointerUp(Vector3 screenPos)
        {
            if (_highlightedCell != null)
            {
                _highlightedCell.SetHighlight(false);
            }

            GridCell targetCell = _highlightedCell != null ? _highlightedCell : _originCell;
            int fromX = _originCell != null ? _originCell.X : _draggedClimber.XCoord;
            int fromY = _originCell != null ? _originCell.Y : _draggedClimber.YCoord;

            _draggedClimber.EndDrag(targetCell);

            if (targetCell != null && targetCell != _originCell)
            {
                _board.RecordPinMove(fromX, fromY, targetCell.X, targetCell.Y);
            }

            _draggedClimber = null;
            _originCell = null;
            _highlightedCell = null;
        }

        private void CancelActiveDrag()
        {
            if (_highlightedCell != null)
            {
                _highlightedCell.SetHighlight(false);
                _highlightedCell = null;
            }
            if (_draggedClimber != null)
            {
                _draggedClimber.EndDrag(_originCell);
                _draggedClimber = null;
                _originCell = null;
            }
        }
    }
}
