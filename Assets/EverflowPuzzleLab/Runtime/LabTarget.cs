using UnityEngine;

namespace EverflowPuzzleLab
{
    // Completed world changes live here, independently of the three orb slots.
    [DisallowMultipleComponent]
    public sealed class LabTarget : MonoBehaviour
    {
        public LabArea area;
        public PuzzleLab owner;
        public string label;
        public bool acceptsBloom, acceptsFreeze, water, acceptsFlow;
        public Transform growth;
        public bool repositionGrowth = true;
        public Vector3 smallScale = Vector3.one, grownScale = Vector3.one;
        public Vector3 smallPosition, grownPosition;
        public GameObject[] grownObjects = new GameObject[0];
        public Collider iceSupport;
        public string[] directions = new string[0];
        public Transform[] arrows = new Transform[0];
        public Renderer[] indicators = new Renderer[0];
        public bool TintIndicators { get; set; } = true;
        [SerializeField] LabWorldState state = new LabWorldState();
        public bool Grown => state.grown;
        public bool Frozen => state.frozen;
        public int Direction => state.direction;
        public string State => label + (acceptsBloom ? (Grown ? " | GROWN" : " | SMALL") : "") +
            (acceptsFreeze ? (Frozen ? (water ? " | ICE / BLOCKED" : " | FIXED") : (water ? " | LIQUID" : " | RELEASED")) : "") +
            (acceptsFlow ? " | " + (Direction < 0 ? "UNSET" : directions[Direction]) : "");
        public bool Accepts(LabAbility ability) => ability == LabAbility.Bloom ? acceptsBloom : ability == LabAbility.Freeze ? acceptsFreeze : acceptsFlow;
        public void Apply(LabAbility ability, int direction = 0)
        {
            if (!Accepts(ability)) return;
            if (ability == LabAbility.Flow && (direction < 0 || direction >= directions.Length)) return;
            state.Apply(ability,direction);
            Refresh();
        }
        public void Undo(LabAbility ability)
        {
            state.Undo(ability);
            // FLOW is changed by choosing another valid direction, never by recall.
            Refresh();
        }
        public void ResetState() { state.Reset(); Refresh(); }
        void Start() { Refresh(); }
        public void Refresh()
        {
            if (growth != null)
            {
                growth.localScale = Grown ? grownScale : smallScale;
                if (repositionGrowth) growth.localPosition = Grown ? grownPosition : smallPosition;
            }
            foreach (var item in grownObjects) if (item != null) item.SetActive(Grown);
            if (iceSupport != null) iceSupport.enabled = Frozen;
            if (TintIndicators && indicators.Length > 0)
            {
                var block = new MaterialPropertyBlock();
                foreach (var item in indicators) if (item != null)
                {
                    Color color = Frozen ? new Color(.5f,.85f,1) : Grown ? new Color(.3f,.8f,.3f) :
                        item.sharedMaterial != null ? item.sharedMaterial.color : Color.white;
                    block.SetColor("_BaseColor",color); block.SetColor("_Color",color); item.SetPropertyBlock(block);
                }
            }
            for (int i = 0; i < arrows.Length; i++) if (arrows[i] != null)
                arrows[i].localScale = Vector3.one * (i == Direction ? 1.35f : .8f);
        }
        void OnDisable()
        {
            var lab = owner != null ? owner : area != null ? area.lab : null;
            if (lab != null) lab.ReleaseTarget(this);
        }
    }
}
