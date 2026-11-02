using System;
using System.Collections.Generic;
using RopeEscape.Config;
using RopeEscape.Rope;
using RopeEscape.Telemetry;
using RopeEscape.Utils;
using UnityEngine;

namespace RopeEscape.Gameplay
{
    /// <summary>
    /// Core 3D board controller (mirrors original SparkyGameNode).
    /// Builds the board from local GameLevelConfig JSON, steps 3D Verlet rope physics,
    /// manages skills (Freeze, Cut Rope, Electric Fan), and reports to first-party TelemetryManager.
    /// </summary>
    public class SparkyGameNode : MonoBehaviour
    {
        public int CurrentLevelId { get; private set; } = 1;
        public GameLevelConfig CurrentConfig { get; private set; }
        public bool IsGameActive { get; private set; }
        public bool IsTimeFrozen { get; private set; }
        public bool IsCutRopeMode { get; set; }
        public bool HasPlayerInteracted { get; private set; }

        public float RemainingTime { get; private set; }
        public float FreezeRemainingTime { get; private set; }
        public float LevelElapsedTime { get; private set; }
        public int MoveCount { get; private set; }
        public int ComboCount { get; private set; }
        public int MaxCombo { get; private set; }
        public int Coins { get; private set; } = 2500;

        public event Action<int> OnLevelLoaded;
        public event Action<int, float, int> OnLevelWon;
        public event Action<int, string> OnLevelFailed;

        private readonly Dictionary<Vector2Int, GridCell> _cells = new Dictionary<Vector2Int, GridCell>();
        private readonly Dictionary<int, GridCell> _uidCells = new Dictionary<int, GridCell>();
        private readonly List<RopeGroupItem> _ropeGroups = new List<RopeGroupItem>();
        private readonly List<VerletRope> _allRopeSegments = new List<VerletRope>();
        private readonly List<Vector3> _pillarPositions = new List<Vector3>();
        private readonly List<LockItem> _locks = new List<LockItem>();

        private Transform _boardRoot;
        private float _centerX = 2.5f;
        private float _centerY = 4.0f;
        private float _comboResetTimer;

        public int ActiveRopeGroupCount => _ropeGroups.Count;
        public IReadOnlyDictionary<Vector2Int, GridCell> Cells => _cells;

        private void Awake()
        {
            CurrentLevelId = PlayerPrefs.GetInt("fp_current_level", 1);
            Coins = PlayerPrefs.GetInt("fp_player_coins", 2500);
        }

        private void Start()
        {
            LoadLevel(CurrentLevelId);
        }

        public void LoadLevel(int levelId)
        {
            CurrentLevelId = Mathf.Clamp(levelId, 1, LevelDataManager.MaxEmbeddedLevels);
            PlayerPrefs.SetInt("fp_current_level", CurrentLevelId);
            PlayerPrefs.Save();

            ClearBoard();
            RopeCollisionSolver.ResetState();

            CurrentConfig = LevelDataManager.GetLevelConfig(CurrentLevelId);
            _boardRoot = new GameObject($"Board_Lv{CurrentLevelId}").transform;
            _boardRoot.SetParent(transform, false);

            ComputeBoardCenter(CurrentConfig);
            CreateBackdropBoard();
            CreateGridMap(CurrentConfig);
            CreateObstacles(CurrentConfig);
            CreateRopeGroups(CurrentConfig);
            FrameCameraToBoard();

            RemainingTime = CurrentConfig.levelTime > 5f ? CurrentConfig.levelTime : 120f;
            FreezeRemainingTime = 0f;
            LevelElapsedTime = 0f;
            IsTimeFrozen = false;
            IsCutRopeMode = false;
            HasPlayerInteracted = false;
            MoveCount = 0;
            ComboCount = 0;
            MaxCombo = 0;
            _comboResetTimer = 0f;
            IsGameActive = true;

            // Pre-settle rope physics for 6 steps so initial crossing states and Z-layers are locked immediately
            for (int i = 0; i < 6; i++)
            {
                RopeCollisionSolver.StepSimulation(_allRopeSegments, _pillarPositions, 0.015f);
            }

            TelemetryManager.Instance.TrackLevelStart(
                CurrentLevelId,
                _ropeGroups.Count,
                CurrentConfig.width,
                CurrentConfig.height,
                RemainingTime);

            OnLevelLoaded?.Invoke(CurrentLevelId);
        }

        private void ClearBoard()
        {
            _cells.Clear();
            _uidCells.Clear();
            _ropeGroups.Clear();
            _allRopeSegments.Clear();
            _pillarPositions.Clear();
            _locks.Clear();

            if (_boardRoot != null)
            {
                Destroy(_boardRoot.gameObject);
                _boardRoot = null;
            }
        }

        private void ComputeBoardCenter(GameLevelConfig cfg)
        {
            if (cfg.grids != null && cfg.grids.Count > 0)
            {
                _centerX = 0f;
                _centerY = 0f;
                return;
            }

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            if (cfg.validCells != null)
            {
                for (int i = 0; i < cfg.validCells.Count; i++)
                {
                    ValidCell vc = cfg.validCells[i];
                    if (vc.Item1 < minX) minX = vc.Item1;
                    if (vc.Item1 > maxX) maxX = vc.Item1;
                    if (vc.Item2 < minY) minY = vc.Item2;
                    if (vc.Item2 > maxY) maxY = vc.Item2;
                }
            }

            if (cfg.ropeElementsDatas != null)
            {
                for (int i = 0; i < cfg.ropeElementsDatas.Count; i++)
                {
                    RopeElementData r = cfg.ropeElementsDatas[i];
                    minX = Mathf.Min(minX, r.climberXCoord, r.flagXCoord);
                    maxX = Mathf.Max(maxX, r.climberXCoord, r.flagXCoord);
                    minY = Mathf.Min(minY, r.climberYCoord, r.flagYCoord);
                    maxY = Mathf.Max(maxY, r.climberYCoord, r.flagYCoord);
                }
            }

            if (minX > maxX)
            {
                _centerX = 2.5f;
                _centerY = 4.0f;
            }
            else
            {
                _centerX = (minX + maxX) * 0.5f;
                _centerY = (minY + maxY) * 0.5f;
            }
        }

        private void CreateBackdropBoard()
        {
            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backdrop.name = "BoardBackdrop";
            Destroy(backdrop.GetComponent<Collider>());
            backdrop.transform.SetParent(_boardRoot, false);
            backdrop.transform.localPosition = new Vector3(0f, 0f, 0.80f);
            backdrop.transform.localScale = new Vector3(28f, 38f, 0.6f);

            MeshRenderer mr = backdrop.GetComponent<MeshRenderer>();
            mr.sharedMaterial = ObjMeshLoader.CreateColoredMaterial(new Color(0.14f, 0.17f, 0.25f), 0.25f, 0.05f);
        }

        private void CreateGridMap(GameLevelConfig cfg)
        {
            if (cfg.grids != null && cfg.grids.Count > 0)
            {
                for (int i = 0; i < cfg.grids.Count; i++)
                {
                    int uid = i + 1;
                    GridInfo gi = cfg.grids[i];
                    Vector3 worldPos = new Vector3(gi.x, gi.y, 0f);
                    Vector2Int key = new Vector2Int(1000 + uid, i);

                    GameObject go = new GameObject($"GridCell_UID_{uid}");
                    go.transform.SetParent(_boardRoot, false);
                    GridCell cell = go.AddComponent<GridCell>();
                    cell.Initialize(uid, i, worldPos);
                    _cells[key] = cell;
                    _uidCells[uid] = cell;
                }
            }

            if (cfg.validCells != null && cfg.validCells.Count > 0)
            {
                for (int i = 0; i < cfg.validCells.Count; i++)
                {
                    ValidCell vc = cfg.validCells[i];
                    GetOrCreateCell(vc.Item1, vc.Item2);
                }
            }

            if (cfg.shapes != null && cfg.shapes.Count > 0)
            {
                for (int i = 0; i < cfg.shapes.Count; i++)
                {
                    ShapeInfo si = cfg.shapes[i];
                    ShapeConfigEntry shapeCfg = LevelDataManager.GetShapeConfig(si.shapeId);
                    if (shapeCfg != null && shapeCfg.Points != null)
                    {
                        for (int p = 0; p < shapeCfg.Points.Count; p++)
                        {
                            ValidCell pt = shapeCfg.Points[p];
                            GetOrCreateCell(si.x + pt.Item1, si.y + pt.Item2);
                        }
                    }
                }
            }
        }

        public GridCell GetCellByUidOrCoord(int uid, int x, int y)
        {
            if (uid > 0 && _uidCells.TryGetValue(uid, out GridCell uidCell) && uidCell != null)
                return uidCell;

            return GetOrCreateCell(x, y);
        }

        public GridCell GetOrCreateCell(int x, int y)
        {
            Vector2Int key = new Vector2Int(x, y);
            if (_cells.TryGetValue(key, out GridCell existing) && existing != null)
                return existing;

            Vector3 worldPos = GameConst.GridToWorld(x, y, _centerX, _centerY);
            GameObject go = new GameObject($"GridCell_{x}_{y}");
            go.transform.SetParent(_boardRoot, false);
            GridCell cell = go.AddComponent<GridCell>();
            cell.Initialize(x, y, worldPos);
            _cells[key] = cell;
            return cell;
        }

        private void CreateObstacles(GameLevelConfig cfg)
        {
            if (cfg.pillarCells != null)
            {
                HashSet<Vector2Int> addedPillars = new HashSet<Vector2Int>();
                for (int i = 0; i < cfg.pillarCells.Count; i++)
                {
                    ValidCell pc = cfg.pillarCells[i];
                    Vector2Int key = new Vector2Int(pc.Item1, pc.Item2);
                    if (!addedPillars.Add(key))
                        continue;

                    GridCell cell = GetOrCreateCell(pc.Item1, pc.Item2);
                    GameObject pGo = new GameObject($"Pillar_{pc.Item1}_{pc.Item2}");
                    pGo.transform.SetParent(_boardRoot, false);
                    PillarItem pillar = pGo.AddComponent<PillarItem>();
                    pillar.Initialize(cell);
                    _pillarPositions.Add(cell.transform.position);
                }
            }

            if (cfg.keys != null)
            {
                for (int i = 0; i < cfg.keys.Count; i++)
                {
                    KeyInfo ki = cfg.keys[i];
                    GridCell cell = GetOrCreateCell(ki.x, ki.y);
                    if (!ki.isKey)
                    {
                        GameObject lGo = new GameObject($"Lock_{ki.x}_{ki.y}");
                        lGo.transform.SetParent(_boardRoot, false);
                        LockItem lockItem = lGo.AddComponent<LockItem>();
                        lockItem.Initialize(ki.type, cell);
                        _locks.Add(lockItem);
                    }
                }
            }

            if (cfg.rods != null)
            {
                for (int i = 0; i < cfg.rods.Count; i++)
                {
                    RodInfo ri = cfg.rods[i];
                    Vector3 pos = GameConst.GridToWorld(Mathf.RoundToInt(ri.x), Mathf.RoundToInt(ri.y), _centerX, _centerY);
                    GameObject rGo = new GameObject($"Rod_{i}");
                    rGo.transform.SetParent(_boardRoot, false);
                    RopeRodItem rod = rGo.AddComponent<RopeRodItem>();
                    rod.Initialize(ri, pos);
                }
            }
        }

        private void CreateRopeGroups(GameLevelConfig cfg)
        {
            if (cfg.ropeElementsDatas == null)
                return;

            for (int i = 0; i < cfg.ropeElementsDatas.Count; i++)
            {
                RopeElementData rData = cfg.ropeElementsDatas[i];
                GameObject groupGo = new GameObject($"RopeGroup_{i}");
                groupGo.transform.SetParent(_boardRoot, false);
                RopeGroupItem group = groupGo.AddComponent<RopeGroupItem>();
                group.Initialize(i, rData, this);

                _ropeGroups.Add(group);
                _allRopeSegments.AddRange(group.RopeSegments);
            }
        }

        private void FrameCameraToBoard()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float minWorldX = float.MaxValue, maxWorldX = float.MinValue;
            float minWorldY = float.MaxValue, maxWorldY = float.MinValue;

            foreach (GridCell cell in _cells.Values)
            {
                if (cell == null) continue;
                Vector3 p = cell.transform.position;
                if (p.x < minWorldX) minWorldX = p.x;
                if (p.x > maxWorldX) maxWorldX = p.x;
                if (p.y < minWorldY) minWorldY = p.y;
                if (p.y > maxWorldY) maxWorldY = p.y;
            }

            if (minWorldX > maxWorldX)
            {
                minWorldX = -6f;
                maxWorldX = 6f;
                minWorldY = -9f;
                maxWorldY = 9f;
            }

            float centerX = (minWorldX + maxWorldX) * 0.5f;
            float centerY = (minWorldY + maxWorldY) * 0.5f;
            float boardSpanX = Mathf.Max(12f, (maxWorldX - minWorldX) + 6.5f);
            float boardSpanY = Mathf.Max(18f, (maxWorldY - minWorldY) + 9.5f);
            float aspect = Mathf.Max(0.45f, (float)Screen.width / Mathf.Max(1, Screen.height));

            float distanceForHeight = (boardSpanY * 0.68f) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float distanceForWidth = (boardSpanX * 0.68f) / (Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * aspect);
            float camZ = -Mathf.Clamp(Mathf.Max(distanceForHeight, distanceForWidth), 22f, 46f);

            cam.transform.position = new Vector3(centerX, centerY - 1.2f, camZ);
            cam.transform.rotation = Quaternion.Euler(-3.0f, 0f, 0f);
        }

        private void Update()
        {
            if (!IsGameActive)
                return;

            float dt = Time.deltaTime;
            LevelElapsedTime += dt;

            // Update freeze timer & level countdown
            if (IsTimeFrozen)
            {
                FreezeRemainingTime -= dt;
                if (FreezeRemainingTime <= 0f)
                {
                    FreezeRemainingTime = 0f;
                    IsTimeFrozen = false;
                }
            }
            else
            {
                RemainingTime -= dt;
                if (RemainingTime <= 0f)
                {
                    RemainingTime = 0f;
                    TriggerLevelFail("time_out");
                    return;
                }
            }

            if (ComboCount > 0)
            {
                _comboResetTimer -= dt;
                if (_comboResetTimer <= 0f)
                {
                    ComboCount = 0;
                }
            }

            // Step 3D Verlet rope simulation and collision resolution
            RopeCollisionSolver.StepSimulation(_allRopeSegments, _pillarPositions, dt);

            // Check if any rope group is untangled and ready to eliminate (only after player interaction)
            if (HasPlayerInteracted)
            {
                for (int i = _ropeGroups.Count - 1; i >= 0; i--)
                {
                    RopeGroupItem grp = _ropeGroups[i];
                    if (grp != null && !grp.IsEliminating && grp.CheckReadyToEliminate(dt))
                    {
                        grp.EliminateImmediate();
                    }
                }
            }
        }

        public GridCell FindNearestAvailableCell(Vector3 worldPos, GridCell currentCell, float maxRadius = 2.5f)
        {
            GridCell best = null;
            float bestDistSq = maxRadius * maxRadius;

            foreach (KeyValuePair<Vector2Int, GridCell> kv in _cells)
            {
                GridCell cell = kv.Value;
                if (cell == null) continue;
                if (cell != currentCell && !cell.IsAvailableForMove)
                    continue;

                float dx = worldPos.x - cell.transform.position.x;
                float dy = worldPos.y - cell.transform.position.y;
                float dSq = dx * dx + dy * dy;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = cell;
                }
            }
            return best;
        }

        public void RecordPinMove(int fromX, int fromY, int toX, int toY)
        {
            if (fromX == toX && fromY == toY)
                return;

            HasPlayerInteracted = true;
            MoveCount++;
            TelemetryManager.Instance.TrackPinMove(CurrentLevelId, fromX, fromY, toX, toY, MoveCount);
        }

        public void UnlockMatchingLock(int keyType)
        {
            for (int i = _locks.Count - 1; i >= 0; i--)
            {
                LockItem lk = _locks[i];
                if (lk != null && (lk.LockType == keyType || keyType == 0))
                {
                    lk.Unlock();
                    _locks.RemoveAt(i);
                    break;
                }
            }
        }

        public void OnRopeGroupEliminated(RopeGroupItem group)
        {
            if (group != null)
            {
                for (int i = 0; i < group.RopeSegments.Count; i++)
                {
                    _allRopeSegments.Remove(group.RopeSegments[i]);
                }
                _ropeGroups.Remove(group);
            }

            // Crack 1 layer of ice on all frozen pins across the board
            for (int i = 0; i < _ropeGroups.Count; i++)
            {
                RopeGroupItem g = _ropeGroups[i];
                if (g == null) continue;
                for (int c = 0; c < g.Climbers.Count; c++)
                {
                    if (g.Climbers[c] != null && g.Climbers[c].IceCount > 0)
                    {
                        g.Climbers[c].DecrementIce();
                    }
                }
            }

            ComboCount++;
            if (ComboCount > MaxCombo)
                MaxCombo = ComboCount;
            _comboResetTimer = 4.0f;

            Coins += 10 * Mathf.Max(1, ComboCount);
            PlayerPrefs.SetInt("fp_player_coins", Coins);

            TelemetryManager.Instance.TrackRopeEliminate(
                CurrentLevelId,
                group != null ? group.GroupIndex : -1,
                _ropeGroups.Count,
                ComboCount,
                LevelElapsedTime);

            if (_ropeGroups.Count == 0 && IsGameActive)
            {
                IsGameActive = false;
                Coins += 50;
                PlayerPrefs.SetInt("fp_player_coins", Coins);
                PlayerPrefs.Save();

                TelemetryManager.Instance.TrackLevelComplete(
                    CurrentLevelId,
                    LevelElapsedTime,
                    MoveCount,
                    MaxCombo);

                OnLevelWon?.Invoke(CurrentLevelId, LevelElapsedTime, MoveCount);
            }
        }

        private void TriggerLevelFail(string reason)
        {
            if (!IsGameActive)
                return;

            IsGameActive = false;
            TelemetryManager.Instance.TrackLevelFail(CurrentLevelId, reason, _ropeGroups.Count, MoveCount);
            OnLevelFailed?.Invoke(CurrentLevelId, reason);
        }

        public void ReviveWithExtraTime()
        {
            RemainingTime += GameConst.ReviveAddTimes;
            IsGameActive = true;
            TelemetryManager.Instance.TrackSkillUsed(CurrentLevelId, "REVIVE_EXTRA_TIME");
        }

        /// <summary>
        /// Skill 1: Freezes countdown timer for 15 seconds (mirrors SkillFreezingTime).
        /// </summary>
        public void SkillFreezingTime()
        {
            if (!IsGameActive) return;
            IsTimeFrozen = true;
            FreezeRemainingTime += 15f;
            TelemetryManager.Instance.TrackSkillUsed(CurrentLevelId, "FREEZE_TIME");
        }

        /// <summary>
        /// Skill 2: Cuts the most tangled rope group (or toggles Cut Rope mode) and marks player interaction.
        /// </summary>
        public void SkillCutRope()
        {
            if (!IsGameActive || _ropeGroups.Count == 0) return;

            HasPlayerInteracted = true;
            RopeGroupItem target = _ropeGroups[0];
            int bestCrossings = -1;
            for (int i = 0; i < _ropeGroups.Count; i++)
            {
                RopeGroupItem g = _ropeGroups[i];
                if (g == null || g.IsEliminating) continue;
                int crossings = 0;
                for (int s = 0; s < g.RopeSegments.Count; s++)
                {
                    if (g.RopeSegments[s] != null)
                        crossings += g.RopeSegments[s].ActiveCrossingCount;
                }
                if (crossings > bestCrossings)
                {
                    bestCrossings = crossings;
                    target = g;
                }
            }

            if (target != null)
            {
                TelemetryManager.Instance.TrackSkillUsed(CurrentLevelId, "CUT_ROPE");
                target.EliminateImmediate();
            }
        }

        /// <summary>
        /// Skill 3: Electric Fan — blows movable pins outward to open empty cells to help untangle (mirrors SkillElectricFan).
        /// </summary>
        public void SkillElectricFan()
        {
            if (!IsGameActive || _ropeGroups.Count == 0) return;

            HasPlayerInteracted = true;
            List<GridCell> emptyCells = new List<GridCell>();
            foreach (GridCell c in _cells.Values)
            {
                if (c != null && c.IsAvailableForMove)
                    emptyCells.Add(c);
            }

            int moved = 0;
            for (int i = 0; i < _ropeGroups.Count && emptyCells.Count > 0; i++)
            {
                RopeGroupItem g = _ropeGroups[i];
                if (g == null || g.IsEliminating) continue;
                for (int c = 0; c < g.Climbers.Count && emptyCells.Count > 0; c++)
                {
                    Climber pin = g.Climbers[c];
                    if (pin != null && pin.CanDrag)
                    {
                        int pickIdx = UnityEngine.Random.Range(0, emptyCells.Count);
                        GridCell targetCell = emptyCells[pickIdx];
                        emptyCells.RemoveAt(pickIdx);
                        if (pin.CurrentCell != null)
                            emptyCells.Add(pin.CurrentCell);
                        pin.SnapToCell(targetCell);
                        moved++;
                        if (moved >= 3) break;
                    }
                }
                if (moved >= 3) break;
            }

            TelemetryManager.Instance.TrackSkillUsed(CurrentLevelId, "ELECTRIC_FAN");
        }
    }
}
