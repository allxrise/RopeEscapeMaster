using RopeEscape.Telemetry;
using UnityEngine;

namespace RopeEscape.Gameplay
{
    /// <summary>
    /// Automatically initializes the 3D camera, lighting, board controller (SparkyGameNode),
    /// input handler (SparkGamePlayerInput), HUD (GameUIController), and first-party TelemetryManager
    /// when any scene starts.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            // Ensure first-party telemetry singleton is initialized
            _ = TelemetryManager.Instance;

            if (Object.FindAnyObjectByType<SparkyGameNode>() != null)
                return;

            // Ensure Main Camera exists and is configured for the 3D board
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.11f, 0.16f);
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            cam.transform.position = new Vector3(0f, -1.5f, -32f);

            // Ensure Directional Light exists
            if (Object.FindAnyObjectByType<Light>() == null)
            {
                GameObject lightGo = new GameObject("Directional Light");
                Light dirLight = lightGo.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.intensity = 1.15f;
                dirLight.color = new Color(1.0f, 0.98f, 0.94f);
                lightGo.transform.rotation = Quaternion.Euler(32f, -18f, 0f);
            }

            // Create main GameRoot with SparkyGameNode, SparkGamePlayerInput, and GameUIController
            GameObject rootGo = new GameObject("SparkyGameRoot");
            SparkyGameNode board = rootGo.AddComponent<SparkyGameNode>();
            SparkGamePlayerInput input = rootGo.AddComponent<SparkGamePlayerInput>();
            GameUIController ui = rootGo.AddComponent<GameUIController>();

            input.Initialize(board);
            ui.Initialize(board);
        }
    }
}
