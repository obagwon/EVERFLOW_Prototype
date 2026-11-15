using System.Collections.Generic;
using UnityEngine;

namespace EverflowMap
{
    [DefaultExecutionOrder(950), DisallowMultipleComponent]
    public sealed class EFV2CharacterCameraFade : MonoBehaviour
    {
        [SerializeField] EFV2PuzzleWorld world;
        [SerializeField] Shader fadeShader;

        const float FadeStart = 2.35f;
        const float FadeEnd = .85f;
        const float MinimumOpacity = .08f;
        static readonly int CameraOpacity = Shader.PropertyToID("_CameraOpacity");

        readonly List<(SkinnedMeshRenderer renderer, Material[] original, Material[] faded)> bodies = new();
        readonly Dictionary<Material, Material> fadeMaterials = new();
        int lastTeleport = -1;
        bool usingFade;

        public float Opacity { get; private set; } = 1;

        void Awake()
        {
            if (world == null || world.player == null || fadeShader == null || !fadeShader.isSupported)
            {
                enabled = false;
                return;
            }

            foreach (var renderer in world.player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var original = renderer.sharedMaterials;
                var faded = new Material[original.Length];
                for (int slot = 0; slot < original.Length; slot++)
                {
                    var source = original[slot];
                    if (source == null || !source.HasProperty("_BaseMap"))
                    {
                        faded[slot] = source;
                        continue;
                    }

                    if (!fadeMaterials.TryGetValue(source, out var material))
                    {
                        material = new Material(fadeShader) { name = source.name + " (camera fade)" };
                        material.CopyMatchingPropertiesFromMaterial(source);
                        fadeMaterials.Add(source, material);
                    }
                    faded[slot] = material;
                }
                bodies.Add((renderer, original, faded));
            }
        }

        void LateUpdate()
        {
            if (!world.Built || world.OpeningActive || world.Mode == EFV2InteractionMode.Cinematic)
            {
                SetOpacity(1);
                return;
            }

            var player = world.player;
            var focus = player.cinemachineRig.virtualCamera.LookAt;
            float distance = Vector3.Distance(player.view.transform.position, focus.position);
            float target = Mathf.SmoothStep(MinimumOpacity, 1, Mathf.InverseLerp(FadeEnd, FadeStart, distance));

            // Continue a short visual transition while the ability cursor freezes the simulation.
            float duration = target < Opacity ? .14f : .24f;
            float next = lastTeleport == player.TeleportVersion
                ? Mathf.MoveTowards(Opacity, target, Time.unscaledDeltaTime / duration)
                : target;
            lastTeleport = player.TeleportVersion;
            SetOpacity(next);
        }

        void SetOpacity(float opacity)
        {
            bool needsFade = opacity < 1;
            if (needsFade && opacity != Opacity)
                foreach (var material in fadeMaterials.Values) material.SetFloat(CameraOpacity, opacity);

            if (needsFade != usingFade)
            {
                foreach (var body in bodies)
                    if (body.renderer != null) body.renderer.sharedMaterials = needsFade ? body.faded : body.original;
                usingFade = needsFade;
            }
            Opacity = opacity;
        }

        void OnDisable() => SetOpacity(1);

        void OnDestroy()
        {
            foreach (var material in fadeMaterials.Values) Destroy(material);
        }
    }
}
