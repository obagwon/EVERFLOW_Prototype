using UnityEngine;
using EverflowMap;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EverflowPuzzleLab
{
    [DefaultExecutionOrder(150)]
    public sealed class PuzzleLab : MonoBehaviour
    {
        public EFMapPlayer player;
        public LabArea[] areas;
        public LabAbility selected;
        public float castRange = 14;
        public LabTarget[] slots = new LabTarget[3];
        public Transform[] orbs = new Transform[3];
        public LabArea CurrentArea { get; private set; }
        public LabTarget Hovered { get; private set; }
        public string AimMessage { get; private set; }
        public string Message { get; private set; }
        public bool navigationUsed;
        public LabTarget choosing;
        public int previewDirection;
        bool menu;
        bool showDebug;
        Vector2 debugScroll;
        Vector3 hitPoint;
        LabDirection arrow;
        GUIStyle textStyle;
#if UNITY_EDITOR
        public bool testClick, testUndo;
#endif
        void Start() { CurrentArea = areas[0]; ReturnTo(CurrentArea); SetMenu(false); }
        void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        bool ExtraKey(KeyCode key)
        {
#if UNITY_EDITOR
            if (EverflowGraybox.EFInput.TestMode) return EverflowGraybox.EFInput.TestPress == key;
#endif
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null) return false;
            Key input = key == KeyCode.Q ? Key.Q : key == KeyCode.X ? Key.X : key == KeyCode.Backspace ? Key.Backspace :
                key == KeyCode.Tab ? Key.Tab : key == KeyCode.F9 ? Key.F9 : Key.Escape;
            return Keyboard.current[input].wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }
        void SetMenu(bool value)
        {
            menu = value; player.enabled = !value; player.follow.enabled = !value;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = value;
        }
        void Update()
        {
            foreach (var area in areas)
            {
                if (area.bounds.Contains(player.transform.position)) CurrentArea = area;
                area.Evaluate();
            }
            if (CurrentArea != null && player.transform.position.y < CurrentArea.recoveryHeight) ReturnTo(CurrentArea);
            if (EverflowGraybox.EFInput.KeyDown(KeyCode.Alpha1)) { selected = LabAbility.Bloom; choosing = null; }
            if (EverflowGraybox.EFInput.KeyDown(KeyCode.Alpha2)) { selected = LabAbility.Freeze; choosing = null; }
            if (EverflowGraybox.EFInput.KeyDown(KeyCode.Alpha3)) { selected = LabAbility.Flow; choosing = null; }
            if (ExtraKey(KeyCode.Q)) Recall(selected);
            if (ExtraKey(KeyCode.Backspace)) ReturnTo(CurrentArea);
            if (EverflowGraybox.EFInput.KeyDown(KeyCode.R)) CurrentArea.ResetArea();
            if (ExtraKey(KeyCode.F9)) ResetAll();
            if (ExtraKey(KeyCode.Tab)) showDebug = !showDebug;
            for (int i = 0; i < areas.Length; i++)
                if (EverflowGraybox.EFInput.KeyDown(KeyCode.F1 + i)) Navigate(i);
            if (ExtraKey(KeyCode.Escape)) { if (choosing != null) choosing = null; else SetMenu(!menu); }
            if (choosing != null && EverflowGraybox.EFInput.KeyDown(KeyCode.E)) previewDirection = (previewDirection + 1) % choosing.directions.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || !slots[i].isActiveAndEnabled) slots[i] = null;
                if (orbs[i] != null) orbs[i].position = slots[i] != null ? slots[i].transform.position + Vector3.up * 1.2f :
                    player.transform.position + player.transform.right * ((i - 1) * .5f) + Vector3.up * 1.7f;
            }
        }
        void LateUpdate()
        {
            if (menu) return;
            Physics.SyncTransforms();
            Aim();
            bool click = EFMapInput.Click, undo = ExtraKey(KeyCode.X);
#if UNITY_EDITOR
            click |= testClick; undo |= testUndo; testClick = testUndo = false;
#endif
            if (undo) UndoHovered();
            if (!click) return;
            if (Hovered == null) { Message = AimMessage; return; }
            if (selected == LabAbility.Flow)
            {
                if (!CanUse(Hovered, selected, hitPoint, out var reason)) { Message = reason; return; }
                if (arrow != null) { TryApply(Hovered, selected, arrow.index); choosing = null; }
                else if (choosing == Hovered) { TryApply(Hovered, selected, previewDirection); choosing = null; }
                else { choosing = Hovered; previewDirection = Mathf.Max(0, Hovered.Direction); Message = "E: preview next direction; click target/arrow to apply. Esc: cancel."; }
            }
            else if (CanUse(Hovered, selected, hitPoint, out var reason)) TryApply(Hovered, selected);
            else Message = reason;
        }
        void Aim()
        {
            Hovered = null; arrow = null;
            AimMessage = "Aim at a named target. 1 BLOOM / 2 FREEZE / 3 FLOW";
            var ray = player.view.ViewportPointToRay(new Vector3(.5f, .5f));
            var hits = Physics.RaycastAll(ray, 100, ~(1 << 2), QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(player.transform)) continue;
                var target = hit.collider.GetComponentInParent<LabTarget>();
                var direction = hit.collider.GetComponentInParent<LabDirection>();
                if (direction != null) target = direction.target;
                if (hit.collider.isTrigger && target == null) continue;
                if (target == null) { AimMessage = "View blocked: " + hit.collider.name; break; }
                Hovered = target; hitPoint = hit.point; arrow = direction;
                bool can = CanUse(target, selected, hit.point, out var reason);
                AimMessage = target.State + "\n" + (can ? selected == LabAbility.Flow ? "LMB: choose / confirm FLOW" : "LMB: " + (selected == LabAbility.Bloom ? "grow (retained after Q)" : target.water ? "freeze water / block supply" : "fix current pose") : reason);
                if (selected == LabAbility.Freeze && target.Frozen) AimMessage += " | X: " + (target.water ? "THAW / restore supply" : "RELEASE / resume current flow");
                if (selected == LabAbility.Bloom && target.Grown) AimMessage += " | X: shrink / safe return";
                break;
            }
        }
        public bool CanUse(LabTarget target, LabAbility ability, Vector3 point, out string reason)
        {
            reason = null;
            if (target == null || !target.isActiveAndEnabled) reason = "No active target.";
            else if (!target.Accepts(ability)) reason = "Target does not accept " + ability.ToString().ToUpperInvariant();
            else if (slots[(int)ability] != null && slots[(int)ability] != target) reason = "Orb already placed at " + slots[(int)ability].label + ". Q: recall first.";
            else if (Vector3.Distance(player.transform.position, point) > castRange) reason = "Out of range (" + castRange + "m).";
            else
            {
                Vector3 origin = player.transform.position + Vector3.up * .5f, delta = point - origin;
                foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(player.transform)) continue;
                    var other = hit.collider.GetComponentInParent<LabTarget>();
                    var direction = hit.collider.GetComponentInParent<LabDirection>();
                    if (other != target && (direction == null || direction.target != target)) { reason = "Player line blocked by " + hit.collider.name; break; }
                }
            }
            return reason == null;
        }
        // State API deliberately separate from actual center-ray/line-of-sight input tests.
        public bool TryApply(LabTarget target, LabAbility ability, int direction = 0)
        {
            int slot = (int)ability;
            if (slots[slot] == null) slots[slot] = null; // Normalize Unity's destroyed-object null before the managed registry.
            if (target == null || !target.isActiveAndEnabled || !target.Accepts(ability) ||
                (slots[slot] != null && slots[slot] != target) || ability == LabAbility.Flow && (direction < 0 || direction >= target.directions.Length)) return false;
            if (!LabOrbSlots.TryPlace(slots,ability,target)) return false;
            target.Apply(ability, direction); Message = target.State; return true;
        }
        public void Recall(LabAbility ability) { slots[(int)ability] = null; Message = "Recalled " + ability + ": completed changes retained."; }
        public void ReleaseTarget(LabTarget target)
        {
            LabOrbSlots.Release(slots,target);
            if (choosing == target) choosing = null;
        }
        void UndoHovered()
        {
            if (Hovered == null || selected == LabAbility.Flow || !CanUse(Hovered, selected, hitPoint, out _)) return;
            var target = Hovered;
            if (selected == LabAbility.Bloom || target.water) ReturnTo(target.area);
            target.Undo(selected); Message = "Explicit undo: " + target.State;
        }
        public void ReturnTo(LabArea area)
        {
            if (area == null) return;
            CurrentArea = area; choosing = null; player.transform.rotation = area.spawn.rotation; player.Teleport(area.spawn.position);
            Message = "Safe return; puzzle states retained.";
        }
        public void Navigate(int index) { navigationUsed = true; ReturnTo(areas[index]); Message = "DEBUG travel: " + areas[index].title + ". Arrival does not complete a puzzle."; }
        public void ResetAll() { ReturnTo(areas[0]); foreach (var area in areas) area.ResetArea(); foreach (LabAbility a in System.Enum.GetValues(typeof(LabAbility))) Recall(a); navigationUsed = false; }
        void OnGUI()
        {
            if (textStyle == null) textStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, normal = { textColor = Color.white } };
            float scale = Mathf.Clamp(Screen.height / 900f, .65f, 1.5f);
            var previous = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1));
            GUILayout.BeginArea(new Rect(12,12,510,450), GUI.skin.box);
            GUILayout.Label("EVERFLOW v0.2 PUZZLE LAB | " + (CurrentArea != null ? CurrentArea.title : ""), textStyle);
            GUILayout.Label("WASD move | Space jump | mouse look | C recenter\n1 BLOOM  2 FREEZE  3 FLOW | LMB apply | Q recall\nX explicit undo/thaw | E FLOW preview | Esc cursor/menu\nR area reset | F9 all reset | Backspace safe return | F1-F5 travel\nTab: all local target states", textStyle);
            for (int i = 0; i < 3; i++) GUILayout.Label(((int)selected == i ? "> " : "  ") + ((LabAbility)i).ToString().ToUpperInvariant() + ": " + (slots[i] == null ? "AVAILABLE" : "PLACED @ " + slots[i].label), textStyle);
            if (choosing != null) GUILayout.Label("FLOW preview " + (previewDirection+1) + "/" + choosing.directions.Length + ": " + choosing.directions[previewDirection], textStyle);
            GUILayout.Label(Message, textStyle);
            if (CurrentArea != null && CurrentArea.canal != null) GUILayout.Label(CurrentArea.canal.Status, textStyle);
            if (menu)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < areas.Length; i++) if (GUILayout.Button("F" + (i+1))) Navigate(i);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset area")) CurrentArea.ResetArea();
                if (GUILayout.Button("Reset ALL")) ResetAll();
                if (GUILayout.Button("Resume")) SetMenu(false);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
            if(showDebug && CurrentArea!=null)
            {
                GUILayout.BeginArea(new Rect(Screen.width/scale-430,12,415,Screen.height/scale-125),GUI.skin.box);
                GUILayout.Label("LOCAL DEBUG | travel used="+navigationUsed,textStyle);
                debugScroll=GUILayout.BeginScrollView(debugScroll);
                foreach(var target in CurrentArea.GetComponentsInChildren<LabTarget>(true)) GUILayout.Label(target.State,textStyle);
                foreach(var gate in CurrentArea.GetComponentsInChildren<LabGate>()) GUILayout.Label(gate.name+" open="+gate.OpenAmount.ToString("P0")+" latched="+gate.Latched,textStyle);
                GUILayout.EndScrollView(); GUILayout.EndArea();
            }
            GUI.matrix = previous;
            GUI.Label(new Rect(Screen.width/2-5,Screen.height/2-10,20,25), "+");
            GUI.Box(new Rect(Screen.width/2-300,Screen.height-95,600,90), AimMessage ?? "");
        }
    }
}
