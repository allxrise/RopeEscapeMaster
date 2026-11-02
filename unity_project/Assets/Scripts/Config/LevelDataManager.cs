using System;
using System.Collections.Generic;
using UnityEngine;

namespace RopeEscape.Config
{
    /// <summary>
    /// Offline-only level and shape configuration loader.
    /// Zero CDN dependencies: loads embedded level configs directly from Resources/Configs/Level/{levelId}.
    /// </summary>
    public static class LevelDataManager
    {
        public const int MaxEmbeddedLevels = 200;

        private static readonly Dictionary<int, GameLevelConfig> _levelCache = new Dictionary<int, GameLevelConfig>();
        private static Dictionary<int, ShapeConfigEntry> _shapeMap;
        private static HashSet<int> _difficultLevels;

        public static GameLevelConfig GetLevelConfig(int levelId)
        {
            int wrappedId = levelId;
            if (wrappedId < 1)
                wrappedId = 1;
            else if (wrappedId > MaxEmbeddedLevels)
                wrappedId = ((wrappedId - 1) % MaxEmbeddedLevels) + 1;

            if (_levelCache.TryGetValue(wrappedId, out GameLevelConfig cached))
                return cached;

            string resourcePath = $"Configs/Level/{wrappedId}";
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
            {
                try
                {
                    string json = StringCompressor.BrotliDecompress(asset.text);
                    GameLevelConfig config = JsonUtility.FromJson<GameLevelConfig>(json);
                    if (config != null && config.ropeElementsDatas != null && config.ropeElementsDatas.Count > 0)
                    {
                        _levelCache[wrappedId] = config;
                        return config;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LevelDataManager] Failed to parse {resourcePath}: {e.Message}");
                }
            }

            GameLevelConfig fallback = CreateProceduralFallbackLevel(wrappedId);
            _levelCache[wrappedId] = fallback;
            return fallback;
        }

        public static ShapeConfigEntry GetShapeConfig(int shapeId)
        {
            EnsureShapesLoaded();
            if (_shapeMap != null && _shapeMap.TryGetValue(shapeId, out ShapeConfigEntry entry))
                return entry;
            return null;
        }

        public static bool IsDifficultLevel(int levelId)
        {
            EnsureDifficultLevelsLoaded();
            return _difficultLevels != null && _difficultLevels.Contains(levelId);
        }

        private static void EnsureShapesLoaded()
        {
            if (_shapeMap != null)
                return;

            _shapeMap = new Dictionary<int, ShapeConfigEntry>();
            TextAsset asset = Resources.Load<TextAsset>("Configs/ShapeConfig");
            if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
            {
                try
                {
                    string wrappedJson = "{\"items\":" + asset.text.Trim() + "}";
                    ShapeConfigListWrapper wrapper = JsonUtility.FromJson<ShapeConfigListWrapper>(wrappedJson);
                    if (wrapper != null && wrapper.items != null)
                    {
                        foreach (ShapeConfigEntry item in wrapper.items)
                        {
                            _shapeMap[item.ShapeId] = item;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LevelDataManager] Failed to load ShapeConfig: {e.Message}");
                }
            }
        }

        private static void EnsureDifficultLevelsLoaded()
        {
            if (_difficultLevels != null)
                return;

            _difficultLevels = new HashSet<int>();
            TextAsset asset = Resources.Load<TextAsset>("Configs/difficultLevel");
            if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
            {
                try
                {
                    DifficultLevelConfig cfg = JsonUtility.FromJson<DifficultLevelConfig>(asset.text);
                    if (cfg != null && cfg.difflevels != null)
                    {
                        foreach (int id in cfg.difflevels)
                            _difficultLevels.Add(id);
                    }
                }
                catch (Exception)
                {
                    // Ignore parse failure
                }
            }
        }

        private static GameLevelConfig CreateProceduralFallbackLevel(int levelId)
        {
            GameLevelConfig cfg = new GameLevelConfig
            {
                levelId = levelId,
                width = 6,
                height = 10,
                levelTime = 60f
            };
            for (int x = 1; x <= 4; x++)
            {
                for (int y = 2; y <= 6; y++)
                {
                    cfg.validCells.Add(new ValidCell(x, y));
                }
            }
            cfg.ropeElementsDatas.Add(new RopeElementData
            {
                climberXCoord = 2,
                climberYCoord = 5,
                flagXCoord = 3,
                flagYCoord = 3,
                materialType = 3,
                particlePositionsData = new List<Vector3>
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(1f, -2f, -0.35f),
                    new Vector3(2f, -4f, -0.35f),
                    new Vector3(3f, -6f, 0f)
                }
            });
            cfg.ropeElementsDatas.Add(new RopeElementData
            {
                climberXCoord = 3,
                climberYCoord = 5,
                flagXCoord = 2,
                flagYCoord = 3,
                materialType = 2,
                particlePositionsData = new List<Vector3>
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(-1f, -2f, 0.05f),
                    new Vector3(-2f, -4f, 0.05f),
                    new Vector3(-3f, -6f, 0f)
                }
            });
            return cfg;
        }
    }
}
