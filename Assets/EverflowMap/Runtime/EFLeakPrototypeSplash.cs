using UnityEngine;

namespace EverflowMap
{
    // Presentation only: follow the existing water state without writing puzzle state.
    [DefaultExecutionOrder(710)]
    public sealed class EFLeakPrototypeSplash : MonoBehaviour
    {
        public EFV2WaterIntegration water;
        public ParticleSystem spray;
        void LateUpdate()
        {
            if(water==null||spray==null)return;
            EFV2WaterSegment source=null;
            foreach(var s in water.segments)if(s.label=="W_Leak_S3"){source=s;break;}
            if(source==null)return;
            if(!source.wet||source.frozen)
            {if(!spray.isStopped)spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);return;}
            bool paused=water.world!=null&&water.world.Paused;
            if(paused){if(spray.isPlaying)spray.Pause();}
            else if(!spray.isPlaying)spray.Play();
        }
    }
}
