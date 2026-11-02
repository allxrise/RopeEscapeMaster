using System;
using UnityEngine;

namespace RopeEscape.Telemetry
{
    /// <summary>
    /// Configuration for the custom first-party telemetry client.
    /// Loaded locally from Resources/TelemetryConfig.json with zero third-party SDK dependencies.
    /// </summary>
    [Serializable]
    public class TelemetryConfig
    {
        public string endpointUrl = "http://127.0.0.1:8080/api/v1/telemetry/batch";
        public string apiKey = "first-party-dev-key";
        public string appId = "com.firstparty.rope.escape";
        public string environment = "production-offline-ready";
        public bool enableTelemetry = true;
        public bool enableNetworkFlush = false;
        public bool persistOfflineQueue = true;
        public int batchSize = 20;
        public float flushIntervalSeconds = 15.0f;
        public int maxQueueSize = 1000;

        public static TelemetryConfig LoadFromResources()
        {
            TextAsset asset = Resources.Load<TextAsset>("TelemetryConfig");
            if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
            {
                try
                {
                    TelemetryConfig cfg = JsonUtility.FromJson<TelemetryConfig>(asset.text);
                    if (cfg != null)
                        return cfg;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[TelemetryConfig] Failed to parse TelemetryConfig.json: {e.Message}");
                }
            }
            return new TelemetryConfig();
        }
    }

    [Serializable]
    public class TelemetryEvent
    {
        public string eventId;
        public string eventName;
        public string sessionId;
        public string deviceId;
        public long timestampUnixMs;
        public int levelId;
        public string payloadJson;
    }

    [Serializable]
    public class TelemetryBatchPayload
    {
        public string appId;
        public string environment;
        public string platform;
        public string appVersion;
        public string sessionId;
        public string deviceId;
        public long sentAtUnixMs;
        public TelemetryEvent[] events;
    }
}
