using RopeEscape.Config;
using RopeEscape.Utils;
using UnityEngine;

namespace RopeEscape.Gameplay
{
    /// <summary>
    /// Pillar obstacle (uses extracted Y_Pillar mesh) that blocks grid cells and deflects ropes.
    /// </summary>
    public class PillarItem : MonoBehaviour
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public GridCell Cell { get; private set; }

        public void Initialize(GridCell cell)
        {
            Cell = cell;
            if (cell != null)
            {
                X = cell.X;
                Y = cell.Y;
                cell.HasObstacle = true;
                transform.position = cell.transform.position;
            }

            GameObject meshGo = new GameObject("PillarMesh");
            meshGo.transform.SetParent(transform, false);
            meshGo.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            meshGo.transform.localScale = new Vector3(0.95f, 0.95f, 0.85f);

            MeshFilter mf = meshGo.AddComponent<MeshFilter>();
            Mesh mesh = ObjMeshLoader.LoadMesh("Y_Pillar");
            if (mesh != null)
            {
                mf.sharedMesh = mesh;
            }
            else
            {
                GameObject prim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mf.sharedMesh = prim.GetComponent<MeshFilter>().sharedMesh;
                Destroy(prim);
            }

            MeshRenderer mr = meshGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ObjMeshLoader.CreateColoredMaterial(new Color(0.55f, 0.58f, 0.66f), 0.45f, 0.2f);
        }
    }

    /// <summary>
    /// Key item (uses extracted Y_Key mesh) attached to a pin or cell; unlocks a matching LockItem.
    /// </summary>
    public class KeyItem : MonoBehaviour
    {
        public int KeyType { get; private set; }

        public void Initialize(int keyType, Vector3 worldPos)
        {
            KeyType = keyType;
            transform.position = worldPos + new Vector3(0f, 0f, -0.65f);

            GameObject meshGo = new GameObject("KeyMesh");
            meshGo.transform.SetParent(transform, false);
            meshGo.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);

            MeshFilter mf = meshGo.AddComponent<MeshFilter>();
            Mesh mesh = ObjMeshLoader.LoadMesh("Y_Key");
            if (mesh != null)
            {
                mf.sharedMesh = mesh;
            }
            else
            {
                GameObject prim = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mf.sharedMesh = prim.GetComponent<MeshFilter>().sharedMesh;
                Destroy(prim);
            }

            MeshRenderer mr = meshGo.AddComponent<MeshRenderer>();
            Color c = GameConst.GetRopeColor(keyType > 0 ? keyType : 4);
            mr.sharedMaterial = ObjMeshLoader.CreateColoredMaterial(c, 0.75f, 0.35f);
        }
    }

    /// <summary>
    /// Lock obstacle (uses extracted M_Lock, LockLeft, LockRight meshes) blocking a GridCell until unlocked.
    /// </summary>
    public class LockItem : MonoBehaviour
    {
        public int LockType { get; private set; }
        public GridCell Cell { get; private set; }

        public void Initialize(int lockType, GridCell cell)
        {
            LockType = lockType;
            Cell = cell;
            if (cell != null)
            {
                cell.HasLock = true;
                transform.position = cell.transform.position + new Vector3(0f, 0f, -0.35f);
            }

            Color c = GameConst.GetRopeColor(lockType > 0 ? lockType : 4);

            GameObject bodyGo = new GameObject("LockBody");
            bodyGo.transform.SetParent(transform, false);
            bodyGo.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            MeshFilter mf = bodyGo.AddComponent<MeshFilter>();
            Mesh m = ObjMeshLoader.LoadMesh("M_Lock") ?? ObjMeshLoader.LoadMesh("LockLeft");
            if (m != null)
                mf.sharedMesh = m;
            MeshRenderer mr = bodyGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ObjMeshLoader.CreateColoredMaterial(c, 0.65f, 0.25f);
        }

        public void Unlock()
        {
            if (Cell != null)
                Cell.HasLock = false;
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Handrail / Rod item (uses extracted handrail mesh) along which a rope pin can slide.
    /// </summary>
    public class RopeRodItem : MonoBehaviour
    {
        public RodInfo Info { get; private set; }

        public void Initialize(RodInfo info, Vector3 worldPos)
        {
            Info = info;
            transform.position = worldPos + new Vector3(0f, 0f, -0.15f);

            GameObject go = new GameObject("HandrailMesh");
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(Mathf.Max(1, info.length), 1f, 1f);
            if (info.dir == 1)
                go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            MeshFilter mf = go.AddComponent<MeshFilter>();
            Mesh m = ObjMeshLoader.LoadMesh("handrail");
            if (m != null)
                mf.sharedMesh = m;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ObjMeshLoader.CreateColoredMaterial(new Color(0.72f, 0.76f, 0.84f), 0.6f, 0.3f);
        }
    }
}
