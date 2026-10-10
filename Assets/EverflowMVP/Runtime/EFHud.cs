using UnityEngine;

namespace EverflowMVP
{
    public class EFHud : MonoBehaviour
    {
        public EFWorld world;
        GUIStyle text;
        void OnGUI()
        {
            if (world == null || world.player == null || world.stages.Length == 0) return;
            if (text == null) text = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            float width = Mathf.Min(960, Screen.width - 20);
            GUI.Box(new Rect(10, 10, width, 142), "");
            EFStage stage = world.stages[world.StageIndex];
            GUI.Label(new Rect(20, 16, width - 20, 22), $"EVERFLOW | {world.StageIndex + 1}/6: {stage.title}", text);
            GUI.Label(new Rect(20, 41, width - 20, 22), "WASD move | RMB drag camera | Space jump/release | E ride | 1 BLOOM  2 FREEZE  3 PULSE | Click cast", text);
            GUI.Label(new Rect(20, 65, width - 20, 22), "R restart | Enter next after clear" + (world.debugStageSelection ? " | F1-F6 DEBUG room select" : ""), text);
            GUI.Label(new Rect(20, 90, width - 20, 24), world.Message, text);
            GUI.Label(new Rect(20, 116, width - 20, 24), $"Selected: {world.player.selected.ToString().ToUpperInvariant()} | {world.player.Mode}", text);
            GUI.Label(new Rect(20, Screen.height - 62, Screen.width - 40, 48), world.player.Hovered != null ? world.player.Hovered.Status : stage.hint, text);
            if (world.Completed[world.StageIndex])
                GUI.Box(new Rect(Screen.width * .5f - 170, Screen.height * .3f, 340, 50), world.StageIndex == 5 ? "WORLD RESTORED" : "PUZZLE CLEAR - press Enter");
        }
    }
}
