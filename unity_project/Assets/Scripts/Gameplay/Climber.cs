using RopeEscape.Config;
using RopeEscape.Utils;
using UnityEngine;

namespace RopeEscape.Gameplay
{
    /// <summary>
    /// Represents a movable or fixed pin (Climber) on the board.
    /// Uses the extracted Yu_Pedestal, Y_PedestalNoMove, SphereFace, ropeRing_1, Gear, and IceModelPedestal_0..2 meshes.
    /// </summary>
    public class Climber : MonoBehaviour
    {
        public int XCoord { get; private set; }
        public int YCoord { get; private set; }
        public int ColorType { get; private set; }
        public bool IsFixed { get; private set; }
        public bool IsGear { get; private set; }
        public int IceCount { get; private set; }
        public bool IsBeingDragged { get; private set; }

        public GridCell CurrentCell { get; private set; }
        public RopeGroupItem ParentGroup { get; private set; }
        public KeyItem AttachedKey { get; set; }

        private GameObject _iceVisualGo;
        private MeshFilter _iceMeshFilter;
        private Transform _visualRoot;

        public bool CanDrag =>
            !IsFixed && IceCount <= 0 && ParentGroup != null && !ParentGroup.IsEliminating;

        public void Initialize(
            RopeGroupItem group,
            GridCell cell,
            int colorType,
            bool isFixed,
            bool isGear,
            int iceCount)
        {
            ParentGroup = group;
            ColorType = colorType;
            IsFixed = isFixed;
            IsGear = isGear;
            IceCount = Mathf.Max(0, iceCount);

            SnapToCell(cell);
            BuildVisuals();

            SphereCollider col = gameObject.GetComponent<SphereCollider>();
            if (col == null)
                col = gameObject.AddComponent<SphereCollider>();
            col.radius = 1.25f;
            col.center = new Vector3(0f, 0f, -0.45f);
        }

        private void BuildVisuals()
        {
            _visualRoot = new GameObject("VisualRoot").transform;
            _visualRoot.SetParent(transform, false);

            Color pinColor = GameConst.GetRopeColor(ColorType);

            if (IsFixed)
            {
                // Fixed pin uses Y_PedestalNoMove
                GameObject fixedGo = CreateMeshPart("FixedPedestal", "Y_PedestalNoMove", _visualRoot,
                    new Vector3(0f, 0f, -0.05f), Quaternion.identity, new Vector3(0.95f, 0.95f, 1.1f),
                    Color.Lerp(pinColor, new Color(0.25f, 0.25f, 0.30f), 0.35f));

                // Top metallic bolt cap indicating non-movable pin
                CreateMeshPart("FixedCap", "SphereTex", fixedGo.transform,
                    new Vector3(0f, 0f, -0.52f), Quaternion.identity, new Vector3(0.75f, 0.75f, 0.35f),
                    new Color(0.78f, 0.80f, 0.85f));
            }
            else
            {
                // Movable pin uses Yu_Pedestal (oriented along -Z toward camera)
                CreateMeshPart("Pedestal", "Yu_Pedestal", _visualRoot,
                    new Vector3(0f, 0f, -0.05f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.95f, 1.15f, 0.95f),
                    pinColor);

                // Top cap & ring
                CreateMeshPart("Ring", "ropeRing_1", _visualRoot,
                    new Vector3(0f, 0f, -0.55f), Quaternion.identity, new Vector3(1.1f, 1.1f, 1.1f),
                    Color.Lerp(pinColor, Color.white, 0.25f));
            }

            if (IsGear)
            {
                CreateMeshPart("Gear", "Gear", _visualRoot,
                    new Vector3(0f, 0f, -0.10f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.95f, 0.95f, 0.95f),
                    new Color(0.85f, 0.72f, 0.25f));
            }

            if (IceCount > 0)
            {
                UpdateIceVisual();
            }
        }

        private GameObject CreateMeshPart(
            string partName,
            string meshName,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            Vector3 localScale,
            Color color)
        {
            GameObject go = new GameObject(partName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            Mesh loaded = ObjMeshLoader.LoadMesh(meshName);
            if (loaded != null)
            {
                mf.sharedMesh = loaded;
            }
            else
            {
                GameObject prim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mf.sharedMesh = prim.GetComponent<MeshFilter>().sharedMesh;
                Destroy(prim);
            }

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ObjMeshLoader.CreateColoredMaterial(color, 0.65f, 0.1f);
            return go;
        }

        public void DecrementIce()
        {
            if (IceCount <= 0)
                return;

            IceCount--;
            UpdateIceVisual();
        }

        private void UpdateIceVisual()
        {
            if (IceCount <= 0)
            {
                if (_iceVisualGo != null)
                    Destroy(_iceVisualGo);
                return;
            }

            string meshName = IceCount >= 3
                ? "IceModelPedestal_0"
                : (IceCount == 2 ? "IceModelPedestal_1" : "IceModelPedestal_2");

            if (_iceVisualGo == null)
            {
                _iceVisualGo = CreateMeshPart(
                    "IceBlock",
                    meshName,
                    _visualRoot,
                    new Vector3(0f, 0f, -0.45f),
                    Quaternion.Euler(-90f, 0f, 0f),
                    new Vector3(1.1f, 1.1f, 1.1f),
                    new Color(0.55f, 0.88f, 1.0f, 0.85f));
                _iceMeshFilter = _iceVisualGo.GetComponent<MeshFilter>();
            }
            else if (_iceMeshFilter != null)
            {
                Mesh m = ObjMeshLoader.LoadMesh(meshName);
                if (m != null)
                    _iceMeshFilter.sharedMesh = m;
            }
        }

        public void BeginDrag()
        {
            IsBeingDragged = true;
            if (_visualRoot != null)
            {
                _visualRoot.localScale = new Vector3(1.18f, 1.18f, 1.18f);
            }
        }

        public void UpdateDragPosition(Vector3 worldPos)
        {
            // Lift pin toward camera (-Z) while dragging so its rope glides smoothly
            transform.position = new Vector3(worldPos.x, worldPos.y, -1.35f);
            if (AttachedKey != null)
            {
                AttachedKey.transform.position = transform.position + new Vector3(0f, 0f, -0.6f);
            }
        }

        public void EndDrag(GridCell targetCell)
        {
            IsBeingDragged = false;
            if (_visualRoot != null)
            {
                _visualRoot.localScale = Vector3.one;
            }

            if (targetCell != null && targetCell != CurrentCell && targetCell.IsAvailableForMove)
            {
                SnapToCell(targetCell);
            }
            else if (CurrentCell != null)
            {
                transform.position = CurrentCell.transform.position;
            }

            if (AttachedKey != null)
            {
                AttachedKey.transform.position = transform.position + new Vector3(0f, 0f, -0.6f);
            }
        }

        public void SnapToCell(GridCell cell)
        {
            if (CurrentCell != null && CurrentCell.CurrentClimber == this)
            {
                CurrentCell.CurrentClimber = null;
            }

            CurrentCell = cell;
            if (cell != null)
            {
                cell.CurrentClimber = this;
                XCoord = cell.X;
                YCoord = cell.Y;
                transform.position = cell.transform.position;
            }
        }
    }
}
