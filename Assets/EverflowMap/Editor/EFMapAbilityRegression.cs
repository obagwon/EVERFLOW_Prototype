using System;
using System.Collections;
using UnityEngine;
using EFInput = EverflowGraybox.EFInput;

namespace EverflowMap.Editor
{
    // Runtime fixtures are isolated from the authored map; no saved assets edited.
    internal static class EFMapAbilityRegression
    {
        static int injectedClickFrame;
        static IEnumerator Frames(int count, KeyCode key = KeyCode.None, bool click = false, Vector2 look = default)
        {
            // Coroutines resume after Update, before LateUpdate. Select first so
            // a click observed this LateUpdate uses the intended ability.
            if (key != KeyCode.None || look != Vector2.zero)
            {
                EFInput.TestPress = key; EFMapInput.TestLookDelta = look;
                yield return null;
                EFInput.TestPress = KeyCode.None; EFMapInput.TestLookDelta = Vector2.zero;
            }
            EFMapInput.TestClick = click;
            if (click) injectedClickFrame = Time.frameCount;
            for (int i = 0; i < count; i++)
            {
                yield return null;
                EFInput.TestPress = KeyCode.None; EFMapInput.TestClick = false; EFMapInput.TestLookDelta = Vector2.zero;
            }
        }
        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, true); go.transform.position = position; go.transform.localScale = size; return go;
        }
        public static IEnumerator Run(EFMapWorld world, EFMapPlayer player, Action<bool, string> check)
        {
            var fixtures = new GameObject("Ability regression fixtures");
            GameObject self = null;
            var ignored = player.abilityIgnoreRoots;
            try
            {
                Box(fixtures.transform, "Isolated floor", new Vector3(200, 80, 200), new Vector3(80, 1, 80));
                player.Teleport(new Vector3(200, 81.6f, 200));
                player.follow.yaw = 0; player.follow.pitch = 0; player.follow.Snap();
                yield return Frames(30);
                var root = new GameObject("Logical receiver"); root.transform.SetParent(fixtures.transform);
                var node = root.AddComponent<EFMapNode>(); node.world = world; node.stage = 0; node.label = "Regression flower";
                var nested = new GameObject("FBX level 1").transform; nested.SetParent(root.transform);
                var nested2 = new GameObject("FBX level 2").transform; nested2.SetParent(nested);
                var point = player.view.transform.position + player.view.transform.forward * 12;
                var visible = Box(nested2, "FBX collider child", point, Vector3.one);
                var body = visible.GetComponent<Collider>(); Physics.SyncTransforms();
                check(EFMapAbilityTarget.Resolve(body) == node, "REGRESSION: nested FBX collider resolves logical parent");
                yield return Frames(1, KeyCode.Alpha1, true);
                check(player.Hovered == node && node.Started, "REGRESSION: center ray and injected BLOOM click reach nested child");

                node.ResetNode();
                yield return Frames(1, KeyCode.Alpha3, true);
                check(!node.Started && player.LastCastResult.Contains("does not respond"), "REGRESSION: wrong ability is blocked with reason");
                node.stage = 1;
                yield return Frames(1, KeyCode.Alpha1, true);
                check(!node.Started && player.Hovered == node && player.LastCastResult.Contains("not active"), "REGRESSION: inactive stage stays detected but cannot be used");
                node.stage = 0;

                var trigger = Box(fixtures.transform, "Unrelated trigger", Vector3.Lerp(player.view.transform.position, point, .6f), Vector3.one);
                trigger.GetComponent<Collider>().isTrigger = true;
                yield return Frames(1, click: true);
                check(node.Started, "REGRESSION: unrelated trigger does not swallow the aim ray");
                trigger.SetActive(false); node.ResetNode();

                self = Box(player.transform, "Player child on Default layer", player.transform.position + Vector3.forward * 1.3f + Vector3.up * .65f, Vector3.one);
                self.layer = 0;
                yield return Frames(1, click: true);
                check(node.Started, "REGRESSION: own child collider is ignored by both camera ray and player line of sight");
                UnityEngine.Object.Destroy(self); self = null; node.ResetNode(); yield return Frames(1);
                var orb = Box(fixtures.transform, "Ignored orb", Vector3.Lerp(player.transform.position + Vector3.up * .5f, point, .5f), Vector3.one);
                player.abilityIgnoreRoots = new[] { orb.transform };
                yield return Frames(1, click: true);
                check(node.Started, "REGRESSION: explicit ability ignore root excludes an independent orb");
                orb.SetActive(false); player.abilityIgnoreRoots = ignored; node.ResetNode();

                var wall = Box(fixtures.transform, "Solid aim wall", Vector3.Lerp(player.view.transform.position, point, .7f), new Vector3(2, 3, .3f));
                yield return Frames(1, click: true);
                check(!node.Started && player.Hovered == null && player.LastCastResult.Contains("Solid aim wall"), "REGRESSION: solid world occlusion prevents detection and reports collider");
                wall.SetActive(false);
                Vector3 origin = player.transform.position + Vector3.up * .5f;
                wall = Box(fixtures.transform, "Player LOS wall", Vector3.Lerp(origin, point, .12f), new Vector3(.18f, .25f, .15f));
                yield return Frames(1, click: true);
                check(!node.Started && player.Hovered == node && player.LastCastResult.Contains("blocked by"), "REGRESSION: visible target behind player-side obstacle cannot be cast on");
                wall.SetActive(false);

                visible.transform.position = player.view.transform.position + player.view.transform.forward * 30;
                yield return Frames(1, click: true);
                check(!node.Started && player.Hovered == node && player.LastCastResult.Contains("Too far"), "REGRESSION: detected target outside player range is rejected");
                visible.transform.position = point;
                var link = visible.AddComponent<EFMapAbilityTarget>(); link.receiver = node;
                visible.transform.SetParent(fixtures.transform, true);
                yield return Frames(1, click: true);
                check(node.Started && player.Hovered == node, "REGRESSION: explicit receiver survives separated model and functionality parents");
                link.receiver = null; node.ResetNode();
                yield return Frames(1, click: true);
                check(!node.Started && player.Hovered == null, "REGRESSION: null receiver keeps solid non-target geometry blocking");
                link.receiver = node;

                float yaw = player.follow.yaw;
                player.follow.yaw = yaw - 20; player.follow.Snap(); yield return Frames(2);
                yield return Frames(1, KeyCode.Alpha1, true, new Vector2(20 / player.follow.sensitivity, 0));
                check(player.Hovered == node && node.Started && player.LastCastFrame == injectedClickFrame,
                    "REGRESSION: same-frame mouse orbit and click use rendered camera ray after LateUpdate (no next-frame catch-up)");
            }
            finally
            {
                if (self != null) UnityEngine.Object.Destroy(self);
                UnityEngine.Object.Destroy(fixtures); player.abilityIgnoreRoots = ignored;
                EFMapInput.TestClick = false; EFMapInput.TestLookDelta = Vector2.zero; EFInput.TestPress = KeyCode.None;
                world.Respawn();
            }
            yield return Frames(3);
        }
    }
}
