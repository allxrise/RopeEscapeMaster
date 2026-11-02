using System.Collections.Generic;
using RopeEscape.Config;
using RopeEscape.Utils;
using UnityEngine;

namespace RopeEscape.Rope
{
    /// <summary>
    /// 3D Verlet / Position-Based Dynamics (PBD) rope segment with procedural tubular mesh generation.
    /// Preserves initial curved rest length while tangled so looped ropes never self-untangle on load.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VerletRope : MonoBehaviour
    {
        public struct RopeParticle
        {
            public Vector3 Position;
            public Vector3 PrevPosition;
            public float InvMass;
        }

        public Transform StartPin { get; private set; }
        public Transform EndPin { get; private set; }
        public int MaterialType { get; private set; }
        public float RopeRadius { get; set; } = 0.28f;
        public int ActiveCrossingCount { get; set; }
        public bool IsEliminating { get; private set; }
        public bool HasPinMoved { get; set; }

        public RopeParticle[] Particles;
        private float _restSegmentLength = 0.45f;
        private float _initialCurveSegmentLength = 0.45f;
        private Vector3 _initialStartAnchor;
        private Vector3 _initialEndAnchor;

        private Mesh _tubeMesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        private readonly List<Vector3> _meshVerts = new List<Vector3>(640);
        private readonly List<Vector3> _meshNorms = new List<Vector3>(640);
        private readonly List<Vector2> _meshUvs = new List<Vector2>(640);
        private readonly List<int> _meshTris = new List<int>(1920);

        private const int RadialSegments = 8;

        public void Initialize(
            Transform startPin,
            Transform endPin,
            List<Vector3> localOffsetsFromStart,
            int materialType)
        {
            StartPin = startPin;
            EndPin = endPin;
            MaterialType = materialType;

            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            _tubeMesh = new Mesh { name = $"RopeTube_{materialType}" };
            _tubeMesh.MarkDynamic();
            _meshFilter.sharedMesh = _tubeMesh;

            Color ropeColor = GameConst.GetRopeColor(materialType);
            _meshRenderer.sharedMaterial = ObjMeshLoader.CreateColoredMaterial(ropeColor, 0.65f, 0.08f);

            BuildInitialParticles(localOffsetsFromStart);
            RebuildTubeMesh();
        }

        private void BuildInitialParticles(List<Vector3> localOffsets)
        {
            Vector3 startWorld = GetPinAnchor(StartPin);
            Vector3 endWorld = GetPinAnchor(EndPin);
            _initialStartAnchor = startWorld;
            _initialEndAnchor = endWorld;

            List<Vector3> rawPoints = new List<Vector3>();
            if (localOffsets != null && localOffsets.Count >= 2)
            {
                for (int i = 0; i < localOffsets.Count; i++)
                {
                    Vector3 offset = localOffsets[i];
                    // Offset Z in level config is relative to board plane; shift by -0.35f so ropes sit above tiles
                    Vector3 pt = new Vector3(
                        startWorld.x + offset.x,
                        startWorld.y + offset.y,
                        offset.z - 0.35f);
                    rawPoints.Add(pt);
                }
                rawPoints[0] = startWorld;
                rawPoints[rawPoints.Count - 1] = endWorld;
            }
            else
            {
                const int defaultSteps = 18;
                for (int i = 0; i <= defaultSteps; i++)
                {
                    float t = i / (float)defaultSteps;
                    rawPoints.Add(Vector3.Lerp(startWorld, endWorld, t));
                }
            }

            // Resample by arc-length to 20 particles for uniform capsule-to-capsule collision resolution
            const int targetParticleCount = 20;
            Particles = new RopeParticle[targetParticleCount];
            float totalCurveLen = ComputePolylineLength(rawPoints);

            for (int i = 0; i < targetParticleCount; i++)
            {
                float t = i / (float)(targetParticleCount - 1);
                Vector3 pos = SamplePolylineByArcLength(rawPoints, t, totalCurveLen);
                Particles[i] = new RopeParticle
                {
                    Position = pos,
                    PrevPosition = pos,
                    InvMass = (i == 0 || i == targetParticleCount - 1) ? 0f : 1f
                };
            }

            float straightDist = Vector3.Distance(startWorld, endWorld);
            float effectiveLen = Mathf.Max(straightDist, totalCurveLen * 0.98f);
            _initialCurveSegmentLength = Mathf.Clamp(effectiveLen / (targetParticleCount - 1), 0.18f, 1.35f);
            _restSegmentLength = _initialCurveSegmentLength;
            ActiveCrossingCount = 1; // Assume tangled until first crossing evaluation completes
        }

        private static float ComputePolylineLength(List<Vector3> pts)
        {
            float total = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                total += Vector3.Distance(pts[i], pts[i + 1]);
            }
            return total;
        }

        private static Vector3 SamplePolylineByArcLength(List<Vector3> pts, float t, float totalLen)
        {
            if (pts.Count == 0) return Vector3.zero;
            if (t <= 0f || totalLen <= 1e-5f) return pts[0];
            if (t >= 1f) return pts[pts.Count - 1];

            float targetDist = t * totalLen;
            float accum = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float segLen = Vector3.Distance(pts[i], pts[i + 1]);
                if (accum + segLen >= targetDist && segLen > 1e-5f)
                {
                    float localT = (targetDist - accum) / segLen;
                    return Vector3.Lerp(pts[i], pts[i + 1], localT);
                }
                accum += segLen;
            }
            return pts[pts.Count - 1];
        }

        public static Vector3 GetPinAnchor(Transform pin)
        {
            if (pin == null) return Vector3.zero;
            return new Vector3(pin.position.x, pin.position.y, pin.position.z - 0.42f);
        }

        public void StepVerlet(float dt)
        {
            if (Particles == null || Particles.Length < 2 || IsEliminating)
                return;

            Vector3 startAnchor = GetPinAnchor(StartPin);
            Vector3 endAnchor = GetPinAnchor(EndPin);

            if ((startAnchor - _initialStartAnchor).sqrMagnitude > 0.04f ||
                (endAnchor - _initialEndAnchor).sqrMagnitude > 0.04f)
            {
                HasPinMoved = true;
            }

            Particles[0].Position = startAnchor;
            Particles[0].PrevPosition = startAnchor;
            Particles[Particles.Length - 1].Position = endAnchor;
            Particles[Particles.Length - 1].PrevPosition = endAnchor;

            float span = Vector3.Distance(startAnchor, endAnchor);
            float straightRest = Mathf.Clamp((span * 1.02f) / (Particles.Length - 1), 0.18f, 1.35f);

            if (ActiveCrossingCount == 0 && HasPinMoved)
            {
                // Rope is untangled after a move: smoothly tighten toward straight line
                _restSegmentLength = Mathf.Lerp(_restSegmentLength, straightRest, dt * 10f);
            }
            else if (HasPinMoved)
            {
                // Pin was moved while rope still has crossings: tighten gently so it wraps taut around crossing ropes
                float tautRest = Mathf.Max(straightRest, _initialCurveSegmentLength * 0.78f);
                _restSegmentLength = Mathf.Lerp(_restSegmentLength, tautRest, dt * 5f);
            }
            else
            {
                // Initial board state: preserve exact curve rest length so looped ropes never self-contract
                _restSegmentLength = _initialCurveSegmentLength;
            }

            const float damping = 0.80f;
            Vector3 boardPull = new Vector3(0f, 0f, 3.5f);

            for (int i = 1; i < Particles.Length - 1; i++)
            {
                Vector3 cur = Particles[i].Position;
                Vector3 vel = (cur - Particles[i].PrevPosition) * damping;
                Particles[i].PrevPosition = cur;
                Vector3 next = cur + vel + boardPull * (dt * dt);

                // Clamp against board floor (z <= -0.26f) and ceiling (z >= -4.20f)
                if (next.z > -0.26f) next.z = -0.26f;
                if (next.z < -4.20f) next.z = -4.20f;
                Particles[i].Position = next;
            }
        }

        public void SolveInternalConstraints()
        {
            if (Particles == null || Particles.Length < 2 || IsEliminating)
                return;

            int n = Particles.Length;
            Particles[0].Position = GetPinAnchor(StartPin);
            Particles[n - 1].Position = GetPinAnchor(EndPin);

            // Distance constraints
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p1 = Particles[i].Position;
                Vector3 p2 = Particles[i + 1].Position;
                Vector3 delta = p2 - p1;
                float dist = delta.magnitude;
                if (dist < 1e-5f) continue;

                float w1 = Particles[i].InvMass;
                float w2 = Particles[i + 1].InvMass;
                float wSum = w1 + w2;
                if (wSum <= 0f) continue;

                float diff = (dist - _restSegmentLength) / (dist * wSum);
                Particles[i].Position = p1 + delta * (w1 * diff * 0.82f);
                Particles[i + 1].Position = p2 - delta * (w2 * diff * 0.82f);
            }

            // Very light smoothing when tangled so loops keep their curvature; stronger when untangled
            float xySmooth = (ActiveCrossingCount > 0 && !HasPinMoved) ? 0.015f : 0.06f;
            for (int i = 1; i < n - 1; i++)
            {
                Vector3 avg = 0.5f * (Particles[i - 1].Position + Particles[i + 1].Position);
                Vector3 pos = Particles[i].Position;
                pos.x = Mathf.Lerp(pos.x, avg.x, xySmooth);
                pos.y = Mathf.Lerp(pos.y, avg.y, xySmooth);
                pos.z = Mathf.Clamp(Mathf.Lerp(pos.z, avg.z, 0.04f), -4.20f, -0.26f);
                Particles[i].Position = pos;
            }
        }

        public void StartEliminateRetract()
        {
            IsEliminating = true;
        }

        public void StepEliminateAnimation(float progress01)
        {
            if (Particles == null || Particles.Length < 2)
                return;

            Vector3 target = GetPinAnchor(EndPin);
            for (int i = 0; i < Particles.Length; i++)
            {
                Particles[i].Position = Vector3.Lerp(Particles[i].Position, target, progress01);
            }
            RebuildTubeMesh();
        }

        public void RebuildTubeMesh()
        {
            if (Particles == null || Particles.Length < 2 || _tubeMesh == null)
                return;

            _meshVerts.Clear();
            _meshNorms.Clear();
            _meshUvs.Clear();
            _meshTris.Clear();

            int ringCount = Particles.Length;
            Vector3 prevNormal = Vector3.up;

            for (int i = 0; i < ringCount; i++)
            {
                Vector3 center = transform.InverseTransformPoint(Particles[i].Position);
                Vector3 tangent;
                if (i == 0)
                    tangent = (Particles[1].Position - Particles[0].Position).normalized;
                else if (i == ringCount - 1)
                    tangent = (Particles[ringCount - 1].Position - Particles[ringCount - 2].Position).normalized;
                else
                    tangent = (Particles[i + 1].Position - Particles[i - 1].Position).normalized;

                if (tangent.sqrMagnitude < 1e-4f)
                    tangent = Vector3.right;

                Vector3 normal = prevNormal - tangent * Vector3.Dot(prevNormal, tangent);
                if (normal.sqrMagnitude < 1e-4f)
                {
                    normal = Vector3.Cross(tangent, Vector3.back);
                    if (normal.sqrMagnitude < 1e-4f)
                        normal = Vector3.Cross(tangent, Vector3.up);
                }
                normal.Normalize();
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                prevNormal = normal;

                float vCoord = i / (float)(ringCount - 1);
                float r = RopeRadius * ((i == 0 || i == ringCount - 1) ? 0.85f : 1.0f);

                for (int s = 0; s < RadialSegments; s++)
                {
                    float angle = (s / (float)RadialSegments) * Mathf.PI * 2f;
                    Vector3 radialDir = Mathf.Cos(angle) * normal + Mathf.Sin(angle) * binormal;
                    _meshVerts.Add(center + radialDir * r);
                    _meshNorms.Add(radialDir);
                    _meshUvs.Add(new Vector2(s / (float)RadialSegments, vCoord));
                }
            }

            for (int i = 0; i < ringCount - 1; i++)
            {
                int ringA = i * RadialSegments;
                int ringB = (i + 1) * RadialSegments;
                for (int s = 0; s < RadialSegments; s++)
                {
                    int nextS = (s + 1) % RadialSegments;
                    int a0 = ringA + s;
                    int a1 = ringA + nextS;
                    int b0 = ringB + s;
                    int b1 = ringB + nextS;

                    _meshTris.Add(a0);
                    _meshTris.Add(b0);
                    _meshTris.Add(a1);

                    _meshTris.Add(a1);
                    _meshTris.Add(b0);
                    _meshTris.Add(b1);
                }
            }

            _tubeMesh.Clear();
            _tubeMesh.SetVertices(_meshVerts);
            _tubeMesh.SetNormals(_meshNorms);
            _tubeMesh.SetUVs(0, _meshUvs);
            _tubeMesh.SetTriangles(_meshTris, 0);
            _tubeMesh.RecalculateBounds();
        }
    }
}
