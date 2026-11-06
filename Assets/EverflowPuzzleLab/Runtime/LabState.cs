using System;

namespace EverflowPuzzleLab
{
    public enum LabAbility { Bloom, Freeze, Flow }

    [Serializable]
    public sealed class LabWorldState
    {
        public bool grown, frozen;
        public int direction = -1;
        public void Apply(LabAbility ability, int choice)
        {
            if (ability == LabAbility.Bloom) grown = true;
            else if (ability == LabAbility.Freeze) frozen = true;
            else direction = choice;
        }
        public void Undo(LabAbility ability)
        {
            if (ability == LabAbility.Bloom) grown = false;
            if (ability == LabAbility.Freeze) frozen = false;
        }
        public void Reset() { grown = frozen = false; direction = -1; }
    }
    public static class LabOrbSlots
    {
        public static bool TryPlace<T>(T[] slots, LabAbility ability, T target) where T : class
        {
            int i = (int)ability;
            if (target == null || slots[i] != null && !ReferenceEquals(slots[i],target)) return false;
            slots[i] = target; return true;
        }
        public static void Release<T>(T[] slots, T target) where T : class
        {
            for(int i=0;i<slots.Length;i++) if(ReferenceEquals(slots[i],target)) slots[i]=null;
        }
    }
    public static class LabCircuitRules
    {
        public static bool Supply(bool aligned, bool upperFrozen, bool bypass) => aligned && !upperFrozen || bypass;
        public static bool Arriving(bool supply, int branch, bool gateOpen, bool mainFrozen) => supply && branch == 2 && gateOpen && !mainFrozen;
    }
    [Serializable]
    public sealed class LabArrivalState
    {
        public float stableFor;
        public bool success;
        public void Tick(float dt, bool arriving, float requiredSeconds)
        {
            float required = Math.Max(.1f,requiredSeconds);
            stableFor = arriving ? Math.Min(required,stableFor+Math.Max(0,dt)) : 0;
            if(arriving && stableFor>=required) success=true;
        }
        public void Reset() { stableFor=0; success=false; }
    }
    [Serializable]
    public sealed class LabGateState
    {
        public bool latched;
        public float open;
        public void Tick(float dt, bool driven, bool rootGrown, float speed, float lift)
        {
            float goal = latched || driven || rootGrown ? 1 : 0;
            float step = Math.Max(0,dt) * speed / Math.Max(.01f,lift);
            open = goal > open ? Math.Min(goal,open+step) : Math.Max(goal,open-step);
            if(open>=1) latched=true;
        }
        public void Reset() { latched=false; open=0; }
    }
}
