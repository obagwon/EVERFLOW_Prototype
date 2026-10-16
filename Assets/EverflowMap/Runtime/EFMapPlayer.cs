using UnityEngine;
using EFInput = EverflowGraybox.EFInput;
using Ability = EverflowGraybox.Ability;
namespace EverflowMap
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class EFMapPlayer : MonoBehaviour
    {
        public EFMapWorld world;
        public Camera view;
        public EFMapCamera follow;
        // Original graybox locomotion and input convention, adapted to FBX pivots.
        public float moveSpeed = 6, gravity = 24, jumpHeight = 1.35f, castRange = 10;
        [Min(.1f)] public float turnSharpness = 14;
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("selected")]
        Ability selectedAbility;
        public event System.Action<Ability> AbilityChanged;
        public Ability selected
        {
            get => selectedAbility;
            set
            {
                if (selectedAbility == value) return;
                selectedAbility = value;
                AbilityChanged?.Invoke(value);
            }
        }
        public bool FlowerUsed, VineUsed, SlideUsed;
        public EFMapNode Hovered { get; private set; }
        [Header("Ability targeting")]
        public LayerMask abilityMask = ~(1 << 2);
        public Transform[] abilityIgnoreRoots = new Transform[0];
        public bool logAbilityAttempts, drawAbilityGizmos;
        public string AimReason { get; private set; }
        public string LastCastResult { get; private set; }
        public int LastCastFrame { get; private set; } = -1;
        public bool AimReady { get; private set; }
        public Collider AimCollider { get; private set; }
        Vector3 aimPoint;
        public bool Riding => ride != null;
        public Vector3 CameraTravelTarget => ride == null ? transform.position + transform.forward * 5 :
            ride.kind == MapKind.Vine && ride.launchTarget != null ? ride.launchTarget.position :
            ride.ridePath.Length > 0 ? ride.ridePath[Mathf.Min(rideSegment + 2, ride.ridePath.Length - 1)].position : ride.HandlePosition;
        public bool[] SteppedDebris = new bool[3];
        CharacterController controller;
        EFMapNode ground, ride;
        Transform supportTransform;
        Vector3 supportLocal, supportWorld;
        Vector3 velocity, momentum;
        float rideDistance, rideSpeed, airGrace;
        int rideSegment;
        void Awake() { controller = GetComponent<CharacterController>(); }
        void Update()
        {
            if (world == null || !world.GameInputActive) return;
            Physics.SyncTransforms();
            if (EFInput.KeyDown(KeyCode.Alpha1)) selected = Ability.Bloom;
            if (EFInput.KeyDown(KeyCode.Alpha2)) selected = Ability.Freeze;
            if (EFInput.KeyDown(KeyCode.Alpha3)) selected = Ability.Pulse;
            if (ride != null) Ride();
            else { if (EFInput.KeyDown(KeyCode.E)) TryRide(); if (ride == null) Walk(); }
            if (transform.position.y < world.FallFloor) world.Respawn();
        }
        // Camera LateUpdate (-50) runs before this. Cast against the view rendered
        // this frame, after movement and mouse rotation, rather than yesterday's ray.
        void LateUpdate()
        {
            if (world == null || !world.GameInputActive || view == null) { Hovered = null; return; }
            Physics.SyncTransforms();
            Aim();
        }
        bool Ignored(Collider body)
        {
            if (body.transform.IsChildOf(transform)) return true;
            foreach (var root in abilityIgnoreRoots) if (root != null && body.transform.IsChildOf(root)) return true;
            return false;
        }
        void Aim()
        {
            Hovered = null;
            AimCollider = null; AimReady = false;
            AimReason = "No ability target. Aim at a flower, vine, crystal or puzzle surface.";
            var ray = view.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            aimPoint = ray.GetPoint(180);
            // Unrelated triggers (checkpoints, ride zones) never hide a target.
            var hits = Physics.RaycastAll(ray, 180, abilityMask, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (Ignored(hit.collider)) continue;
                var target = EFMapAbilityTarget.Resolve(hit.collider);
                if (hit.collider.isTrigger && (target == null || !target.interactive)) continue;
                AimCollider = hit.collider; aimPoint = hit.point;
                if (target == null || !target.interactive)
                    AimReason = "No ability target: " + hit.collider.name + " blocks the view.";
                else
                {
                    Hovered = target;
                    AimReady = CanCastAt(target, hit.point, out var reason);
                    AimReason = AimReady ? "Click: " + selected.ToString().ToUpperInvariant() + " / " + target.label : reason;
                }
                break;
            }
            if (!EFMapInput.Click) return;
            if (Hovered != null) CastAt(Hovered, aimPoint);
            else ReportAttempt(false, AimReason);
        }
        // Also used by the editor probe to exercise range and obstruction checks.
        public bool CastAt(EFMapNode target, Vector3 point)
        {
            if (!CanCastAt(target, point, out var reason)) { ReportAttempt(false, reason); return false; }
            ReportAttempt(true, target.Apply(selected)); return true;
        }
        public bool CanCastAt(EFMapNode target, Vector3 point, out string reason)
        {
            reason = null;
            if (world == null || !world.GameInputActive) reason = "Gameplay input is paused or in a cinematic.";
            else if (target == null) reason = "No ability target.";
            else if (target.world != world || !target.CanApply(selected, out reason)) { if (reason == null) reason = "Target belongs to another world."; }
            else if (Vector3.Distance(transform.position, point) > castRange) reason = $"Too far. Move closer ({castRange:0.#} m range).";
            else
            {
                Vector3 origin = transform.position + Vector3.up * .5f, delta = point - origin;
                foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, abilityMask, QueryTriggerInteraction.Ignore))
                    if (!Ignored(hit.collider) && EFMapAbilityTarget.Resolve(hit.collider) != target)
                    { reason = "The target is blocked by " + hit.collider.name + "."; break; }
            }
            return reason == null;
        }
        void ReportAttempt(bool applied, string reason)
        {
            LastCastFrame = Time.frameCount;
            LastCastResult = (applied ? "Applied: " : "Blocked: ") + reason;
            if (world != null) world.Message = reason;
            if (logAbilityAttempts) Debug.Log($"[EVERFLOW Ability] {selected} target={Hovered?.name ?? "none"} collider={AimCollider?.name ?? "none"} {LastCastResult}", this);
        }
        void OnDrawGizmosSelected()
        {
            if (!drawAbilityGizmos || !Application.isPlaying || view == null) return;
            Gizmos.color = AimReady ? Color.cyan : Color.yellow;
            Gizmos.DrawLine(view.transform.position, aimPoint);
            Gizmos.DrawLine(transform.position + Vector3.up * .5f, aimPoint);
            Gizmos.DrawWireSphere(aimPoint, .15f);
            if (AimCollider != null) Gizmos.DrawWireCube(AimCollider.bounds.center, AimCollider.bounds.size);
        }
        void Walk()
        {
            Vector2 input = Vector2.ClampMagnitude(EFInput.Move, 1);
            Vector3 forward = follow != null ? follow.PlanarForward : Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 move = (forward * input.y + right * input.x) * moveSpeed;
            airGrace = Mathf.Max(0, airGrace - Time.deltaTime);
            if (controller.isGrounded && airGrace <= 0)
            {
                if (supportTransform != null) controller.Move(supportTransform.TransformPoint(supportLocal) - supportWorld);
                velocity.y = -2;
                momentum = Vector3.MoveTowards(momentum, Vector3.zero, Time.deltaTime * 35);
                if (EFInput.KeyDown(KeyCode.Space))
                {
                    if (ground != null && ground.kind == MapKind.Flower && ground.Done && ground.launchTarget != null)
                    { FlowerUsed = true; LaunchTo(ground.launchTarget.position, 14); }
                    else { velocity.y = Mathf.Sqrt(2 * gravity * jumpHeight); supportTransform = null; }
                }
            }
            velocity.y -= gravity * Time.deltaTime;
            Vector3 horizontal = momentum.sqrMagnitude > .05f ? momentum + move * .22f : move;
            ground = null; supportTransform = null;
            Vector3 beforeMove = transform.position;
            var flags = controller.Move((horizontal + Vector3.up * velocity.y) * Time.deltaTime);
            if (input.sqrMagnitude > .001f || momentum.sqrMagnitude > .05f) FaceMotion(transform.position - beforeMove);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0) velocity.y = 0;
            if (supportTransform != null)
            { supportWorld = transform.position; supportLocal = supportTransform.InverseTransformPoint(supportWorld); }
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y <= .5f) return;
            ground = hit.collider.GetComponentInParent<EFMapNode>();
            if (ground == null) return;
            supportTransform = hit.collider.transform;
            if (ground.kind == MapKind.Debris && ground.Frozen)
            {
                for (int i = 0; i < world.debris.Length; i++) if (world.debris[i] == ground) SteppedDebris[i] = true;
            }
        }
        public bool TryRide()
        {
            if (!world.GameInputActive) return false;
            EFMapNode closest = null; float nearest = 3.5f;
            foreach (var node in world.stages[world.StageIndex].nodes)
            {
                if (node.kind != MapKind.Vine && node.kind != MapKind.Water) continue;
                if (node.kind == MapKind.Vine && !node.Done || node.kind == MapKind.Water && (!node.Filled || !node.Frozen)) continue;
                Vector3 point = node.kind == MapKind.Vine ? node.HandlePosition : node.rideVisual.position + Vector3.up;
                float d = Vector3.Distance(transform.position, point);
                if (d < nearest) { nearest = d; closest = node; }
            }
            if (closest == null) { world.Message = "E: approach a grown vine handle or the leaf after freezing the slide."; return false; }
            ride = closest; controller.enabled = false; velocity = momentum = Vector3.zero;
            rideDistance = 0; rideSpeed = 3; rideSegment = 0;
            if (ride.kind == MapKind.Vine) VineUsed = true;
            world.Message = ride.kind == MapKind.Vine ? "Space: release toward the next ledge." : "Leaf slide: follow the water toward the door.";
            return true;
        }
        void Ride()
        {
            if (ride.kind == MapKind.Vine)
            {
                FaceMotion(ride.launchTarget == null ? ride.SwingVelocity : ride.launchTarget.position - transform.position);
                transform.position = ride.HandlePosition - Vector3.up * .65f;
                if (EFInput.KeyDown(KeyCode.Space))
                {
                    Transform target = ride.launchTarget; ride = null; controller.enabled = true;
                    if (target != null) LaunchTo(target.position, 10);
                }
                return;
            }
            if (!ride.Frozen || !ride.Filled || ride.ridePath.Length < 2) { world.Respawn(); return; }
            rideSpeed = Mathf.Min(18, rideSpeed + 8 * Time.deltaTime);
            rideDistance += rideSpeed * Time.deltaTime;
            float remaining = rideDistance;
            Vector3 position = ride.ridePath[0].position;
            bool arrived = true;
            for (int i = 1; i < ride.ridePath.Length; i++)
            {
                Vector3 a = ride.ridePath[i - 1].position, b = ride.ridePath[i].position;
                float length = Vector3.Distance(a, b);
                if (remaining <= length) { rideSegment = i - 1; position = Vector3.Lerp(a, b, remaining / Mathf.Max(.001f, length)); arrived = false; break; }
                remaining -= length; position = b;
            }
            // Sweep the capsule along the ride rather than moving through solid walls.
            Vector3 delta = position - transform.position;
            if (delta.sqrMagnitude > .001f && Physics.CapsuleCast(transform.position - Vector3.up * .5f,
                transform.position + Vector3.up * .5f, .3f, delta.normalized, out var hit, delta.magnitude,
                ~(1 << 2), QueryTriggerInteraction.Ignore) && hit.distance < delta.magnitude - .1f)
            { world.Message = "Leaf path blocked; returned to checkpoint."; world.Respawn(); return; }
            FaceMotion(delta); transform.position = position;
            ride.rideVisual.position = position - Vector3.up * 1.05f;
            if (arrived)
            {
                SlideUsed = true; ride = null; controller.enabled = true;
                velocity = Vector3.down * 2; momentum = Vector3.zero; airGrace = .2f;
                world.Message = "Landed. Movement restored. Follow the root to the door.";
            }
        }
        void LaunchTo(Vector3 target, float up)
        {
            float rise = target.y - transform.position.y;
            float disc = up * up - 2 * gravity * rise;
            float time = (up + Mathf.Sqrt(Mathf.Max(1, disc))) / gravity;
            momentum = Vector3.ProjectOnPlane(target - transform.position, Vector3.up) / time;
            velocity.y = up; airGrace = .2f; supportTransform = null;
        }
        void FaceMotion(Vector3 motion)
        {
            Vector3 planar = Vector3.ProjectOnPlane(motion, Vector3.up);
            if (planar.sqrMagnitude < .00001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(planar, Vector3.up),
                1 - Mathf.Exp(-turnSharpness * Time.deltaTime));
        }
        public void Teleport(Vector3 point)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            ride = ground = null; supportTransform = null; controller.enabled = false;
            transform.position = point; velocity = momentum = Vector3.zero; airGrace = 0;
            controller.enabled = true; if (follow != null) follow.Snap(true);
        }
    }
}
