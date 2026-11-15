using UnityEngine;

namespace EverflowMap
{
    [DefaultExecutionOrder(710)]
    public sealed class EFWaterImpact : MonoBehaviour
    {
        public EFV2WaterIntegration water;
        public string sourceLabel;
        public ParticleSystem spray;
        void LateUpdate()
        {
            if(water==null||spray==null)return;
            EFV2WaterSegment source=null;
            foreach(var s in water.segments)if(s.label==sourceLabel){source=s;break;}
            if(source==null)return;
            if(!source.wet||source.frozen||source.arrival<.95f)
            {if(!spray.isStopped)spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);return;}
            if(water.world!=null&&water.world.SimulationPaused){if(spray.isPlaying)spray.Pause();}
            else if(!spray.isPlaying)spray.Play();
        }
    }
}
