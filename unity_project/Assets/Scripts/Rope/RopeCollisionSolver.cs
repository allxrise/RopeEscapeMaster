using System.Collections.Generic;
using UnityEngine;

namespace RopeEscape.Rope
{
    /// <summary>
    /// Handles 3D capsule-to-capsule segment collision resolution, topological over/under Z-order locking,
    /// pillar obstacle avoidance, and accurate crossing/pillar-wrap detection.
    /// </summary>
    public static class RopeCollisionSolver
    {
        private static readonly Dictionary<long, float> _crossingZSign = new Dictionary<long, float>();

        public static void ResetState()
        {
            _crossingZSign.Clear();
        }

        public static void StepSimulation(
            List<VerletRope> ropes,
            List<Vector3> pillarWorldPositions,
            float dt)
        {
            if (ropes == null || ropes.Count == 0)
                return;

            float clampedDt = Mathf.Min(dt, 0.022f);

            for (int i = 0; i < ropes.Count; i++)
            {
                VerletRope r = ropes[i];
                if (r != null && !r.IsEliminating)
                {
                    r.StepVerlet(clampedDt);
                }
            }

            const int solverIterations = 10;
            for (int iter = 0; iter < solverIterations; iter++)
            {
                for (int i = 0; i < ropes.Count; i++)
                {
                    VerletRope r = ropes[i];
                    if (r != null && !r.IsEliminating)
                    {
                        r.SolveInternalConstraints();
                        if (pillarWorldPositions != null && pillarWorldPositions.Count > 0)
                        {
                            SolvePillarCollisions(r, pillarWorldPositions);
                        }
                    }
                }

                SolveInterRopeCollisions(ropes);
            }

            UpdateCrossingCounts(ropes, pillarWorldPositions);

            for (int i = 0; i < ropes.Count; i++)
            {
                VerletRope r = ropes[i];
                if (r != null && !r.IsEliminating)
                {
                    r.RebuildTubeMesh();
                }
            }
        }

        private static void SolvePillarCollisions(VerletRope rope, List<Vector3> pillars)
        {
            const float pillarRadius = 1.18f;
            float minDist = pillarRadius + rope.RopeRadius;
            float minDistSq = minDist * minDist;

            for (int i = 1; i < rope.Particles.Length - 1; i++)
            {
                Vector3 p = rope.Particles[i].Position;
                for (int k = 0; k < pillars.Count; k++)
                {
                    Vector3 c = pillars[k];
                    float dx = p.x - c.x;
                    float dy = p.y - c.y;
                    float dSq = dx * dx + dy * dy;
                    if (dSq < minDistSq && dSq > 1e-6f)
                    {
                        float d = Mathf.Sqrt(dSq);
                        float push = minDist - d;
                        p.x += (dx / d) * push;
                        p.y += (dy / d) * push;
                        rope.Particles[i].Position = p;
                    }
                }
            }
        }

        private static void SolveInterRopeCollisions(List<VerletRope> ropes)
        {
            int count = ropes.Count;
            for (int rA = 0; rA < count; rA++)
            {
                VerletRope ropeA = ropes[rA];
                if (ropeA == null || ropeA.IsEliminating || ropeA.Particles == null)
                    continue;

                for (int rB = rA + 1; rB < count; rB++)
                {
                    VerletRope ropeB = ropes[rB];
                    if (ropeB == null || ropeB.IsEliminating || ropeB.Particles == null)
                        continue;

                    bool sharePin =
                        ropeA.StartPin == ropeB.StartPin ||
                        ropeA.StartPin == ropeB.EndPin ||
                        ropeA.EndPin == ropeB.StartPin ||
                        ropeA.EndPin == ropeB.EndPin;

                    float minDist = (ropeA.RopeRadius + ropeB.RopeRadius) * 1.06f;
                    float minDistSq = minDist * minDist;

                    int nA = ropeA.Particles.Length;
                    int nB = ropeB.Particles.Length;
                    long pairBase = (((long)(rA + 1)) << 32) | ((long)(rB + 1) << 16);

                    for (int i = 0; i < nA - 1; i++)
                    {
                        for (int j = 0; j < nB - 1; j++)
                        {
                            if (sharePin)
                            {
                                Vector3 aStart = ropeA.Particles[i].Position;
                                Vector3 bStart = ropeB.Particles[j].Position;
                                if (((i <= 1 || i >= nA - 3) && (j <= 1 || j >= nB - 3)) &&
                                    (aStart - bStart).sqrMagnitude < 0.85f)
                                {
                                    continue;
                                }
                            }

                            Vector3 a0 = ropeA.Particles[i].Position;
                            Vector3 a1 = ropeA.Particles[i + 1].Position;
                            Vector3 b0 = ropeB.Particles[j].Position;
                            Vector3 b1 = ropeB.Particles[j + 1].Position;

                            ClosestPointsOnTwoSegments(a0, a1, b0, b1, out float s, out float t, out Vector3 cA, out Vector3 cB);

                            Vector3 delta = cB - cA;
                            float dSq = delta.sqrMagnitude;
                            long segKey = pairBase | (long)((i << 8) | j);

                            // Check 2D XY intersection to preserve over/under Z topology
                            bool crosses2D = SegmentsIntersect2D(
                                new Vector2(a0.x, a0.y), new Vector2(a1.x, a1.y),
                                new Vector2(b0.x, b0.y), new Vector2(b1.x, b1.y));

                            if (crosses2D)
                            {
                                if (!_crossingZSign.TryGetValue(segKey, out float zSign))
                                {
                                    zSign = (cB.z >= cA.z) ? 1f : -1f;
                                    _crossingZSign[segKey] = zSign;
                                }

                                // Enforce minimum Z separation at 2D crossings so ropes cannot pass through each other
                                float desiredZDiff = zSign * (minDist * 0.90f);
                                float currentZDiff = cB.z - cA.z;
                                if (zSign > 0f && currentZDiff < desiredZDiff)
                                {
                                    float zCorr = (desiredZDiff - currentZDiff) * 0.5f;
                                    ApplySegmentCorrection(ropeA, i, s, new Vector3(0f, 0f, -zCorr));
                                    ApplySegmentCorrection(ropeB, j, t, new Vector3(0f, 0f, zCorr));
                                    a0 = ropeA.Particles[i].Position;
                                    a1 = ropeA.Particles[i + 1].Position;
                                    b0 = ropeB.Particles[j].Position;
                                    b1 = ropeB.Particles[j + 1].Position;
                                    cA = Vector3.Lerp(a0, a1, s);
                                    cB = Vector3.Lerp(b0, b1, t);
                                    delta = cB - cA;
                                    dSq = delta.sqrMagnitude;
                                }
                                else if (zSign < 0f && currentZDiff > desiredZDiff)
                                {
                                    float zCorr = (currentZDiff - desiredZDiff) * 0.5f;
                                    ApplySegmentCorrection(ropeA, i, s, new Vector3(0f, 0f, zCorr));
                                    ApplySegmentCorrection(ropeB, j, t, new Vector3(0f, 0f, -zCorr));
                                    a0 = ropeA.Particles[i].Position;
                                    a1 = ropeA.Particles[i + 1].Position;
                                    b0 = ropeB.Particles[j].Position;
                                    b1 = ropeB.Particles[j + 1].Position;
                                    cA = Vector3.Lerp(a0, a1, s);
                                    cB = Vector3.Lerp(b0, b1, t);
                                    delta = cB - cA;
                                    dSq = delta.sqrMagnitude;
                                }
                            }

                            if (dSq < minDistSq && dSq > 1e-7f)
                            {
                                float d = Mathf.Sqrt(dSq);
                                Vector3 dir = delta / d;
                                if (_crossingZSign.TryGetValue(segKey, out float lockedSign))
                                {
                                    if (lockedSign > 0f && dir.z < 0.35f)
                                    {
                                        dir.z = 0.55f;
                                        dir.Normalize();
                                    }
                                    else if (lockedSign < 0f && dir.z > -0.35f)
                                    {
                                        dir.z = -0.55f;
                                        dir.Normalize();
                                    }
                                }
                                else if (Mathf.Abs(dir.z) < 0.35f)
                                {
                                    dir.z = (cB.z >= cA.z) ? 0.45f : -0.45f;
                                    dir.Normalize();
                                }

                                float corrMag = (minDist - d) * 0.55f;
                                Vector3 corr = dir * corrMag;
                                ApplySegmentCorrection(ropeA, i, s, -corr);
                                ApplySegmentCorrection(ropeB, j, t, corr);
                            }
                        }
                    }
                }
            }
        }

        private static void ApplySegmentCorrection(VerletRope rope, int segIdx, float param, Vector3 correction)
        {
            float w0 = rope.Particles[segIdx].InvMass * (1f - param);
            float w1 = rope.Particles[segIdx + 1].InvMass * param;
            float wSum = w0 + w1;
            if (wSum <= 1e-5f)
                return;

            if (w0 > 0f)
            {
                Vector3 p0 = rope.Particles[segIdx].Position + correction * (w0 / wSum);
                p0.z = Mathf.Clamp(p0.z, -4.20f, -0.26f);
                rope.Particles[segIdx].Position = p0;
            }
            if (w1 > 0f)
            {
                Vector3 p1 = rope.Particles[segIdx + 1].Position + correction * (w1 / wSum);
                p1.z = Mathf.Clamp(p1.z, -4.20f, -0.26f);
                rope.Particles[segIdx + 1].Position = p1;
            }
        }

        private static void ClosestPointsOnTwoSegments(
            Vector3 p1, Vector3 q1,
            Vector3 p2, Vector3 q2,
            out float s, out float t,
            out Vector3 c1, out Vector3 c2)
        {
            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);

            if (a <= 1e-6f && e <= 1e-6f)
            {
                s = t = 0f;
                c1 = p1;
                c2 = p2;
                return;
            }
            if (a <= 1e-6f)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-6f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    if (denom != 0f)
                        s = Mathf.Clamp01((b * f - c * e) / denom);
                    else
                        s = 0f;

                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }

            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
        }

        private static void UpdateCrossingCounts(List<VerletRope> ropes, List<Vector3> pillarWorldPositions)
        {
            int count = ropes.Count;
            for (int i = 0; i < count; i++)
            {
                if (ropes[i] != null)
                    ropes[i].ActiveCrossingCount = 0;
            }

            for (int rA = 0; rA < count; rA++)
            {
                VerletRope ropeA = ropes[rA];
                if (ropeA == null || ropeA.IsEliminating || ropeA.Particles == null)
                    continue;

                if (pillarWorldPositions != null && pillarWorldPositions.Count > 0 &&
                    DoesRopeWrapPillar(ropeA, pillarWorldPositions))
                {
                    ropeA.ActiveCrossingCount++;
                }

                for (int rB = rA + 1; rB < count; rB++)
                {
                    VerletRope ropeB = ropes[rB];
                    if (ropeB == null || ropeB.IsEliminating || ropeB.Particles == null)
                        continue;

                    if (DoRopesCrossInXY(ropeA, ropeB))
                    {
                        ropeA.ActiveCrossingCount++;
                        ropeB.ActiveCrossingCount++;
                    }
                }
            }
        }

        private static bool DoesRopeWrapPillar(VerletRope rope, List<Vector3> pillars)
        {
            int n = rope.Particles.Length;
            if (n < 3) return false;

            Vector2 start2D = new Vector2(rope.Particles[0].Position.x, rope.Particles[0].Position.y);
            Vector2 end2D = new Vector2(rope.Particles[n - 1].Position.x, rope.Particles[n - 1].Position.y);
            const float pillarCheckRadius = 1.65f;

            for (int k = 0; k < pillars.Count; k++)
            {
                Vector2 pCenter = new Vector2(pillars[k].x, pillars[k].y);
                // Check if straight line between pins passes through the pillar or if any interior particle is pressed against the pillar
                float lineDist = DistancePointToSegment2D(pCenter, start2D, end2D);
                if (lineDist < 1.25f)
                    return true;

                for (int i = 1; i < n - 1; i++)
                {
                    Vector2 pt = new Vector2(rope.Particles[i].Position.x, rope.Particles[i].Position.y);
                    if (Vector2.Distance(pt, pCenter) < pillarCheckRadius)
                    {
                        float dev = DistancePointToSegment2D(pt, start2D, end2D);
                        if (dev > 0.35f)
                            return true;
                    }
                }
            }
            return false;
        }

        private static float DistancePointToSegment2D(Vector2 pt, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-6f)
                return Vector2.Distance(pt, a);
            float t = Mathf.Clamp01(Vector2.Dot(pt - a, ab) / lenSq);
            Vector2 proj = a + ab * t;
            return Vector2.Distance(pt, proj);
        }

        private static bool DoRopesCrossInXY(VerletRope ropeA, VerletRope ropeB)
        {
            int nA = ropeA.Particles.Length;
            int nB = ropeB.Particles.Length;

            for (int i = 0; i < nA - 1; i++)
            {
                Vector2 a0 = new Vector2(ropeA.Particles[i].Position.x, ropeA.Particles[i].Position.y);
                Vector2 a1 = new Vector2(ropeA.Particles[i + 1].Position.x, ropeA.Particles[i + 1].Position.y);

                for (int j = 0; j < nB - 1; j++)
                {
                    // Skip shared pin endpoints in 3-climber multi-segment ropes
                    if ((i == 0 || i == nA - 2) && (j == 0 || j == nB - 2))
                    {
                        Vector2 b0Check = new Vector2(ropeB.Particles[j].Position.x, ropeB.Particles[j].Position.y);
                        Vector2 b1Check = new Vector2(ropeB.Particles[j + 1].Position.x, ropeB.Particles[j + 1].Position.y);
                        if (Vector2.Distance(a0, b0Check) < 0.30f ||
                            Vector2.Distance(a0, b1Check) < 0.30f ||
                            Vector2.Distance(a1, b0Check) < 0.30f ||
                            Vector2.Distance(a1, b1Check) < 0.30f)
                        {
                            continue;
                        }
                    }

                    Vector2 b0 = new Vector2(ropeB.Particles[j].Position.x, ropeB.Particles[j].Position.y);
                    Vector2 b1 = new Vector2(ropeB.Particles[j + 1].Position.x, ropeB.Particles[j + 1].Position.y);

                    if (SegmentsIntersect2D(a0, a1, b0, b1))
                        return true;
                }
            }
            return false;
        }

        private static bool SegmentsIntersect2D(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            float d1 = Cross2D(p3, p4, p1);
            float d2 = Cross2D(p3, p4, p2);
            float d3 = Cross2D(p1, p2, p3);
            float d4 = Cross2D(p1, p2, p4);

            if (((d1 > 1e-5f && d2 < -1e-5f) || (d1 < -1e-5f && d2 > 1e-5f)) &&
                ((d3 > 1e-5f && d4 < -1e-5f) || (d3 < -1e-5f && d4 > 1e-5f)))
            {
                return true;
            }
            return false;
        }

        private static float Cross2D(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }
    }
}
