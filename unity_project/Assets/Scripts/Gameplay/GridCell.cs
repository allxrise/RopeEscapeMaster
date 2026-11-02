using RopeEscape.Config;
using RopeEscape.Utils;
using UnityEngine;

namespace RopeEscape.Gameplay
{
    /// <summary>
    /// 3D board cell at grid coordinate (X, Y).
    /// Renders using the extracted M_Floor_Square1 mesh and tracks occupancy.
    /// </summary>
    public class GridCell : MonoBehaviour
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public Climber CurrentClimber { get; set; }
        public bool HasObstacle { get; set; }
        public bool HasLock { get; set; }
        public bool HasCurtain { get; set; }

        private MeshRenderer _tileRenderer;
        private MeshRenderer _holeRenderer;
        private Color _baseTileColor;
        private Color _baseHoleColor;

        public bool IsAvailableForMove =>
            CurrentClimber == null && !HasObstacle && !HasLock && !HasCurtain;

        public void Initialize(int x, int y, Vector3 worldPos)
        {
            X = x;
            Y = y;
            transform.position = worldPos;
            name = $"GridCell_{x}_{y}";

            // Create 3D tile visual from extracted M_Floor_Square1 mesh
            GameObject tileGo = new GameObject("TileMesh");
            tileGo.transform.SetParent(transform, false);
            tileGo.transform.localPosition = new Vector3(0f, 0f, 0.32f);
            tileGo.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);

            MeshFilter mf = tileGo.AddComponent<MeshFilter>();
            Mesh floorMesh = ObjMeshLoader.LoadMesh("M_Floor_Square1");
            if (floorMesh != null)
            {
                mf.sharedMesh = floorMesh;
            }
            else
            {
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mf.sharedMesh = fallback.GetComponent<MeshFilter>().sharedMesh;
                Destroy(fallback);
                tileGo.transform.localScale = new Vector3(2.75f, 2.75f, 0.45f);
            }

            _tileRenderer = tileGo.AddComponent<MeshRenderer>();
            bool isChecker = ((x + y) & 1) == 0;
            _baseTileColor = isChecker
                ? new Color(0.24f, 0.28f, 0.38f)
                : new Color(0.21f, 0.25f, 0.34f);
            _tileRenderer.sharedMaterial = ObjMeshLoader.CreateColoredMaterial(_baseTileColor, 0.35f, 0.05f);

            // Socket hole indicator in the center of the cell
            GameObject holeGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            holeGo.name = "SocketHole";
            Destroy(holeGo.GetComponent<Collider>());
            holeGo.transform.SetParent(transform, false);
            holeGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            holeGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            holeGo.transform.localScale = new Vector3(1.15f, 0.06f, 1.15f);

            _holeRenderer = holeGo.GetComponent<MeshRenderer>();
            _baseHoleColor = new Color(0.12f, 0.14f, 0.20f);
            _holeRenderer.sharedMaterial = ObjMeshLoader.CreateColoredMaterial(_baseHoleColor, 0.2f, 0.0f);
        }

        public void SetHighlight(bool highlighted, bool valid = true)
        {
            if (_holeRenderer == null)
                return;

            if (!highlighted)
            {
                _holeRenderer.sharedMaterial.color = _baseHoleColor;
                if (_holeRenderer.sharedMaterial.HasProperty("_BaseColor"))
                    _holeRenderer.sharedMaterial.SetColor("_BaseColor", _baseHoleColor);
            }
            else
            {
                Color c = valid
                    ? new Color(0.30f, 0.95f, 0.50f)
                    : new Color(0.95f, 0.30f, 0.30f);
                _holeRenderer.sharedMaterial.color = c;
                if (_holeRenderer.sharedMaterial.HasProperty("_BaseColor"))
                    _holeRenderer.sharedMaterial.SetColor("_BaseColor", c);
            }
        }
    }
}
