using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mythologicum
{
    [RequireComponent(typeof(Camera))]
    public sealed class IsometricCameraController : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float moveSpeed = 20f;
        [SerializeField, Min(1f)] private float rotationSpeed = 90f;
        [SerializeField, Min(0.001f)] private float zoomSpeed = 2f;
        [SerializeField, Min(1f)] private float minimumZoom = 5f;
        [SerializeField, Min(1f)] private float maximumZoom = 35f;
        [SerializeField, Min(1f)] private float initialZoom = 18f;
        [SerializeField, Range(15f, 80f)] private float tilt = 35.264f;
        [SerializeField] private Vector2 mapMinimum = new Vector2(-45f, -45f);
        [SerializeField] private Vector2 mapMaximum = new Vector2(45f, 45f);
        [SerializeField] private Vector3 focusPoint;
        private Camera view;
        private float yaw = 45f;
        private float zoomLevel;

        private void Awake()
        {
            view = GetComponent<Camera>();
            view.orthographic = false;
            zoomLevel = Mathf.Clamp(initialZoom, minimumZoom, maximumZoom);
            ApplyPose();
        }

        private void Update()
        {
            if (GameSaveMenu.IsOpen || GetComponent<BuildModeController>()?.ManagementOpen == true) return;
            Keyboard keys = Keyboard.current;
            InputField input = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                ? EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() : null;
            if (keys != null && (input == null || !input.isFocused))
            {
                float horizontal = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0)
                    - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0);
                float vertical = (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0)
                    - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0);
                Vector3 movement = Vector3.ClampMagnitude(new Vector3(horizontal, 0f, vertical), 1f);
                focusPoint += Quaternion.Euler(0f, yaw, 0f) * movement * (moveSpeed * Time.deltaTime);
                yaw += ((keys.eKey.isPressed ? 1 : 0) - (keys.qKey.isPressed ? 1 : 0))
                    * rotationSpeed * Time.deltaTime;
            }
            if (Mouse.current != null && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                zoomLevel = Mathf.Clamp(zoomLevel
                    - Mouse.current.scroll.ReadValue().y * zoomSpeed, minimumZoom, maximumZoom);

            focusPoint.x = Mathf.Clamp(focusPoint.x, mapMinimum.x, mapMaximum.x);
            focusPoint.z = Mathf.Clamp(focusPoint.z, mapMinimum.y, mapMaximum.y);
            ApplyPose();
        }

        public Vector3 SavedFocus => focusPoint;
        public void FocusOn(Vector3 point) { focusPoint = point; ApplyPose(); }
        public float SavedYaw => yaw;
        public float SavedZoom => zoomLevel;
        public void RestorePose(Vector3 focus, float heading, float zoom)
        {
            focusPoint = focus; yaw = heading;
            zoomLevel = Mathf.Clamp(zoom, minimumZoom, maximumZoom); ApplyPose();
        }

        private void ApplyPose()
        {
            var park=GetComponent<BuildModeController>();
            if(park?.Terrain!=null)focusPoint.y=park.GroundElevation(focusPoint);
            transform.rotation = Quaternion.Euler(tilt, yaw, 0f);
            // Match the previous framing at the focus point; zoom now moves the camera
            // through the world while perspective supplies natural depth and scale.
            float distance = zoomLevel / Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            transform.position = focusPoint - transform.forward * distance;
            if(park?.Terrain!=null)
            {var p=transform.position;p.y=Mathf.Max(p.y,park.GroundElevation(p)+2f);transform.position=p;}
        }

        private void OnValidate()
        {
            maximumZoom = Mathf.Max(minimumZoom, maximumZoom);
            initialZoom = Mathf.Clamp(initialZoom, minimumZoom, maximumZoom);
            mapMaximum = Vector2.Max(mapMinimum, mapMaximum);
        }
    }
}
