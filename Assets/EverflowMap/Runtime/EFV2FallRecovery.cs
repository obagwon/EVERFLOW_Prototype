using System;
using UnityEngine;

namespace EverflowMap
{
    [Serializable]
    public sealed class EFV2FallRecovery
    {
        // World X/Z envelope, measured from T_Terrain plus a 2m margin.
        // Its four exterior half-spaces are explicit void regions, not colliders.
        // Internal gaps are caught by the world's existing below-map safety plane.
        [Tooltip("World X/Z bounds (Rect Y means world Z). Update when expanding the map. Exterior sides are fall-recovery regions.")]
        public Rect playableXZBounds=Rect.MinMaxRect(-104,-94,14,14);

        public bool ShouldRecover(EFMapPlayer player,float bottomY,out string reason)
        {
            reason=null;
            var p=player.transform.position;
            if(p.y<bottomY){reason="Below-map safety plane";return true;}
            // Walking on a surface or the rising part of a jump is never an out-of-map fall.
            if(player.Grounded||player.VerticalSpeed>=-.1f)return false;
            if(p.x<playableXZBounds.xMin)reason="West exterior";
            else if(p.x>playableXZBounds.xMax)reason="East exterior";
            else if(p.z<playableXZBounds.yMin)reason="North exterior";
            else if(p.z>playableXZBounds.yMax)reason="South exterior";
            return reason!=null;
        }
    }
}
