using System;
using EverflowPuzzleLab;

namespace EverflowMap
{
    public enum EFV2OrbPhase { Ready, Flying, Placed, Returning }

    // Orb ownership has no authority over persistent LabTarget world state.
    [Serializable]
    public sealed class EFV2OrbSlot<T> where T : class
    {
        public T target;
        public EFV2OrbPhase phase;
        public int direction;
        public bool release;
        public int revision;

        public bool Launch(T next, int choice, bool undo)
        {
            if (next == null) return false;
            if (phase == EFV2OrbPhase.Flying && ReferenceEquals(target, next) && direction == choice && release == undo)
                return false; // repeated click must not restart the flight or toggle twice
            target = next; direction = choice; release = undo;
            phase = EFV2OrbPhase.Flying; revision++;
            return true;
        }
        public bool Arrive(int flightRevision)
        {
            if (phase != EFV2OrbPhase.Flying || target == null || revision != flightRevision) return false;
            phase = EFV2OrbPhase.Placed;
            return true;
        }
        public void Recall()
        {
            target = null; phase = EFV2OrbPhase.Returning; revision++;
        }
        public bool RecallIfFar(float distance,float threshold)
        {
            if(phase!=EFV2OrbPhase.Placed || distance<=Math.Max(.1f,threshold)) return false;
            Recall();return true;
        }
        public void Returned()
        {
            if (phase == EFV2OrbPhase.Returning) phase = EFV2OrbPhase.Ready;
        }
    }
    public static class EFV2CircuitRules
    {
        public static bool Supply(bool aligned,bool structureFixed,bool upperIce,bool bypass)
            => LabCircuitRules.Supply(aligned && structureFixed,upperIce,bypass);
    }
}
