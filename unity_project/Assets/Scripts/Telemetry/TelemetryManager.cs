using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RopeEscape.Telemetry
{
    /// <summary>
    /// First-party telemetry client.
    /// Replaces all third-party trackers (Adjust, Firebase, Facebook, AppLovin, Bugly, iPoseidon)
    /// with a single, self-hosted/first-party JSON batch reporter and offline ring-buffer queue.
    /// </summary>
    public class TelemetryManager : MonoBehaviour
    {
        private static TelemetryManager _instance;
        public static TelemetryManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("[FirstPartyTelemetry]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<TelemetryManager>();
                }
                return _instance;
            }
        }

        public TelemetryConfig Config { get; private set; }
        public string SessionId { get; private set; }
        public string DeviceId { get; private set; }
        public int TotalEventsLogged { get; private set; }
        public int TotalBatchesFlushed { get; private set; }
        public string LastEventSummary { get; private set; } = "None";

        private readonly List<TelemetryEvent> _queue = new List<TelemetryEvent>();
        private bool _isFlushing;
        private float _flushTimer;
        private string _queueFilePath;

        [Serializable]
        private class QueueWrapper
        {
            public List<TelemetryEvent> items = new List<TelemetryEvent>();
        }

        public int QueuedEventCount => _queue.Count;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            Config = TelemetryConfig.LoadFromResources();
            SessionId = Guid.NewGuid().ToString("N");
            DeviceId = GetOrCreateFirstPartyDeviceId();
            _queueFilePath = Path.Combine(Application.persistentDataPath, "first_party_telemetry_queue.json");

            LoadQueueFromDisk();
            TrackAppLaunch();
        }

        private void Update()
        {
            if (Config == null || !Config.enableTelemetry || !Config.enableNetworkFlush)
                return;

            _flushTimer += Time.unscaledDeltaTime;
            if (_flushTimer >= Mathf.Max(2f, Config.flushIntervalSeconds))
            {
                _flushTimer = 0f;
                FlushAsync();
            }
        }

        private string GetOrCreateFirstPartyDeviceId()
        {
            const string prefKey = "fp_telemetry_device_id";
            string id = PlayerPrefs.GetString(prefKey, string.Empty);
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(prefKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }

        public void ConfigureEndpoint(string endpointUrl, string apiKey, bool enableNetworkFlush)
        {
            Config.endpointUrl = endpointUrl;
            Config.apiKey = apiKey;
            Config.enableNetworkFlush = enableNetworkFlush;
        }

        public void TrackAppLaunch()
        {
            string json = $"{{\"platform\":\"{Application.platform}\",\"unityVersion\":\"{Application.unityVersion}\",\"screen\":\"{Screen.width}x{Screen.height}\"}}";
            EnqueueEvent("app_launch", 0, json);
        }

        public void TrackLevelStart(int levelId, int ropeCount, int gridWidth, int gridHeight, float levelTime)
        {
            string json = $"{{\"ropeCount\":{ropeCount},\"gridWidth\":{gridWidth},\"gridHeight\":{gridHeight},\"levelTime\":{levelTime:F1}}}";
            EnqueueEvent("level_start", levelId, json);
        }

        public void TrackPinMove(int levelId, int fromX, int fromY, int toX, int toY, int moveIndex)
        {
            string json = $"{{\"fromX\":{fromX},\"fromY\":{fromY},\"toX\":{toX},\"toY\":{toY},\"moveIndex\":{moveIndex}}}";
            EnqueueEvent("pin_move", levelId, json);
        }

        public void TrackRopeEliminate(int levelId, int ropeIndex, int remainingRopes, int comboCount, float elapsedSec)
        {
            string json = $"{{\"ropeIndex\":{ropeIndex},\"remainingRopes\":{remainingRopes},\"comboCount\":{comboCount},\"elapsedSec\":{elapsedSec:F2}}}";
            EnqueueEvent("rope_eliminate", levelId, json);
        }

        public void TrackSkillUsed(int levelId, string skillName)
        {
            string json = $"{{\"skillName\":\"{skillName}\"}}";
            EnqueueEvent("skill_used", levelId, json);
        }

        public void TrackLevelComplete(int levelId, float durationSec, int totalMoves, int maxCombo)
        {
            string json = $"{{\"durationSec\":{durationSec:F2},\"totalMoves\":{totalMoves},\"maxCombo\":{maxCombo}}}";
            EnqueueEvent("level_complete", levelId, json);
        }

        public void TrackLevelFail(int levelId, string reason, int remainingRopes, int totalMoves)
        {
            string json = $"{{\"reason\":\"{reason}\",\"remainingRopes\":{remainingRopes},\"totalMoves\":{totalMoves}}}";
            EnqueueEvent("level_fail", levelId, json);
        }

        public void EnqueueEvent(string eventName, int levelId, string payloadJson)
        {
            if (Config == null || !Config.enableTelemetry)
                return;

            TelemetryEvent ev = new TelemetryEvent
            {
                eventId = Guid.NewGuid().ToString("N"),
                eventName = eventName,
                sessionId = SessionId,
                deviceId = DeviceId,
                timestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                levelId = levelId,
                payloadJson = payloadJson ?? "{}"
            };

            if (_queue.Count >= Mathf.Max(50, Config.maxQueueSize))
            {
                _queue.RemoveAt(0);
            }

            _queue.Add(ev);
            TotalEventsLogged++;
            LastEventSummary = $"{eventName} (Lv.{levelId}) {payloadJson}";

            if (Config.persistOfflineQueue)
            {
                SaveQueueToDisk();
            }

            if (Config.enableNetworkFlush && _queue.Count >= Config.batchSize)
            {
                FlushAsync();
            }
        }

        public void FlushAsync()
        {
            if (_isFlushing || _queue.Count == 0 || Config == null || !Config.enableNetworkFlush)
                return;

            if (string.IsNullOrWhiteSpace(Config.endpointUrl))
                return;

            StartCoroutine(FlushCoroutine());
        }

        private IEnumerator FlushCoroutine()
        {
            _isFlushing = true;
            int count = Mathf.Min(_queue.Count, Mathf.Max(1, Config.batchSize));
            TelemetryEvent[] batchEvents = new TelemetryEvent[count];
            _queue.CopyTo(0, batchEvents, 0, count);

            TelemetryBatchPayload batch = new TelemetryBatchPayload
            {
                appId = Config.appId,
                environment = Config.environment,
                platform = Application.platform.ToString(),
                appVersion = Application.version,
                sessionId = SessionId,
                deviceId = DeviceId,
                sentAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                events = batchEvents
            };

            string bodyJson = JsonUtility.ToJson(batch);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

            using (UnityWebRequest req = new UnityWebRequest(Config.endpointUrl, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-App-Id", Config.appId ?? "com.firstparty.rope.escape");
                if (!string.IsNullOrEmpty(Config.apiKey))
                {
                    req.SetRequestHeader("X-Api-Key", Config.apiKey);
                }
                req.timeout = 8;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    _queue.RemoveRange(0, Mathf.Min(count, _queue.Count));
                    TotalBatchesFlushed++;
                    if (Config.persistOfflineQueue)
                    {
                        SaveQueueToDisk();
                    }
                }
            }

            _isFlushing = false;
        }

        private void SaveQueueToDisk()
        {
            try
            {
                QueueWrapper wrapper = new QueueWrapper { items = _queue };
                string json = JsonUtility.ToJson(wrapper);
                File.WriteAllText(_queueFilePath, json, Encoding.UTF8);
            }
            catch (Exception)
            {
                // Ignore disk I/O errors in restricted sandboxes
            }
        }

        private void LoadQueueFromDisk()
        {
            try
            {
                if (File.Exists(_queueFilePath))
                {
                    string json = File.ReadAllText(_queueFilePath, Encoding.UTF8);
                    QueueWrapper wrapper = JsonUtility.FromJson<QueueWrapper>(json);
                    if (wrapper != null && wrapper.items != null)
                    {
                        _queue.Clear();
                        _queue.AddRange(wrapper.items);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore corrupted queue file
            }
        }
    }
}
