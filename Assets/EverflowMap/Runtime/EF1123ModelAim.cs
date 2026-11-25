using System.Collections.Generic;
using EverflowPuzzleLab;
using UnityEngine;

namespace EverflowMap
{
    internal sealed class EF1123ModelAim
    {
        const int SolidMask = ~(1 << 2);
        const float SurfaceTolerance = .035f;

        internal readonly struct Contact
        {
            public readonly Transform model;
            public readonly Vector3 localPoint;
            public Contact(Transform model, Vector3 point)
            {
                this.model = model;
                localPoint = model.InverseTransformPoint(point);
            }
            public Vector3 Point => model.TransformPoint(localPoint);
        }

        sealed class Surface
        {
            public EF1123Target target;
            public Renderer model;
            public Vector3[] vertices;
            public int[] triangles;
            public bool Visible => target.isActiveAndEnabled && model.enabled &&
                                   !model.forceRenderingOff && model.gameObject.activeInHierarchy;
        }

        readonly List<Surface> surfaces = new();

        public EF1123ModelAim(EF1123World world, EFV2TargetMeshCache cache)
        {
            var geometry = new Dictionary<Mesh, (Vector3[] vertices, int[] triangles)>();
            foreach (var entry in cache.entries)
                geometry[entry.source] = (entry.vertices, entry.triangles);
            foreach (var tutorial in world.tutorials) Add(tutorial, geometry);
            foreach (var course in world.courses)
                foreach (var target in course.steps) Add(target, geometry);
        }

        void Add(EF1123Target target, Dictionary<Mesh, (Vector3[] vertices, int[] triangles)> geometry)
        {
            foreach (var model in target.aimModels)
            {
                if (model == null || !model.TryGetComponent<MeshFilter>(out var filter) ||
                    filter.sharedMesh == null) continue;
                var mesh = filter.sharedMesh;
                if (!geometry.TryGetValue(mesh, out var data))
                {
                    if (!mesh.isReadable) continue;
                    data = (mesh.vertices, mesh.triangles);
                    geometry.Add(mesh, data);
                }
                surfaces.Add(new Surface { target = target, model = model, vertices = data.vertices, triangles = data.triangles });
            }
        }

        public EF1123Target Pick(Ray ray, float maximum, LabAbility selected, out Contact contact)
        {
            var obstacles = Physics.RaycastAll(ray, maximum, SolidMask, QueryTriggerInteraction.Ignore);
            EF1123Target best = null;
            float nearest = maximum;
            contact = default;
            foreach (var surface in surfaces)
            {
                if (!surface.Visible || !surface.model.bounds.IntersectRay(ray, out float entry) ||
                    entry > nearest + SurfaceTolerance) continue;
                if (!Intersect(ray, surface.model.transform, surface.vertices, surface.triangles,
                        nearest + SurfaceTolerance, out float distance)) continue;
                if (best != null && distance >= nearest - SurfaceTolerance &&
                    (surface.target.ability != selected || best.ability == selected)) continue;
                if (Blocked(surface.target, obstacles, distance)) continue;
                best = surface.target;
                nearest = distance;
                contact = new Contact(surface.model.transform, ray.GetPoint(distance));
            }
            return best;
        }

        public bool CanReach(EF1123Target target, Vector3 origin, Vector3 point)
        {
            var direction = point - origin;
            float distance = direction.magnitude;
            if (distance <= SurfaceTolerance) return true;
            var obstacles = Physics.RaycastAll(origin, direction / distance, distance, SolidMask, QueryTriggerInteraction.Ignore);
            return !Blocked(target, obstacles, distance);
        }

        public bool HasReachableModel(EF1123Target target, Vector3 origin, float range)
        {
            foreach (var surface in surfaces)
            {
                if (surface.target != target || !surface.Visible) continue;
                var point = surface.model.bounds.ClosestPoint(origin);
                if ((point - origin).sqrMagnitude <= range * range && CanReach(target, origin, point)) return true;
            }
            return false;
        }

        static bool Blocked(EF1123Target target, RaycastHit[] obstacles, float distance)
        {
            foreach (var hit in obstacles)
            {
                if (hit.distance >= distance - SurfaceTolerance) continue;
                bool ownSurface = false;
                foreach (var model in target.aimModels)
                    if (model != null && hit.transform == model.transform)
                    {
                        ownSurface = true;
                        break;
                    }
                if (!ownSurface) return true;
            }
            return false;
        }

        internal static bool Intersect(Ray ray, Transform model, Vector3[] vertices, int[] triangles, float maximum, out float distance)
        {
            var origin = model.InverseTransformPoint(ray.origin);
            var direction = model.InverseTransformVector(ray.direction);
            distance = maximum;
            bool found = false;
            for (int i = 0; i < triangles.Length; i += 3)
                if (EFV2AbilityTargets.Triangle(origin, direction, vertices[triangles[i]], vertices[triangles[i+1]],
                    vertices[triangles[i+2]], out float hit) && hit < distance)
                {
                    distance = hit;
                    found = true;
                }
            return found;
        }
    }
}
