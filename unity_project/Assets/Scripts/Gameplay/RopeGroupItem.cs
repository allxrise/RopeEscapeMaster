using System.Collections;
using System.Collections.Generic;
using RopeEscape.Config;
using RopeEscape.Rope;
using UnityEngine;

namespace RopeEscape.Gameplay
{
    /// <summary>
    /// Connects 2 to 5 Climber pins with 1 to 4 VerletRope segments.
    /// Monitors untangled state and executes smooth elimination when free of crossings after player interaction.
    /// </summary>
    public class RopeGroupItem : MonoBehaviour
    {
        public int GroupIndex { get; private set; }
        public RopeElementData Data { get; private set; }
        public SparkyGameNode Board { get; private set; }

        public readonly List<Climber> Climbers = new List<Climber>();
        public readonly List<VerletRope> RopeSegments = new List<VerletRope>();

        public bool IsEliminating { get; private set; }
        private float _untangledTimer;

        private const float UntangleHoldDuration = 0.28f;

        public void Initialize(int groupIndex, RopeElementData data, SparkyGameNode board)
        {
            GroupIndex = groupIndex;
            Data = data;
            Board = board;
            name = $"RopeGroup_{groupIndex}_Mat{data.materialType}";

            int color1 = data.cType1 > 0 ? data.cType1 : data.materialType;
            int color2 = data.cType2 > 0 ? data.cType2 : data.materialType;
            int color3 = data.cType3 > 0 ? data.cType3 : data.materialType;

            GridCell cell1 = board.GetCellByUidOrCoord(data.g1Uid, data.climberXCoord, data.climberYCoord);
            GridCell cell2 = board.GetCellByUidOrCoord(data.g2Uid, data.flagXCoord, data.flagYCoord);

            Climber c1 = CreateClimber("Climber_1", cell1, color1, data.fixed1, false, data.iIce);
            Climber c2 = CreateClimber("Climber_2", cell2, color2, data.fixed2, false, 0);
            Climbers.Add(c1);
            Climbers.Add(c2);

            int ropeMat1 = data.rType1 > 0 ? data.rType1 : data.materialType;
            VerletRope seg1 = CreateSegment("RopeSeg_1", c1.transform, c2.transform, data.particlePositionsData, ropeMat1);
            RopeSegments.Add(seg1);

            if (data.isThreeClimber || data.heads >= 3)
            {
                GridCell cell3 = board.GetCellByUidOrCoord(data.g3Uid, data.threeXCoord, data.threeYCoord);
                Climber c3 = CreateClimber("Climber_3", cell3, color3, data.fixed3, false, 0);
                Climbers.Add(c3);

                // In level configs, particlePositionsData2 is relative to Climber 3 (threeXCoord, threeYCoord) and ends at Climber 2 (flagXCoord, flagYCoord)
                int ropeMat2 = data.rType2 > 0 ? data.rType2 : data.materialType;
                VerletRope seg2 = CreateSegment("RopeSeg_2", c3.transform, c2.transform, data.particlePositionsData2, ropeMat2);
                RopeSegments.Add(seg2);

                if (data.heads >= 4)
                {
                    GridCell cell4 = board.GetOrCreateCell(data.foXCoord, data.foYCoord);
                    Climber c4 = CreateClimber("Climber_4", cell4, data.materialType, data.fixed4, false, 0);
                    Climbers.Add(c4);

                    int ropeMat3 = data.rType3 > 0 ? data.rType3 : data.materialType;
                    VerletRope seg3 = CreateSegment("RopeSeg_3", c4.transform, c3.transform, data.ppd3, ropeMat3);
                    RopeSegments.Add(seg3);

                    if (data.heads >= 5)
                    {
                        GridCell cell5 = board.GetOrCreateCell(data.fiXCoord, data.fiYCoord);
                        Climber c5 = CreateClimber("Climber_5", cell5, data.materialType, data.fixed5, false, 0);
                        Climbers.Add(c5);

                        VerletRope seg4 = CreateSegment("RopeSeg_4", c5.transform, c4.transform, data.ppd4, data.materialType);
                        RopeSegments.Add(seg4);
                    }
                }
            }

            // Attach key if specified in RopeElementData.keys
            if (data.keys != null && data.keys.Count > 0)
            {
                KeyInfo ki = data.keys[0];
                GameObject keyGo = new GameObject($"Key_{ki.type}");
                keyGo.transform.SetParent(transform, false);
                KeyItem keyItem = keyGo.AddComponent<KeyItem>();
                keyItem.Initialize(ki.type, c1.transform.position);
                c1.AttachedKey = keyItem;
            }
        }

        private Climber CreateClimber(
            string objName,
            GridCell cell,
            int colorType,
            bool isFixed,
            bool isGear,
            int iceCount)
        {
            GameObject go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            Climber climber = go.AddComponent<Climber>();
            climber.Initialize(this, cell, colorType, isFixed, isGear, iceCount);
            return climber;
        }

        private VerletRope CreateSegment(
            string objName,
            Transform startPin,
            Transform endPin,
            List<Vector3> offsets,
            int materialType)
        {
            GameObject go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            VerletRope rope = go.AddComponent<VerletRope>();
            rope.Initialize(startPin, endPin, offsets, materialType);
            return rope;
        }

        public bool CheckReadyToEliminate(float dt)
        {
            if (IsEliminating || Board == null || !Board.HasPlayerInteracted)
            {
                _untangledTimer = 0f;
                return false;
            }

            // Cannot auto-eliminate while any pin of this group is being dragged or frozen in ice
            for (int i = 0; i < Climbers.Count; i++)
            {
                Climber c = Climbers[i];
                if (c == null) continue;
                if (c.IsBeingDragged || c.IceCount > 0)
                {
                    _untangledTimer = 0f;
                    return false;
                }
            }

            // Check if all rope segments of this group have 0 crossings with other ropes and pillars
            for (int i = 0; i < RopeSegments.Count; i++)
            {
                VerletRope seg = RopeSegments[i];
                if (seg != null && seg.ActiveCrossingCount > 0)
                {
                    _untangledTimer = 0f;
                    return false;
                }
            }

            _untangledTimer += dt;
            return _untangledTimer >= UntangleHoldDuration;
        }

        public void EliminateImmediate()
        {
            if (IsEliminating)
                return;

            IsEliminating = true;
            for (int i = 0; i < RopeSegments.Count; i++)
            {
                if (RopeSegments[i] != null)
                    RopeSegments[i].StartEliminateRetract();
            }

            StartCoroutine(EliminateRoutine());
        }

        private IEnumerator EliminateRoutine()
        {
            // Free grid cells immediately so player can keep moving other pins fluidly
            for (int i = 0; i < Climbers.Count; i++)
            {
                Climber c = Climbers[i];
                if (c != null)
                {
                    if (c.AttachedKey != null && Board != null)
                    {
                        Board.UnlockMatchingLock(c.AttachedKey.KeyType);
                    }
                    if (c.CurrentCell != null && c.CurrentCell.CurrentClimber == c)
                    {
                        c.CurrentCell.CurrentClimber = null;
                    }
                }
            }

            const float duration = 0.20f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < RopeSegments.Count; i++)
                {
                    if (RopeSegments[i] != null)
                        RopeSegments[i].StepEliminateAnimation(t);
                }
                for (int i = 0; i < Climbers.Count; i++)
                {
                    if (Climbers[i] != null)
                        Climbers[i].transform.localScale = Vector3.one * (1f - t * 0.85f);
                }
                yield return null;
            }

            if (Board != null)
            {
                Board.OnRopeGroupEliminated(this);
            }
            Destroy(gameObject);
        }
    }
}
