using UnityEngine;

namespace EverflowMap
{
    // Scene-local P3 experiment state. R still resets the current puzzle;
    // ordinary falls preserve its prepared source/gate/ice through World.Respawn.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class EFCanalExperiment : MonoBehaviour
    {
        public const int Stage = 3; // Existing map UI calls the canal P4.
        public EFMapWorld world;
        public EFMapNode source, gate, water;
        public BoxCollider departureFloor;
        public Transform departureCheckpoint;
        public BoxCollider arrivalFloor;
        public Renderer[] feedIndicators = new Renderer[0];
        public GameObject[] flowingWater = new GameObject[0];
        public Renderer[] lowWaterSurfaces = new Renderer[0];
        [Tooltip("World-space recovery below the elevated canal. Excludes the original approach and low courtyard.")]
        public Bounds fallRecovery;
        [Tooltip("Below the high dock. Only contact with original terrain triggers recovery; the approach stairs stay legal.")]
        public Bounds departureRecovery;
        public Transform originalTerrain;
        public float stableSeconds = .18f;
        public bool DepartureSaved { get; private set; }
        float groundedFor;
        MaterialPropertyBlock colors;
        CharacterController controller;

        void Awake() { colors = new MaterialPropertyBlock(); }
        void LateUpdate() { TickLocalState(Time.deltaTime); }

        public void TickLocalState(float deltaTime)
        {
            if (world == null || source == null || gate == null || water == null || world.player == null) return;
            if (colors == null) colors = new MaterialPropertyBlock();
            if (controller == null) controller = world.player.GetComponent<CharacterController>();
            if (world.StageIndex == Stage && world.GameInputActive && !world.player.Riding &&
                (fallRecovery.Contains(world.player.transform.position) ||
                departureRecovery.Contains(world.player.transform.position) && StandingOnOriginalTerrain()))
            {
                world.Respawn();
                groundedFor = 0;
            }
            bool filled = water.Filled;
            foreach (var display in flowingWater) if (display != null) display.SetActive(filled);
            foreach (var surface in lowWaterSurfaces) if (surface != null)
            {
                surface.enabled=filled;
                surface.sharedMaterial=water.Frozen?water.iceMaterial:water.waterMaterial;
            }
            Color color = source.SourceOn ? new Color(.2f, .9f, 1) : new Color(.35f, .3f, .2f);
            colors.SetColor("_BaseColor", color);
            colors.SetColor("_EmissionColor", source.SourceOn ? color * 1.2f : Color.black);
            foreach (var indicator in feedIndicators) if (indicator != null) indicator.SetPropertyBlock(colors);

            // Retry resets these node states. A fall leaves them intact.
            if (!source.SourceOn || !water.Frozen) { DepartureSaved = false; groundedFor = 0; }
            if (DepartureSaved || world.StageIndex != Stage || !world.GameInputActive || !water.Satisfied)
            { groundedFor = 0; return; }
            if (controller == null) controller = world.player.GetComponent<CharacterController>();
            if (!IsStandingOn(departureFloor)) { groundedFor = 0; return; }
            groundedFor += Mathf.Max(0, deltaTime);
            if (groundedFor < stableSeconds || departureCheckpoint == null) return;
            DepartureSaved = true;
            world.SetCheckpoint(departureCheckpoint.position);
        }

        // Require the designated deck at the feet, even when a coplanar stair
        // overlaps it. A different-height support is not evidence of deck contact.
        public bool IsStandingOn(BoxCollider floor)
        {
            if (world == null || world.player == null || floor == null || !floor.enabled || !floor.gameObject.activeInHierarchy || floor.isTrigger ||
                floor.gameObject.layer == 2 || world.player.Riding) return false;
            if (controller == null) controller = world.player.GetComponent<CharacterController>();
            if (controller == null || !controller.enabled || !controller.isGrounded) return false;
            var feet = controller.bounds.center - Vector3.up * controller.bounds.extents.y;
            Vector3 local = floor.transform.InverseTransformPoint(feet);
            Vector3 half = floor.size * .5f;
            Vector3 center = floor.center;
            if (Mathf.Abs(local.x - center.x) > half.x + .001f || Mathf.Abs(local.z - center.z) > half.z + .001f) return false;
            var contactRay = new Ray(feet + Vector3.up * .15f, Vector3.down);
            if (!floor.Raycast(contactRay, out var floorHit, .4f) || floorHit.normal.y <= .8f) return false;
            // Physics.Raycast may return either collider at an overlapping seam.
            // Query the intended floor directly, then reject genuinely higher
            // solids without depending on the order of equal-distance hits.
            foreach (var hit in Physics.RaycastAll(contactRay, .4f, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == floor || hit.collider.transform.IsChildOf(world.player.transform)) continue;
                if (floorHit.distance - hit.distance >= .03f) return false;
            }
            return true;
        }
        bool StandingOnOriginalTerrain()
        {
            if(originalTerrain==null||controller==null||!controller.enabled||!controller.isGrounded) return false;
            var feet=controller.bounds.center-Vector3.up*controller.bounds.extents.y;
            return Physics.Raycast(feet+Vector3.up*.15f,Vector3.down,out var hit,.4f,
                ~(1<<2),QueryTriggerInteraction.Ignore)&&hit.collider.transform.IsChildOf(originalTerrain);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1,.3f,.1f,.25f);
            Gizmos.DrawWireCube(fallRecovery.center,fallRecovery.size);
            Gizmos.color=Color.yellow;
            Gizmos.DrawWireCube(departureRecovery.center,departureRecovery.size);
        }
    }
}
