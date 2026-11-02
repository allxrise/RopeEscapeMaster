using UnityEngine;

namespace RopeEscape.Config
{
    /// <summary>
    /// Board and camera constants matching the original GameConst class.
    /// Notice GirdSize = 3.0f (confirmed from level JSON particlePositionsData offsets).
    /// </summary>
    public static class GameConst
    {
        public const float ResolutionCameraSize = 23.0f;
        public const float ResolutionWidth = 1080.0f;
        public const float ResolutionHeight = 1920.0f;
        public const float GirdSize = 3.0f;
        public const int MaxRow = 10;
        public const int MaxCol = 6;
        public const float MaxChangeLength = 15.0f;
        public const float ReviveAddTimes = 30.0f;

        /// <summary>
        /// Converts 1-based or 0-based grid coordinates (x, y) into centered 3D board world coordinates on XY plane (z = 0).
        /// </summary>
        public static Vector3 GridToWorld(int x, int y, float centerX, float centerY)
        {
            float wx = (x - centerX) * GirdSize;
            float wy = (y - centerY) * GirdSize;
            return new Vector3(wx, wy, 0f);
        }

        /// <summary>
        /// Returns a distinct, vibrant material color for each RopeElementData.materialType (1..12).
        /// </summary>
        public static Color GetRopeColor(int materialType)
        {
            switch (materialType)
            {
                case 1: return new Color(0.95f, 0.24f, 0.24f); // Red
                case 2: return new Color(0.20f, 0.78f, 0.35f); // Green
                case 3: return new Color(0.22f, 0.52f, 0.96f); // Blue
                case 4: return new Color(0.98f, 0.80f, 0.15f); // Yellow
                case 5: return new Color(0.18f, 0.85f, 0.88f); // Cyan
                case 6: return new Color(0.92f, 0.30f, 0.78f); // Magenta / Pink
                case 7: return new Color(0.98f, 0.52f, 0.14f); // Orange
                case 8: return new Color(0.62f, 0.32f, 0.92f); // Purple
                case 9: return new Color(0.55f, 0.88f, 0.22f); // Lime
                case 10: return new Color(0.96f, 0.45f, 0.55f); // Coral
                case 11: return new Color(0.35f, 0.70f, 0.75f); // Teal
                default: return new Color(0.88f, 0.88f, 0.92f); // Silver / White
            }
        }
    }
}
