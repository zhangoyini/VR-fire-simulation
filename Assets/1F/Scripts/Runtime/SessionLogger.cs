using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Harbourview.Journey
{
    /// <summary>
    /// Writes the anonymous behaviour log defined in Module D spec section 7.
    /// One JSON Lines file per session, local only, never linked to consent forms
    /// or participant identity (proposal requirement R7 / Scenario D section 6).
    /// </summary>
    public sealed class SessionLogger : MonoBehaviour
    {
        public static SessionLogger Instance { get; private set; }

        [SerializeField] private bool loggingEnabled = true;
        [SerializeField] private string logFolderName = "session-logs";

        private string _sessionId;
        private string _filePath;
        private float _startedAt;
        private StreamWriter _writer;
        private bool _closed;

        public string SessionId => _sessionId;
        public string LogFilePath => _filePath;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BeginSession();
        }

        private void OnApplicationQuit()
        {
            Flush();
        }

        private void OnDestroy()
        {
            Flush();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void BeginSession()
        {
            CloseWriter();

            _sessionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture).Substring(0, 16);
            _startedAt = Time.realtimeSinceStartup;

            if (!loggingEnabled)
            {
                return;
            }

            string directory = Path.Combine(Application.persistentDataPath, logFolderName);
            Directory.CreateDirectory(directory);
            _filePath = Path.Combine(directory, _sessionId + ".jsonl");
            _writer = new StreamWriter(File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
            _closed = false;

            Write("SESSION_START", "{\"appVersion\":\"" + Application.version + "\"}");
        }

        public void Log(string eventName, string payloadJson = "null")
        {
            if (!loggingEnabled || _closed || _writer == null)
            {
                return;
            }

            long elapsedMs = (long)((Time.realtimeSinceStartup - _startedAt) * 1000f);
            string state = JourneyDirector.Instance != null
                ? JourneyDirector.Instance.State.ToString()
                : JourneyState.Idle.ToString();

            var line = new StringBuilder();
            line.Append("{\"sessionId\":\"").Append(_sessionId)
                .Append("\",\"tMs\":").Append(elapsedMs.ToString(CultureInfo.InvariantCulture))
                .Append(",\"state\":\"").Append(state)
                .Append("\",\"event\":\"").Append(eventName)
                .Append("\",\"payload\":").Append(payloadJson)
                .Append('}');

            _writer.WriteLine(line.ToString());
        }

        public void Flush()
        {
            if (_writer != null)
            {
                _writer.Flush();
            }
        }

        private void CloseWriter()
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
                _closed = true;
            }
        }

        /// <summary>Lazily creates a logger so that a bare test scene still produces evidence.</summary>
        public static SessionLogger Ensure()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var host = new GameObject("SessionLogger") { hideFlags = HideFlags.DontSaveInEditor };
            var created = host.AddComponent<SessionLogger>();
            if (Instance != created)
            {
                Destroy(host);
            }

            return Instance;
        }

        public static void Write(string eventName)
        {
            var logger = Ensure();
            if (logger != null)
            {
                logger.Log(eventName);
            }
        }

        public static void Write(string eventName, string payloadJson)
        {
            var logger = Ensure();
            if (logger != null)
            {
                logger.Log(eventName, payloadJson);
            }
        }
    }
}
