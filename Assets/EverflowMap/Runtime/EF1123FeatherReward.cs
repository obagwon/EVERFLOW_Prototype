using UnityEngine;
using UnityEngine.Rendering;

namespace EverflowMap
{
    public sealed class EF1123FeatherReward : MonoBehaviour
    {
        static readonly int BurstStart = Shader.PropertyToID("_BurstStart");
        Material glow;
        Material feather;
        Mesh quad;

        public void Attach(EF1123Course[] courses)
        {
            var model = Resources.Load<GameObject>("EverflowReward/Feather");
            var filter = model == null ? null : model.GetComponentInChildren<MeshFilter>();
            var shader = Resources.Load<Shader>("EverflowReward/FeatherRays");
            if (filter == null || filter.sharedMesh == null || shader == null)
            {
                Debug.LogWarning("Breath fragment art could not be loaded.", this);
                return;
            }

            glow = new Material(shader);
            quad = new Mesh { name = "Breath fragment rays" };
            quad.vertices = new[] { new Vector3(-1, -1), new Vector3(1, -1),
                new Vector3(-1, 1), new Vector3(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 6.4f);

            foreach (var course in courses)
            {
                if (course.star == null || !course.star.TryGetComponent<MeshFilter>(out var starMesh) ||
                    !course.star.TryGetComponent<MeshRenderer>(out var starRenderer) || starRenderer.sharedMaterial == null)
                    continue;
                if (feather == null)
                {
                    feather = new Material(starRenderer.sharedMaterial);
                    feather.SetFloat("_Cull", (float)CullMode.Off);
                    feather.SetColor("_BaseColor", new Color(1f, .89f, .64f));
                    feather.EnableKeyword("_EMISSION");
                    feather.SetColor("_EmissionColor", new Color(.54f, .35f, .12f));
                }
                starRenderer.sharedMaterial = feather;
                starMesh.sharedMesh = filter.sharedMesh;
                var rays = CreateRays();
                rays.transform.SetParent(course.star, false);
            }
        }

        MeshRenderer CreateRays()
        {
            var node = new GameObject("Breath fragment rays");
            node.AddComponent<MeshFilter>().sharedMesh = quad;
            var renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        public void PlayPickup(Vector3 position)
        {
            if (glow == null) return;
            var rays = CreateRays();
            rays.transform.SetParent(transform);
            rays.transform.position = position;
            var properties = new MaterialPropertyBlock();
            properties.SetFloat(BurstStart, Time.timeSinceLevelLoad);
            rays.SetPropertyBlock(properties);
            Destroy(rays.gameObject, .40f);
        }

        void OnDestroy()
        {
            if (glow != null) Destroy(glow);
            if (feather != null) Destroy(feather);
            if (quad != null) Destroy(quad);
        }
    }
}
