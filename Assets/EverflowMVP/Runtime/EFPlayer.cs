using UnityEngine;

namespace EverflowMVP
{
    [RequireComponent(typeof(CharacterController))]
    public class EFPlayer : MonoBehaviour
    {
        public EFWorld world;
        public Camera view;
        public EFCamera follow;
        public float moveSpeed = 6;
        public float gravity = 24;
        public float jumpHeight = 1.35f;
        public float castRange = 15;
        public float rideAcceleration = 8;
        public float rideMaxSpeed = 18;
        public Ability selected;
        public EFNode Hovered { get; private set; }
        public string Mode => ride == null ? (controller.isGrounded ? "Walk" : "Air") : ride.kind == NodeKind.Vine ? "Swing: Space to release" : "Leaf slide";
        CharacterController controller;
        EFNode ground;
        EFNode ride;
        Vector3 velocity;
        Vector3 momentum;
        float progress, rideSpeed, steer, airborneGrace;
        public bool IsRiding => ride != null;

        void Awake() { controller = GetComponent<CharacterController>(); }
        void Update()
        {
            if (world == null || view == null) return;
            // Nodes animate in Update before this component. Sync once before queries/movement.
            Physics.SyncTransforms();
            if (EFInput.KeyDown(KeyCode.Alpha1)) selected = Ability.Bloom;
            if (EFInput.KeyDown(KeyCode.Alpha2)) selected = Ability.Freeze;
            if (EFInput.KeyDown(KeyCode.Alpha3)) selected = Ability.Pulse;
            AimAndCast();
            if (ride != null) UpdateRide();
            else
            {
                if (EFInput.KeyDown(KeyCode.E)) TryRide();
                if (ride == null) Walk();
            }
            if (transform.position.y < -15) world.Respawn();
        }
        void AimAndCast()
        {
            Hovered = null;
            Vector2 pointer = EFInput.Pointer;
            if (pointer.x < 980 && pointer.y > Screen.height - 162) return;
            if (!Physics.Raycast(view.ScreenPointToRay(pointer), out RaycastHit hit, 150,
                ~(1 << 2), QueryTriggerInteraction.Collide)) return;
            EFNode target = hit.collider.GetComponentInParent<EFNode>();
            if (target == null || target.stage != world.StageIndex) return;
            Hovered = target;
            if (!EFInput.Click) return;
            if (Vector3.Distance(transform.position, hit.point) > castRange) { world.Message = "Too far. Move closer."; return; }
            Vector3 origin = transform.position + Vector3.up * .5f;
            if (Physics.Linecast(origin, hit.point, out RaycastHit obstacle, ~(1 << 2), QueryTriggerInteraction.Ignore)
                && obstacle.collider.GetComponentInParent<EFNode>() != target)
            { world.Message = "A wall or platform blocks the target."; return; }
            world.Message = target.Apply(selected);
            world.RefreshPower();
        }
        void Walk()
        {
            Vector2 input = Vector2.ClampMagnitude(EFInput.Move, 1);
            Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 move = (forward * input.y + right * input.x) * moveSpeed;
            airborneGrace = Mathf.Max(0, airborneGrace - Time.deltaTime);
            if (controller.isGrounded && airborneGrace <= 0)
            {
                velocity.y = -2;
                momentum = Vector3.MoveTowards(momentum, Vector3.zero, Time.deltaTime * 35);
                if (EFInput.KeyDown(KeyCode.Space))
                {
                    if (ground != null && ground.kind == NodeKind.Flower && ground.Grown)
                    {
                        velocity.y = ground.launchVelocity.y;
                        momentum = Vector3.ProjectOnPlane(ground.transform.TransformDirection(ground.launchVelocity), Vector3.up);
                        world.Message = "Flower launch! Land on the upper ledge.";
                    }
                    else velocity.y = Mathf.Sqrt(2 * gravity * jumpHeight);
                }
            }
            velocity.y -= gravity * Time.deltaTime;
            // Preserve assisted launch velocity; allow limited air steering.
            Vector3 horizontal = momentum.sqrMagnitude > .05f ? momentum + move * .22f : move;
            ground = null;
            CollisionFlags flags = controller.Move((horizontal + Vector3.up * velocity.y) * Time.deltaTime);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0) velocity.y = 0;
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > .5f) ground = hit.collider.GetComponentInParent<EFNode>();
        }
        void TryRide()
        {
            EFNode closest = null;
            float nearest = 3.5f;
            foreach (EFNode node in world.stages[world.StageIndex].nodes)
            {
                if (node == null) continue;
                Vector3 point;
                if (node.kind == NodeKind.Vine && node.Grown) point = node.HandlePosition;
                else if (node.kind == NodeKind.Water && node.Filled && node.Frozen && node.rideStart != null) point = node.rideStart.position;
                else continue;
                float distance = Vector3.Distance(transform.position, point);
                if (distance < nearest) { nearest = distance; closest = node; }
            }
            if (closest == null) { world.Message = "E: move near a grown vine handle or a frozen-water leaf dock."; return; }
            ride = closest;
            controller.enabled = false;
            velocity = momentum = Vector3.zero;
            progress = 0; rideSpeed = 3; steer = 0;
            world.Message = ride.kind == NodeKind.Vine ? "Swing forward; press Space to release." : "Slide accelerates; the ramp launches you automatically.";
        }
        void UpdateRide()
        {
            if (ride.kind == NodeKind.Vine)
            {
                transform.position = ride.HandlePosition - Vector3.up * .5f;
                if (EFInput.KeyDown(KeyCode.Space))
                {
                    Vector3 release = ride.SwingVelocity;
                    // Forgiving greybox assistance, not a physical rope solver.
                    release.z = Mathf.Max(8, release.z);
                    release.y = Mathf.Max(6, release.y + 3);
                    EndRide(release);
                }
                return;
            }
            if (!ride.Frozen || !ride.Filled || ride.rideStart == null || ride.rideEnd == null)
            { EndRide(Vector3.forward * rideSpeed); return; }
            float length = Vector3.Distance(ride.rideStart.position, ride.rideEnd.position);
            rideSpeed = Mathf.Min(rideMaxSpeed, rideSpeed + rideAcceleration * Time.deltaTime);
            progress += rideSpeed * Time.deltaTime / Mathf.Max(.1f, length);
            steer = Mathf.Clamp(steer + EFInput.Move.x * Time.deltaTime * 1.8f, -.75f, .75f);
            Vector3 position = Vector3.Lerp(ride.rideStart.position, ride.rideEnd.position, Mathf.Clamp01(progress)) + Vector3.right * steer;
            transform.position = position;
            if (ride.rideVisual != null) ride.rideVisual.position = position - Vector3.up * 1.05f;
            if (progress >= 1) EndRide(new Vector3(0, 10, 17));
        }
        void EndRide(Vector3 launch)
        {
            ride = null;
            controller.enabled = true;
            velocity = Vector3.up * launch.y;
            momentum = Vector3.ProjectOnPlane(launch, Vector3.up);
            airborneGrace = .2f;
            ground = null;
        }
        public void Teleport(Vector3 position, bool resetCamera)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            ride = null;
            controller.enabled = false;
            transform.position = position;
            velocity = momentum = Vector3.zero;
            airborneGrace = 0;
            ground = null;
            controller.enabled = true;
            if (follow != null) follow.Snap(resetCamera);
        }
    }
}
