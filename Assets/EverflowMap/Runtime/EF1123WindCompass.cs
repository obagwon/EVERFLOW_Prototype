using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EverflowMap
{
    internal sealed class EF1123WindCompass
    {
        const int SolidMask = ~(1 << 2);
        const float Radius = 1.58f;
        static readonly Color Ready = new(1f, .69f, .25f);
        static readonly Color Idle = new(.30f, .59f, .56f);
        static readonly Color Edge = new(.095f, .21f, .21f);

        readonly GameObject root;
        readonly Mesh mesh;
        readonly Material material;
        readonly MeshRenderer[] arrows = new MeshRenderer[4];
        readonly Vector3[] vertices;
        readonly int[] triangles;
        readonly MaterialPropertyBlock properties = new();

        public EF1123WindCompass(Transform parent, Shader shader)
        {
            root = new GameObject("FLOW | 3D direction arrows");
            root.layer = 2;
            root.transform.SetParent(parent, false);
            mesh = CreateMesh();
            vertices = mesh.vertices;
            triangles = mesh.triangles;
            material = new Material(shader) { name = "FLOW arrow finish" };
            var names = new[] { "Forward", "Right", "Back", "Left" };
            for (int i = 0; i < arrows.Length; i++)
            {
                var arrow = new GameObject(names[i], typeof(MeshFilter), typeof(MeshRenderer));
                arrow.layer = 2;
                arrow.transform.SetParent(root.transform, false);
                arrow.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = arrow.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { material, material };
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                arrows[i] = renderer;
            }
            Hide();
        }

        public int Show(EF1123World world, Ray pointer)
        {
            root.SetActive(true);
            Vector3 center = world.player.transform.position + Vector3.up * .30f;
            int hovered = -1;
            float nearest = 20f;
            var view = world.player.view;
            var viewForward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
            bool blocked = Physics.Raycast(pointer, out var obstruction, nearest, SolidMask, QueryTriggerInteraction.Ignore);
            if (blocked) nearest = obstruction.distance + .02f;
            for (int i = 0; i < arrows.Length; i++)
            {
                Vector3 direction = world.WindDirection(i);
                float radius = Radius;
                if (Physics.Raycast(center, direction, out var wall, Radius + .65f, SolidMask, QueryTriggerInteraction.Ignore))
                    radius = Mathf.Clamp(wall.distance - .58f, .68f, Radius);
                var arrow = arrows[i];
                float depth = Vector3.Dot(direction, viewForward);
                var height = Vector3.up * (.36f * depth);
                var tilt = Quaternion.LookRotation(direction + Vector3.up * (.32f * depth));
                arrow.transform.SetPositionAndRotation(center + direction * radius + height, tilt);
                arrow.transform.localScale = Vector3.one * Mathf.Lerp(.53f, .80f, radius / Radius);
                if (view.WorldToViewportPoint(arrow.transform.position).z <= view.nearClipPlane ||
                    !arrow.bounds.IntersectRay(pointer)) continue;
                if (!EF1123ModelAim.Intersect(pointer, arrow.transform, vertices, triangles, nearest, out float distance))
                    continue;
                nearest = distance;
                hovered = i;
            }
            for (int i = 0; i < arrows.Length; i++)
            {
                bool active = world.PreviewWindReceiver(i) != null;
                var tint = active ? Ready : Idle;
                if (i == hovered) tint = Color.Lerp(tint, Color.white, .34f);
                Paint(arrows[i], 0, tint, i == hovered ? .28f : active ? .16f : .06f);
                Paint(arrows[i], 1, Color.Lerp(Edge, tint, .37f), .02f);
            }
            return hovered;
        }

        void Paint(MeshRenderer renderer, int submesh, Color tint, float emission)
        {
            properties.SetColor("_Tint", tint);
            properties.SetFloat("_Glow", emission);
            renderer.SetPropertyBlock(properties, submesh);
        }

        public void Hide() => root.SetActive(false);

        public void Dispose()
        {
            Object.Destroy(root);
            Object.Destroy(mesh);
            Object.Destroy(material);
        }

        static Mesh CreateMesh()
        {
            Vector2[] outline =
            {
                new(-.16f, -.44f), new(.16f, -.44f), new(.16f, .03f),
                new(.41f, .03f), new(0, .65f), new(-.41f, .03f), new(-.16f, .03f)
            };
            var vertices = new List<Vector3>(outline.Length * 3);
            foreach (var y in new[] { -.08f, .045f, .13f })
                foreach (var p in outline)
                {
                    float taper = y > .1f ? .88f : 1f;
                    vertices.Add(new Vector3(p.x * taper, y, p.y * taper));
                }

            int count = outline.Length;
            var top = new List<int> { 14, 16, 15, 14, 20, 16, 17, 19, 18 };
            var sides = new List<int> { 0, 1, 2, 0, 2, 6, 3, 4, 5 };
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                sides.AddRange(new[] { i, i + count, next, next, i + count, next + count });
                top.AddRange(new[] { i + count, i + count * 2, next + count,
                    next + count, i + count * 2, next + count * 2 });
            }
            var mesh = new Mesh { name = "Bevelled FLOW arrow", subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(sides, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
