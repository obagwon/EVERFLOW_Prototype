using System;
using UnityEngine;
using Ability = EverflowGraybox.Ability;

namespace EverflowMap
{
    public enum MapKind { Flower, Vine, Debris, Slate, Bridge, Source, Water, Root, Repair, Gate, Door }
    [DefaultExecutionOrder(-100)]
    public sealed class EFMapNode : MonoBehaviour
    {
        public EFMapWorld world;
        public string label;
        public int stage;
        public MapKind kind;
        public bool interactive = true;
        public bool pulseAtRoot;
        public EFMapAnimation[] actions = Array.Empty<EFMapAnimation>();
        public EFMapNode supply;
        public Transform motion;
        public Transform handle;
        public Transform launchTarget;
        public Transform rideVisual;
        public Transform[] ridePath = Array.Empty<Transform>();
        public Renderer[] indicators = Array.Empty<Renderer>();
        public Collider[] support = Array.Empty<Collider>();
        public Renderer waterSurface;
        public Material waterMaterial, iceMaterial;
        public Vector3 axis = Vector3.up;
        public float speed = 28, amplitude = 25, initialAngle = 35, tolerance = 8;
        public bool Started { get; private set; }
        public bool Done { get; private set; }
        public bool Frozen { get; private set; }
        public bool SourceOn { get; private set; }
        public float Angle { get; private set; }
        public bool Aligned => kind == MapKind.Debris ? Mathf.Abs(offset) <= .55f : Mathf.Abs(Mathf.DeltaAngle(Angle, 0)) <= tolerance;
        public bool Filled => kind == MapKind.Water && supply != null && supply.Done;
        public bool Satisfied => kind == MapKind.Debris || kind == MapKind.Slate || kind == MapKind.Bridge ? Frozen && Aligned :
            kind == MapKind.Water ? Filled && Frozen : Done;
        public Vector3 HandlePosition => handle == null ? transform.position : handle.position;
        public Vector3 SwingVelocity { get; private set; }
        public string Status => label + (kind == MapKind.Debris ? $" | target offset {offset:F1} m" :
            kind == MapKind.Bridge || kind == MapKind.Slate ? $" | {Angle:F0} degrees" : "") +
            (Frozen ? " | FROZEN" : Done ? " | READY" : Started ? " | IN PROGRESS" : "");
        Vector3 home;
        Quaternion rotation;
        Vector3 leafHome;
        Quaternion leafRotation;
        float clock, offset;
        bool initialized;
        MaterialPropertyBlock colors;
        void Awake() { Initialize(); ResetNode(); }
        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            if (motion == null) motion = transform;
            home = motion.localPosition; rotation = motion.localRotation;
            if (rideVisual != null) { leafHome = rideVisual.position; leafRotation = rideVisual.rotation; }
            colors = new MaterialPropertyBlock();
        }
        public void ResetNode()
        {
            Initialize(); Started = Done = Frozen = SourceOn = false; clock = 0; offset = 0; Angle = initialAngle;
            motion.localPosition = home; motion.localRotation = rotation;
            foreach (var action in actions) if (action != null) action.ResetPose();
            ResetRide(); Tick(0);
        }
        public void ResetRide()
        {
            if (rideVisual == null) return;
            rideVisual.SetPositionAndRotation(leafHome, leafRotation);
        }
        public bool CanApply(Ability ability, out string reason)
        {
            reason = null;
            if (!interactive || world == null || stage != world.StageIndex || world.Completed[stage])
                reason = "This area is not active. Finish the current puzzle first.";
            else if (ability == Ability.Bloom && (kind == MapKind.Flower || kind == MapKind.Vine || kind == MapKind.Root || kind == MapKind.Repair)) { }
            else if (ability == Ability.Pulse && (kind == MapKind.Source || kind == MapKind.Root && pulseAtRoot))
            {
                if (kind == MapKind.Root && !Done) reason = "BLOOM the root and wait for the wire to connect, then PULSE this root.";
            }
            else if (ability == Ability.Freeze && (kind == MapKind.Debris || kind == MapKind.Slate || kind == MapKind.Bridge || kind == MapKind.Water))
            {
                if (kind == MapKind.Water && !Filled) reason = "PULSE the source; wait until the watergate is open.";
                if (kind == MapKind.Bridge && (supply == null || !supply.SourceOn)) reason = "PULSE the bridge source first.";
            }
            else reason = label + " does not respond to " + ability.ToString().ToUpperInvariant() + ".";
            return reason == null;
        }
        public string Apply(Ability ability)
        {
            if (!CanApply(ability, out var reason)) return reason;
            if (ability == Ability.Bloom && (kind == MapKind.Flower || kind == MapKind.Vine || kind == MapKind.Root || kind == MapKind.Repair))
            {
                if (Done) return label + ": already grown. Ready.";
                if (!Started) { Started = true; foreach (var action in actions) action.Begin(); }
                return label + ": growing. Wait for the animation to finish.";
            }
            if (ability == Ability.Pulse && (kind == MapKind.Source || kind == MapKind.Root && pulseAtRoot))
            {
                if (kind == MapKind.Root && !Done) return "BLOOM the root and wait for the wire to connect, then PULSE this root.";
                Started = SourceOn = true;
                if (kind == MapKind.Source) Done = true;
                return label + ": power connected.";
            }
            if (ability == Ability.Freeze && (kind == MapKind.Debris || kind == MapKind.Slate || kind == MapKind.Bridge || kind == MapKind.Water))
            {
                if (kind == MapKind.Water && !Filled) return "PULSE the source; wait until the watergate is open.";
                if (kind == MapKind.Bridge && (supply == null || !supply.SourceOn)) return "PULSE the bridge source first.";
                Frozen = !Frozen;
                // Small timing assistance keeps the original authored solution pose exact.
                if (Frozen && Aligned) { offset = 0; Angle = 0; }
                Tick(0);
                return label + (Frozen ? Aligned || kind == MapKind.Water ? ": frozen in position." : ": wrong alignment. FREEZE again or R to retry." : ": released.");
            }
            return label + " does not respond to " + ability.ToString().ToUpperInvariant() + ".";
        }
        void Update() { Tick(Time.deltaTime); }
        public void Tick(float dt)
        {
            Initialize();
            bool sealedStage = world != null && world.Completed != null && world.Completed[stage];
            if ((kind == MapKind.Gate || kind == MapKind.Door) && supply != null && supply.SourceOn && !Started)
            { Started = true; foreach (var action in actions) action.Begin(); }
            if (Started && actions.Length > 0)
            {
                Done = true; foreach (var action in actions) Done &= action != null && action.Complete;
            }
            if (!Frozen && !sealedStage)
            {
                if (kind == MapKind.Debris) { clock += dt; offset = 5 - Mathf.Repeat(clock * 1.7f, 10); }
                if (kind == MapKind.Slate) { clock += dt; Angle = Mathf.Sin(clock * 1.1f + .8f) * amplitude; }
                if (kind == MapKind.Bridge && supply != null && supply.SourceOn) Angle = Mathf.Repeat(Angle + speed * dt, 360);
            }
            if (kind == MapKind.Debris) motion.localPosition = home + Vector3.up * offset;
            if (kind == MapKind.Bridge || kind == MapKind.Slate) motion.localRotation = rotation * Quaternion.AngleAxis(Angle, axis.normalized);
            if (kind == MapKind.Vine && Done && !sealedStage)
            {
                Vector3 previous = HandlePosition;
                clock += dt;
                Angle = -Mathf.Cos(clock * 1.4f) * amplitude;
                motion.localRotation = rotation * Quaternion.AngleAxis(Angle, axis.normalized);
                SwingVelocity = dt > .00001f ? (HandlePosition - previous) / dt : Vector3.zero;
            }
            foreach (var body in support)
                if (body != null) body.enabled = kind == MapKind.Flower ? Done :
                    kind == MapKind.Debris ? Frozen : kind == MapKind.Water ? Filled && Frozen : true;
            if (waterSurface != null)
            {
                waterSurface.enabled = Filled;
                waterSurface.sharedMaterial = Frozen ? iceMaterial : waterMaterial;
            }
            Color color = Frozen ? new Color(.2f, .85f, 1) : SourceOn || Done ? new Color(.4f, 1, .4f) : new Color(.65f, .55f, .3f);
            colors.SetColor("_BaseColor", color); colors.SetColor("_EmissionColor", color * (SourceOn ? 1.5f : .05f));
            foreach (var indicator in indicators) if (indicator != null) indicator.SetPropertyBlock(colors);
        }
    }
}
