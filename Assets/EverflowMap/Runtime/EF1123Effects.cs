using System;
using System.Collections.Generic;
using EverflowPuzzleLab;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

namespace EverflowMap
{
    [Serializable]
    public sealed class EF1123DeviceFeedback
    {
        public EF1123Target target;
        public Renderer[] surfaces;
        public Transform impactPoint;
        public Transform focus;
        public Transform cameraPose;
    }

    [DisallowMultipleComponent, DefaultExecutionOrder(500)]
    public sealed class EF1123Effects : MonoBehaviour
    {
        public static readonly Color[] Colors = EFV2AbilityTargets.Colors;
        public EF1123World world;
        public EF1123Audio audio;
        public Material targetGlow, particleMaterial, trailMaterial;
        public EF1123DeviceFeedback[] devices;
        public Transform[] waterSources;
        public CinemachineCamera cutaway;

        sealed class Surface
        {
            public EF1123Target target;
            public Renderer source;
            public MeshRenderer overlay;
            public float impactTime = -10f;
            public Vector3? impactPoint;
        }

        readonly List<Surface> surfaces = new();
        readonly Dictionary<EF1123Target, EF1123DeviceFeedback> bindings = new();
        readonly Dictionary<Transform, TrailRenderer> trails = new();
        readonly Queue<CutawayShot> pendingShots = new();
        readonly ParticleSystem[] bursts = new ParticleSystem[3];
        MaterialPropertyBlock properties;
        bool heldParticles, grounded;
        int nextBurst, teleports, cueStartFrame;
        float cueTime, effectTime, mechanismDelay;
        public int HighlightCount { get; private set; }
        internal bool ShowingDoor { get; private set; }

        readonly struct CutawayShot
        {
            public readonly Transform pose;
            public readonly float seconds;
            public readonly EF1123Motion[] doors;

            public CutawayShot(Transform pose, float seconds, EF1123Motion[] doors = null)
            {
                this.pose = pose;
                this.seconds = seconds;
                this.doors = doors;
            }

            public void OpenDoors()
            {
                if (doors == null) return;
                foreach (var door in doors) door.Open();
            }
        }

        void Start()
        {
            properties = new MaterialPropertyBlock();
            foreach (var device in devices)
                if (device.target != null) bindings[device.target] = device;
            foreach (var target in world.GetComponentsInChildren<EF1123Target>(true))
            {
                if (world.UsesModelAim)
                    foreach (var model in target.aimModels) AddSurface(target, model);
                else
                    AddSurface(target, target.GetComponent<Renderer>());
                if (bindings.TryGetValue(target, out var device))
                    foreach (var renderer in device.surfaces) AddSurface(target, renderer);
            }
            for (int i = 0; i < world.orbs.Length; i++)
                trails[world.orbs[i]] = MakeTrail(world.orbs[i], Colors[i]);
            for (int i = 0; i < bursts.Length; i++) bursts[i] = CreateBurst();
            teleports = world.player.TeleportVersion;
        }

        void AddSurface(EF1123Target target, Renderer source)
        {
            if (source == null || !source.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) return;
            if (surfaces.Exists(surface => surface.target == target && surface.source == source)) return;
            var node = new GameObject("Ability glow", typeof(MeshFilter), typeof(MeshRenderer));
            node.layer = source.gameObject.layer;
            node.transform.SetParent(source.transform, false);
            node.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = node.GetComponent<MeshRenderer>();
            var materials = new Material[filter.sharedMesh.subMeshCount];
            Array.Fill(materials, targetGlow);
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            surfaces.Add(new Surface {target = target, source = source, overlay = renderer});
        }

        TrailRenderer MakeTrail(Transform orb, Color color)
        {
            var trail = orb.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.time = .19f;
            trail.startWidth = .19f;
            trail.endWidth = .014f;
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0);
            trail.emitting = false;
            return trail;
        }

        ParticleSystem CreateBurst()
        {
            var node = new GameObject("Ability impact");
            node.transform.SetParent(transform, false);
            var system = node.AddComponent<ParticleSystem>();
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 38;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.32f, .65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.65f, 2.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(.045f, .13f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .16f;
            var emission = system.emission;
            emission.enabled = false;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return system;
        }

        public void Select() => audio.Play(EF1123Sound.Select, .62f);
        public void ChooseFlow() => audio.Play(EF1123Sound.MenuChoice, .56f);
        public void Launch(Transform orb)
        {
            if (trails.TryGetValue(orb, out var trail))
            {
                trail.Clear();
                trail.emitting = true;
            }
            audio.Play(EF1123Sound.Launch, .63f);
        }
        public void ProjectileArrived(LabAbility ability)
        {
            if (trails.TryGetValue(world.orbs[(int)ability], out var trail)) trail.emitting = false;
        }
        public void Jump() => audio.Play(EF1123Sound.Jump, .9f);
        public void Fall() => audio.Play(EF1123Sound.Fall, .75f);
        public void Recovery()
        {
            foreach (var trail in trails.Values) { trail.emitting = false; trail.Clear(); }
            audio.Play(EF1123Sound.Recovery, .70f);
        }

        public void Impact(EF1123Target target, Vector3? hitPoint = null)
        {
            int ability = (int)target.ability;
            bindings.TryGetValue(target, out var device);
            Vector3 point = hitPoint ?? (device != null && device.impactPoint != null ? device.impactPoint.position : target.transform.position);
            foreach (var surface in surfaces)
                if (surface.target == target)
                {
                    surface.impactTime = effectTime;
                    surface.impactPoint = point;
                }
            Emit(point, Colors[ability], 21);
            audio.Play(ability == 0 ? EF1123Sound.Bloom : ability == 1 ? EF1123Sound.Freeze : EF1123Sound.Gust, .85f);
            if (target.motions.Length > 0) mechanismDelay = .18f;
            if (device != null && device.cameraPose != null && device.focus != null)
                StartCue(device.cameraPose, device.focus);
        }

        public void Reward(Vector3 position, bool finalStar)
        {
            Emit(position, new Color(1, .82f, .38f), 32);
            audio.Play(EF1123Sound.Star);
            if (finalStar) audio.Play(EF1123Sound.Door, .88f);
        }

        public void Finish()
        {
            CancelCue();
            audio.SetTheme(EF1123Audio.Theme.Ending);
            audio.Ambience(0, true);
        }

        void Emit(Vector3 point, Color tint, int count)
        {
            var burst = bursts[nextBurst++ % bursts.Length];
            burst.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            burst.transform.position = point;
            var main = burst.main;
            main.startColor = tint;
            burst.Play();
            burst.Emit(count);
        }

        void StartCue(Transform pose, Transform focus)
        {
            if (cutaway == null || world.player.Traversing || ShowingDoor) return;
            Vector3 view = world.player.view.WorldToViewportPoint(focus.position);
            if (view.z > 0 && view.x > .16f && view.x < .84f && view.y > .15f && view.y < .82f)
            {
                Vector3 from = world.player.view.transform.position;
                Vector3 to = focus.position - from;
                if (!Physics.Raycast(from, to.normalized, to.magnitude - .20f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    return;
            }
            ShowCutaway(pose, 1.55f);
        }

        internal void ShowCutaway(Transform pose, float seconds)
            => ScheduleCutaway(new CutawayShot(pose, seconds));

        internal void ShowDoorOpening(Transform pose, params EF1123Motion[] doors)
            => ScheduleCutaway(new CutawayShot(pose, 3f, doors));

        void ScheduleCutaway(CutawayShot shot)
        {
            if (cutaway == null || shot.pose == null) { shot.OpenDoors(); return; }
            if (cueTime > 0f) pendingShots.Enqueue(shot);
            else StartCutaway(shot);
        }

        void StartCutaway(CutawayShot shot)
        {
            cutaway.transform.SetPositionAndRotation(shot.pose.position, shot.pose.rotation);
            cutaway.PreviousStateIsValid = false;
            cutaway.Priority = 220;
            cueTime = shot.seconds;
            cueStartFrame = Time.frameCount;
            ShowingDoor = shot.doors != null;
            world.SetCinematic(true);
            shot.OpenDoors();
        }

        public void CancelCue()
        {
            while (pendingShots.TryDequeue(out var shot)) shot.OpenDoors();
            if (!world.Cinematic) return;
            cueTime = 0;
            ShowingDoor = false;
            if (cutaway != null) cutaway.Priority = -40;
            world.player.cinemachineRig.ResetView();
            world.SetCinematic(false);
        }

        void LateUpdate()
        {
            bool stopped = world.AbilityMode || world.Paused || world.Finished;
            if (!stopped)
            {
                effectTime += Time.deltaTime;
                if (cueTime > 0 && Time.frameCount != cueStartFrame && (cueTime -= Time.deltaTime) <= 0)
                {
                    if (pendingShots.TryDequeue(out var shot)) StartCutaway(shot);
                    else CancelCue();
                }
                if (mechanismDelay > 0 && (mechanismDelay -= Time.deltaTime) <= 0)
                    audio.Play(EF1123Sound.Mechanism, .54f);
            }
            if (heldParticles != stopped)
            {
                foreach (var burst in bursts)
                {
                    if (stopped && burst.isPlaying) burst.Pause(false);
                    else if (!stopped && burst.isPaused) burst.Play(false);
                }
                heldParticles = stopped;
            }
            RefreshSurfaces();
            UpdateMovementAudio(stopped || world.Cinematic);
            var position = world.player.transform.position;
            float water = 0;
            foreach (var source in waterSources)
                water = Mathf.Max(water, 1 - Vector3.Distance(position, source.position) / 16);
            audio.Ambience(water, world.Paused || world.Finished || world.AbilityMode);
        }

        void RefreshSurfaces()
        {
            HighlightCount = 0;
            foreach (var surface in surfaces)
            {
                var platform = surface.target.timedPlatform;
                bool valid = world.AbilityMode && (platform == null || platform.IsAligned) && world.CanCast(surface.target, out _);
                float progress = (effectTime - surface.impactTime) / .62f;
                bool impact = !world.AbilityMode && progress < 1;
                var renderer = surface.overlay;
                renderer.enabled = surface.source.enabled && surface.source.gameObject.activeInHierarchy && (valid || impact);
                if (!renderer.enabled) continue;
                if (valid) HighlightCount++;
                bool focused = surface.target == world.Hovered || surface.target == world.PendingFlow;
                properties.Clear();
                properties.SetColor("_GlowColor", Colors[(int)surface.target.ability]);
                properties.SetFloat("_Strength", impact || focused ? 1 : .72f);
                properties.SetFloat("_Pulse", valid ? .94f + .06f * Mathf.Sin(Time.unscaledTime * 3) : 1);
                properties.SetFloat("_WaveProgress", impact ? progress : -1);
                if (impact)
                {
                    var bounds = surface.source.bounds;
                    var p = bounds.ClosestPoint(surface.impactPoint ?? surface.target.transform.position);
                    properties.SetVector("_WaveOrigin", new Vector4(p.x, p.y, p.z, 1 / Mathf.Max(.1f, bounds.extents.magnitude * 2)));
                }
                renderer.SetPropertyBlock(properties);
            }
        }

        void UpdateMovementAudio(bool stopped)
        {
            var player = world.player;
            if (teleports != player.TeleportVersion)
            {
                foreach (var trail in trails.Values) { trail.emitting = false; trail.Clear(); }
                teleports = player.TeleportVersion;
                grounded = player.Grounded;
                return;
            }
            if (stopped) return;
            if (player.Grounded)
            {
                if (!grounded) audio.Play(EF1123Sound.Land, .63f);
            }
            else if (grounded && player.VerticalSpeed > 1 && !player.Traversing) Jump();
            grounded = player.Grounded;
        }
    }
}
