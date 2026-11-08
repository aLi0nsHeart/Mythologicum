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
        private Transform followingTarget;
        private CreatureAgent followingActor;
        private Vector3 followOffset;
        private Vector3 followExtents;
        private float selectionViewportX = .5f;
        private float targetZoom;
        private bool hasFollowTarget;
        private bool framingSelection;

        public Transform FollowingTarget => followingTarget;
        public bool IsFollowing => followingTarget != null && followingTarget.gameObject.activeInHierarchy
            && (followingActor == null || followingActor.Alive);

        private void Awake()
        {
            view = GetComponent<Camera>();
            view.orthographic = false;
            zoomLevel = Mathf.Clamp(initialZoom, minimumZoom, maximumZoom);
            ApplyPose();
        }

        private void Update()
        {
            if (hasFollowTarget && !IsFollowing) StopFollowing();
            bool inputBlocked = GameSaveMenu.IsOpen || SettingsPresentation.IsOpen
                || GetComponent<BuildModeController>()?.ManagementOpen == true;
            Keyboard keys = Keyboard.current;
            InputField input = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                ? EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() : null;
            if (!inputBlocked && keys != null && (input == null || !input.isFocused))
            {
                float horizontal = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0)
                    - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0);
                float vertical = (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0)
                    - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0);
                Vector3 movement = Vector3.ClampMagnitude(new Vector3(horizontal, 0f, vertical), 1f);
                if (movement.sqrMagnitude > 0f) StopFollowing();
                focusPoint += Quaternion.Euler(0f, yaw, 0f) * movement * (moveSpeed * GameSettings.MoveSensitivity * Time.unscaledDeltaTime);
                yaw += ((keys.eKey.isPressed ? 1 : 0) - (keys.qKey.isPressed ? 1 : 0))
                    * rotationSpeed * GameSettings.RotationSensitivity * Time.unscaledDeltaTime;
            }
            if (!inputBlocked && Mouse.current != null && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .001f)
                {
                    framingSelection = false;
                    zoomLevel = Mathf.Clamp(zoomLevel
                        - scroll * zoomSpeed * GameSettings.ZoomSensitivity, minimumZoom, maximumZoom);
                }
            }

            if (IsFollowing)
            {
                // Selection can finish framing while a menu is open or the simulation is paused.
                float blend = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
                if (framingSelection)
                {
                    targetZoom = SelectionZoom();
                    zoomLevel = Mathf.Lerp(zoomLevel, targetZoom, blend);
                }
                // Centre the subject in the park area left clear by the information panel.
                Vector3 screenOffset = Quaternion.Euler(0f, yaw, 0f) * Vector3.right
                    * (zoomLevel * view.aspect * (1f - 2f * selectionViewportX));
                focusPoint = Vector3.Lerp(focusPoint, followingTarget.TransformPoint(followOffset) + screenOffset, blend);
            }
            else
            {
                focusPoint.x = Mathf.Clamp(focusPoint.x, mapMinimum.x, mapMaximum.x);
                focusPoint.z = Mathf.Clamp(focusPoint.z, mapMinimum.y, mapMaximum.y);
            }
            ApplyPose();
        }

        public Vector3 SavedFocus => focusPoint;
        public void FocusOn(Vector3 point) { StopFollowing(); focusPoint = point; ApplyPose(); }

        /// <summary>Frame the selected object's real bounds, then track its movement.</summary>
        public void Follow(Transform target, Bounds worldBounds, float viewportX = .5f)
        {
            if (target == null || (IsFollowing && followingTarget == target)) return;
            followingTarget = target;
            followingActor = target.GetComponent<CreatureAgent>();
            hasFollowTarget = true;
            followOffset = target.InverseTransformPoint(worldBounds.center);
            followExtents = worldBounds.extents;
            selectionViewportX = Mathf.Clamp(viewportX, .25f, .5f);
            framingSelection = true;
            targetZoom = SelectionZoom();
        }

        private float SelectionZoom()
        {
            Quaternion inverseView = Quaternion.Inverse(Quaternion.Euler(tilt, yaw, 0f));
            Vector3 extent = Vector3.zero;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = inverseView * Vector3.Scale(followExtents, new Vector3(x, y, z));
                        extent = Vector3.Max(extent, new Vector3(Mathf.Abs(corner.x), Mathf.Abs(corner.y), Mathf.Abs(corner.z)));
                    }
            float tangent = Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            // Reserve space for the inspector and a small margin around the model.
            return Mathf.Clamp(Mathf.Max(extent.y + 1f, (extent.x + 1f) / Mathf.Max(.4f, view.aspect * selectionViewportX * 1.7f))
                + extent.z * tangent, minimumZoom, maximumZoom);
        }

        public void StopFollowing()
        {
            followingTarget = null;
            followingActor = null;
            hasFollowTarget = false;
            framingSelection = false;
        }
        public void SetMapBounds(Vector3 origin,float width,float depth)
        {
            mapMinimum=new Vector2(origin.x+2f,origin.z+2f);mapMaximum=new Vector2(origin.x+width-2f,origin.z+depth-2f);
            maximumZoom=Mathf.Max(maximumZoom,Mathf.Max(width,depth)*.4f);
        }
        public float SavedYaw => yaw;
        public float SavedZoom => zoomLevel;
        public void RestorePose(Vector3 focus, float heading, float zoom)
        {
            StopFollowing();
            focusPoint = focus; yaw = heading;
            zoomLevel = Mathf.Clamp(zoom, minimumZoom, maximumZoom); ApplyPose();
        }

        private void ApplyPose()
        {
            var park=GetComponent<BuildModeController>();
            if(park?.Terrain!=null && !IsFollowing)focusPoint.y=park.GroundElevation(focusPoint);
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
