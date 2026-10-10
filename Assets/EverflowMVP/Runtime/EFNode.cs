using UnityEngine;

namespace EverflowMVP
{
    public enum Ability { Bloom, Freeze, Pulse }
    public enum NodeKind { Grow, Flower, Vine, Falling, Rotor, Source, Water, Door, Core }

    [DefaultExecutionOrder(-100)]
    public class EFNode : MonoBehaviour
    {
        public string label;
        [Range(0, 5)] public int stage;
        public NodeKind kind;
        [Header("Power and object links")]
        public bool acceptsPulse;
        public bool requiresPower;
        public bool alignmentConducts;
        public EFNode[] outputs = new EFNode[0];
        public EFNode supply;
        public GameObject growthObject;
        public Transform handle;
        public Transform rideStart;
        public Transform rideEnd;
        public Transform rideVisual;
        [Header("Motion (editable before Play)")]
        public Vector3 axis = Vector3.right;
        public float speed = 35;
        public float amplitude = 45;
        public float targetAngle;
        public float tolerance = 10;
        public float initialAngle = 45;
        public float ropeLength = 7;
        public bool supportOnlyWhenFrozen;
        public Vector3 launchVelocity = new Vector3(0, 14, 7);
        [Header("Read-only during play")]
        [SerializeField] bool grown, frozen, sourceOn, powered, filled, restored;
        public bool Grown => grown;
        public bool Frozen => frozen;
        public bool SourceOn => sourceOn;
        public bool Powered { get => powered; set => powered = value; }
        public bool Filled => filled;
        public bool Restored => restored;
        public float Angle { get; private set; }
        // Straight rectangular bridges are symmetric: 0 and 180 are equivalent.
        public bool Aligned => Mathf.Abs(Mathf.DeltaAngle(Angle * 2, targetAngle * 2)) * .5f <= tolerance;
        public bool IsSource => acceptsPulse && kind != NodeKind.Core;
        public bool CanConduct => (!(kind == NodeKind.Grow || kind == NodeKind.Flower || kind == NodeKind.Vine) || grown)
            && (!alignmentConducts || frozen && Aligned);
        public bool Freezable => kind == NodeKind.Falling || kind == NodeKind.Rotor || kind == NodeKind.Water;
        public Vector3 HandlePosition => handle != null ? handle.position : transform.position;
        public Vector3 SwingVelocity { get; private set; }

        Vector3 homePosition, homeScale, visualHome;
        Quaternion homeRotation, visualRotation;
        Collider body;
        Renderer appearance;
        MaterialPropertyBlock colors;
        float phase;
        bool initialized;

        void Awake() { Initialize(); ResetNode(); }
        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            homeScale = transform.localScale;
            if (rideVisual != null) { visualHome = rideVisual.position; visualRotation = rideVisual.rotation; }
            body = GetComponent<Collider>();
            appearance = GetComponent<Renderer>();
            colors = new MaterialPropertyBlock();
        }
        public void ResetNode()
        {
            Initialize();
            grown = frozen = sourceOn = powered = filled = restored = false;
            phase = 0;
            Angle = initialAngle;
            transform.localPosition = homePosition;
            transform.localRotation = homeRotation;
            transform.localScale = homeScale;
            ResetRideVisual();
            if (growthObject != null) growthObject.SetActive(false);
            UpdateMotion(0);
            RefreshVisual();
        }
        public void ResetRideVisual()
        {
            if (rideVisual == null) return;
            rideVisual.position = visualHome;
            rideVisual.rotation = visualRotation;
        }
        public string Apply(Ability ability)
        {
            if (ability == Ability.Bloom && (kind == NodeKind.Grow || kind == NodeKind.Flower || kind == NodeKind.Vine))
            {
                grown = true;
                if (growthObject != null) growthObject.SetActive(true);
                RefreshVisual();
                return label + ": BLOOM connected";
            }
            if (ability == Ability.Freeze && Freezable)
            {
                if (kind == NodeKind.Water && !filled) return "Open the sluice with PULSE first.";
                frozen = !frozen;
                RefreshVisual();
                return label + (frozen ? ": frozen" : ": released");
            }
            if (ability == Ability.Pulse && kind == NodeKind.Core)
            {
                if (!powered) return "No connected power: grow roots, power and align the rotor, then freeze it.";
                restored = true;
                if (growthObject != null) growthObject.SetActive(true);
                return "The spring is restored. Walk to the goal.";
            }
            if (ability == Ability.Pulse && acceptsPulse)
            {
                sourceOn = !sourceOn;
                return label + (sourceOn ? ": PULSE on" : ": PULSE off");
            }
            return "This target does not respond to " + ability.ToString().ToUpperInvariant() + ".";
        }
        void Update() { Tick(Time.deltaTime); }
        public void Tick(float deltaTime)
        {
            Initialize();
            if (kind == NodeKind.Water && (powered || supply != null && supply.Powered)) filled = true;
            UpdateMotion(deltaTime);
            RefreshVisual();
        }
        void UpdateMotion(float dt)
        {
            if (kind == NodeKind.Falling && !frozen)
            {
                // Repeated descent and return, driven by its own stoppable clock.
                phase += dt * speed;
                float progress = Mathf.Repeat(phase, 1f);
                transform.localPosition = homePosition + Vector3.down * amplitude * progress;
            }
            if (kind == NodeKind.Rotor)
            {
                if (!frozen)
                {
                    if (!requiresPower || powered) Angle += dt * speed;
                    else Angle = Mathf.MoveTowardsAngle(Angle, initialAngle, dt * Mathf.Abs(speed));
                }
                transform.localRotation = homeRotation * Quaternion.AngleAxis(Angle - initialAngle, axis.normalized);
            }
            if (kind == NodeKind.Vine && grown && handle != null)
            {
                Vector3 previous = handle.position;
                phase += dt;
                float angle = -Mathf.Cos(phase * speed) * amplitude * Mathf.Deg2Rad;
                handle.localPosition = new Vector3(0, -Mathf.Cos(angle) * ropeLength, Mathf.Sin(angle) * ropeLength);
                if (dt > .00001f) SwingVelocity = (handle.position - previous) / dt;
                else SwingVelocity = Vector3.zero;
                if (growthObject != null)
                {
                    Transform rope = growthObject.transform;
                    rope.position = (transform.position + handle.position) * .5f;
                    rope.up = transform.position - handle.position;
                    rope.localScale = new Vector3(.15f, ropeLength * .5f, .15f);
                }
            }
            if (kind == NodeKind.Door)
            {
                // A raised door remains open if the player occupies its opening.
                bool occupied = false;
                if (!powered && transform.localPosition.y > homePosition.y + .1f && EFWorld.Current != null)
                {
                    Vector3 p = EFWorld.Current.player.transform.position;
                    Vector3 delta = p - transform.parent.TransformPoint(homePosition);
                    occupied = Mathf.Abs(delta.x) < homeScale.x * .5f + .6f && Mathf.Abs(delta.z) < homeScale.z * .5f + .6f;
                }
                if (!occupied) transform.localPosition = Vector3.MoveTowards(transform.localPosition,
                    homePosition + (powered ? Vector3.up * (homeScale.y + 1) : Vector3.zero), dt * 6);
            }
        }
        void RefreshVisual()
        {
            if (body != null)
            {
                if (kind == NodeKind.Water) body.isTrigger = !(filled && frozen);
                else if (supportOnlyWhenFrozen) body.isTrigger = !frozen;
            }
            if (appearance == null) return;
            Color color = new Color(.46f, .48f, .51f);
            if (kind == NodeKind.Grow || kind == NodeKind.Flower || kind == NodeKind.Vine) color = new Color(.28f, .42f, .22f);
            if (IsSource || kind == NodeKind.Core) color = new Color(.55f, .43f, .15f);
            if (Freezable && !IsSource) color = new Color(.32f, .45f, .58f);
            if (grown) color = new Color(.2f, .8f, .35f);
            if (kind == NodeKind.Water && filled) color = new Color(.15f, .4f, .8f);
            if (powered) color = new Color(1, .75f, .12f);
            if (frozen) color = new Color(.18f, .85f, 1);
            if (restored) color = new Color(.5f, 1, .75f);
            colors.SetColor("_Color", color);
            colors.SetColor("_BaseColor", color);
            appearance.SetPropertyBlock(colors);
        }
        public string Status => $"{label} | grown:{grown} power:{powered} frozen:{frozen}" +
            (kind == NodeKind.Rotor ? $" angle:{Mathf.DeltaAngle(0, Angle):F0} / target:{targetAngle:F0}" : "");
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            foreach (EFNode node in outputs) if (node != null) Gizmos.DrawLine(transform.position, node.transform.position);
            if (rideStart != null && rideEnd != null) { Gizmos.color = Color.cyan; Gizmos.DrawLine(rideStart.position, rideEnd.position); }
        }
    }
}
