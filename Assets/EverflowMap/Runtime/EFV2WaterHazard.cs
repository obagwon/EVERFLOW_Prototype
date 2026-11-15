using UnityEngine;
namespace EverflowMap
{
    public sealed class EFV2WaterHazard : MonoBehaviour
    {
        public EFV2PuzzleWorld world;
        float next;
        void OnTriggerStay(Collider other)
        {
            if(Time.time<next || !world.InputActive || world.mainIce.Frozen || other.GetComponentInParent<EFMapPlayer>()!=world.player) return;
            if(world.player.transform.position.y-1 > -11.73f) return;
            next=Time.time+1;
            world.Respawn();world.Record("깊은 분배 수조 · 얼음길, 부두 잎 또는 덩굴을 이용하세요.");
        }
    }
}
