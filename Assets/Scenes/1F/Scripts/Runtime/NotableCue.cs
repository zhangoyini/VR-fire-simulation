using System.Globalization;
using UnityEngine;

namespace Harbourview.Journey
{
    /// <summary>
    /// Marks an object as a cue the user is expected to notice (exit sign, assembly point board,
    /// evacuation map). Recognition is logged as SIGN_LOOKED_AT and feeds evaluation indicators 2
    /// and 3 in the proposal's assessment table. Uses an angle test rather than a raycast so it
    /// costs nothing on Quest 3 and never blocks interaction.
    /// </summary>
    public sealed class NotableCue : MonoBehaviour
    {
        [SerializeField] private string cueId = "";
        [SerializeField] private float angleThresholdDeg = 25f;
        [SerializeField] private float dwellToRecognise = 0.6f;
        [SerializeField] private bool recogniseOnce = true;

        private Camera _camera;
        private float _cameraAge;
        private float _dwell;
        private bool _recognised;

        public bool Recognised => _recognised;

        private void Reset()
        {
            cueId = gameObject.name;
        }

        private void Update()
        {
            if (_recognised && recogniseOnce)
            {
                enabled = false;
                return;
            }

            var cam = ResolveCamera();
            if (cam == null)
            {
                return;
            }

            Vector3 toCue = transform.position - cam.transform.position;
            float angle = Vector3.Angle(cam.transform.forward, toCue);

            if (angle <= angleThresholdDeg)
            {
                _dwell += Time.deltaTime;
                if (!_recognised && _dwell >= dwellToRecognise)
                {
                    _recognised = true;
                    Report((int)(_dwell * 1000f));
                }
            }
            else
            {
                _dwell = Mathf.Max(0f, _dwell - Time.deltaTime);
            }
        }

        private void Report(int dwellMs)
        {
            string payload = "{\"cueId\":\"" + cueId
                + "\",\"dwellMs\":" + dwellMs.ToString(CultureInfo.InvariantCulture) + "}";
            SessionLogger.Write("SIGN_LOOKED_AT", payload);
        }

        private Camera ResolveCamera()
        {
            if (_camera != null && _camera.gameObject.activeInHierarchy)
            {
                return _camera;
            }

            _cameraAge += Time.deltaTime;
            if (_cameraAge < 0.5f)
            {
                return null;
            }

            _cameraAge = 0f;
            _camera = Camera.main;
            return _camera;
        }
    }
}
