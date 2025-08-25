using UnityEngine;

namespace HyperVoxel
{
    public class PlayerFlyController : MonoBehaviour
    {
        [Header("Look")]
        public float mouseSensitivity = 0.2f;
        public bool invertY = false;

        [Header("Move")]
        public float moveSpeed = 12f;
        public float fastMultiplier = 3f;
        public float slowMultiplier = 0.3f;

        [Header("Keys")]
        public KeyCode ascendKey = KeyCode.Space;
        public KeyCode descendKey = KeyCode.LeftControl;
        public KeyCode fastKey = KeyCode.LeftShift;
        public KeyCode slowKey = KeyCode.LeftAlt;

        private float _yaw;
        private float _pitch;
        private bool _locked = true;

        private void OnEnable()
        {
            LockCursor(true);
            Vector3 e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x;
        }

        private void OnDisable()
        {
            LockCursor(false);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && _locked)
            {
                LockCursor(true);
            }
            else if (!hasFocus)
            {
                LockCursor(false);
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (!pause) // resumed
            {
                if (_locked) LockCursor(true);
            }
            else
            {
                LockCursor(false);
            }
        }

        private void Update()
        {
            // Toggle cursor lock
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _locked = !_locked;
                LockCursor(_locked);
            }

            if (_locked)
            {
                float dx = Input.GetAxisRaw("Mouse X");
                float dy = Input.GetAxisRaw("Mouse Y");
                _yaw += dx * 100f * mouseSensitivity * Time.deltaTime;
                _pitch += (invertY ? dy : -dy) * 100f * mouseSensitivity * Time.deltaTime;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            // Movement
            Vector3 input = new Vector3(
                (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                0f,
                (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f)
            );
            Vector3 move = transform.TransformDirection(input.normalized);
            if (Input.GetKey(ascendKey)) move += Vector3.up;
            if (Input.GetKey(descendKey)) move += Vector3.down;

            float speed = moveSpeed;
            if (Input.GetKey(fastKey)) speed *= fastMultiplier;
            if (Input.GetKey(slowKey)) speed *= slowMultiplier;

            transform.position += move * speed * Time.deltaTime;
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}


