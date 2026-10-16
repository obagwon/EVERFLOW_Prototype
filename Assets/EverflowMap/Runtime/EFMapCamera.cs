using UnityEngine;
namespace EverflowMap
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-50)]
    public sealed class EFMapCamera : MonoBehaviour
    {
        public EFMapPlayer player;
        [HideInInspector] public int setupVersion;
        [Header("Framing")]
        [Min(.5f)] public float distance = 7;
        public float lookHeight = .7f;
        [Range(-1.5f, 1.5f)] public float shoulderOffset = .6f;
        public float pitch = 18, yaw = 180;
        [Header("Free look (degrees per mouse pixel)")]
        [Min(.001f)] public float sensitivity = .14f;
        [Min(.1f)] public float verticalSensitivity = .85f;
        [Range(-80, 0)] public float minPitch = -35;
        [Range(1, 85)] public float maxPitch = 70;
        public bool invertY;
        [Range(-35, 70)] public float recenterPitch = 18;
        [Header("Position follow; mouse rotation stays immediate")]
        [Min(.001f)] public float followSmoothTime = .1f;
        [Min(.001f)] public float verticalSmoothTime = .14f;
        [Min(.1f)] public float maxFollowLag = 1.5f;
        [Header("Obstruction")]
        public LayerMask collisionMask = ~(1 << 2);
        [Min(.05f)] public float collisionRadius = .25f;
        [Min(.01f)] public float collisionPadding = .12f;
        [Min(.001f)] public float collisionReturnSmoothTime = .25f;
        public Transform[] collisionIgnoreRoots = new Transform[0];
        [Min(0)] public float hidePlayerDistance = 1;
        [Header("Vine / leaf framing; mouse always overrides")]
        public bool assistRides = true;
        [Min(0)] public float rideAssistDelay = 1.25f;
        [Min(1)] public float rideTurnSpeed = 90;
        [Min(0)] public float rideLookAhead = 1.5f;
        public Vector3 PlanarForward => Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        public Vector3 PlanarRight => Quaternion.Euler(0, yaw, 0) * Vector3.right;
        public Vector3 FollowPoint { get; private set; }
        public float CurrentDistance { get; private set; }
        public int LastSnapFrame { get; private set; }
        Vector3 focusVelocity;
        float boomVelocity, verticalFocusVelocity, lastManualLook = float.NegativeInfinity;
        bool initialized, discardLook;
        readonly RaycastHit[] hits = new RaycastHit[64];
        Renderer[] playerRenderers;
        bool[] originalRenderingOff;

        bool InputActive => player != null && (player.world == null || player.world.GameInputActive);
        void OnEnable() { discardLook = true; if (player != null) Snap(); }
        void Start() { if (!initialized) Snap(); }
        void Update()
        {
            if (!InputActive) return;
            if (EFMapInput.KeyDown(KeyCode.C)) RecenterBehindPlayer();
            Vector2 delta = discardLook ? Vector2.zero : EFMapInput.LookDelta;
            discardLook = false;
            if (delta.sqrMagnitude > .0001f)
            {
                // Mouse delta already measures this frame's displacement: no deltaTime multiplier.
                yaw = Mathf.Repeat(yaw + delta.x * sensitivity, 360);
                pitch = Mathf.Clamp(pitch + delta.y * sensitivity * verticalSensitivity * (invertY ? 1 : -1), minPitch, maxPitch);
                lastManualLook = Time.time;
            }
            else if (assistRides && player.Riding && Time.time - lastManualLook >= rideAssistDelay)
            {
                Vector3 direction = player.CameraTravelTarget - (player.transform.position + Vector3.up * lookHeight);
                float planar = Vector3.ProjectOnPlane(direction, Vector3.up).magnitude;
                if (planar > .1f)
                {
                    float heading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    float slope = -Mathf.Atan2(direction.y, planar) * Mathf.Rad2Deg + 10;
                    yaw = Mathf.MoveTowardsAngle(yaw, heading, rideTurnSpeed * Time.deltaTime);
                    pitch = Mathf.MoveTowards(pitch, Mathf.Clamp(slope, minPitch, maxPitch), rideTurnSpeed * .5f * Time.deltaTime);
                }
            }
            // Movement uses these angles in its Update, before the camera's LateUpdate.
        }
        void LateUpdate() { if (InputActive) Position(Time.deltaTime, false); }
        public void RecenterBehindPlayer()
        {
            if (player == null) return;
            yaw = player.transform.eulerAngles.y; pitch = Mathf.Clamp(recenterPitch, minPitch, maxPitch);
            lastManualLook = Time.time;
        }
        public void DiscardLookDelta() { discardLook = true; }
        public void Snap(bool alignBehind = false)
        {
            if (player == null) return;
            if (alignBehind) RecenterBehindPlayer();
            focusVelocity = Vector3.zero; boomVelocity = verticalFocusVelocity = 0;
            FollowPoint = player.transform.position + Vector3.up * lookHeight;
            initialized = true; LastSnapFrame = Time.frameCount; discardLook = true;
            Position(0, true);
        }
        void Position(float dt, bool snap)
        {
            if (player == null) return;
            if (!initialized) { Snap(); return; }
            Vector3 anchor = player.transform.position + Vector3.up * lookHeight;
            Vector3 target = anchor;
            if (player.Riding)
                target += Vector3.ProjectOnPlane(player.CameraTravelTarget - anchor, Vector3.up).normalized * rideLookAhead;
            if (snap) FollowPoint = target;
            else
            {
                Vector3 horizontal = Vector3.SmoothDamp(Vector3.ProjectOnPlane(FollowPoint, Vector3.up),
                    Vector3.ProjectOnPlane(target, Vector3.up), ref focusVelocity, Mathf.Max(.001f, followSmoothTime), Mathf.Infinity, dt);
                float y = Mathf.SmoothDamp(FollowPoint.y, target.y, ref verticalFocusVelocity, Mathf.Max(.001f, verticalSmoothTime), Mathf.Infinity, dt);
                FollowPoint = new Vector3(horizontal.x, y, horizontal.z);
                FollowPoint = target + Vector3.ClampMagnitude(FollowPoint - target, maxFollowLag);
            }
            Quaternion orbit = Quaternion.Euler(Mathf.Clamp(pitch, minPitch, maxPitch), yaw, 0);
            Vector3 desired = FollowPoint + PlanarRight * shoulderOffset + orbit * Vector3.back * distance;
            Vector3 displacement = desired - anchor;
            float desiredLength = displacement.magnitude;
            if (desiredLength < .001f) return;
            Vector3 direction = displacement / desiredLength;
            int count = Physics.SphereCastNonAlloc(anchor, collisionRadius, direction, hits, desiredLength, collisionMask, QueryTriggerInteraction.Ignore);
            float allowed = desiredLength;
            if (count == hits.Length)
            {
                foreach (var hit in Physics.SphereCastAll(anchor, collisionRadius, direction, desiredLength, collisionMask, QueryTriggerInteraction.Ignore))
                    if (!Ignored(hit.collider)) allowed = Mathf.Min(allowed, Mathf.Max(.02f, hit.distance - collisionPadding));
            }
            else for (int i = 0; i < count; i++)
                if (!Ignored(hits[i].collider)) allowed = Mathf.Min(allowed, Mathf.Max(.02f, hits[i].distance - collisionPadding));
            // Retract immediately for safety; smooth only the return after the obstruction clears.
            if (snap || allowed < CurrentDistance) { CurrentDistance = allowed; boomVelocity = 0; }
            else CurrentDistance = Mathf.Min(allowed, Mathf.SmoothDamp(CurrentDistance, allowed, ref boomVelocity, collisionReturnSmoothTime, Mathf.Infinity, dt));
            transform.SetPositionAndRotation(anchor + direction * CurrentDistance, orbit);
            SetPlayerHidden(Vector3.Distance(transform.position, player.transform.position) < hidePlayerDistance);
        }
        bool Ignored(Collider body)
        {
            if (body == null || body.transform.IsChildOf(player.transform)) return true;
            foreach (var root in collisionIgnoreRoots) if (root != null && body.transform.IsChildOf(root)) return true;
            return false;
        }
        void SetPlayerHidden(bool hidden)
        {
            if (playerRenderers == null)
            {
                playerRenderers = player.GetComponentsInChildren<Renderer>(true);
                originalRenderingOff = new bool[playerRenderers.Length];
                for (int i = 0; i < playerRenderers.Length; i++) originalRenderingOff[i] = playerRenderers[i].forceRenderingOff;
            }
            for (int i = 0; i < playerRenderers.Length; i++)
                if (playerRenderers[i] != null) playerRenderers[i].forceRenderingOff = hidden || originalRenderingOff[i];
        }
        void OnDisable()
        {
            if (playerRenderers != null) SetPlayerHidden(false);
            discardLook = true;
        }
    }
}
