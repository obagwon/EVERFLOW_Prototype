using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EverflowMap
{
    // Only invoked by the cloned puzzle world's builder, never the source map or PuzzleLab.
    public static class EFV2VisualCleanup
    {
        public static readonly List<string> LastChanges=new List<string>();
        public static void Apply(EFV2PuzzleWorld world)
        {
            LastChanges.Clear();
            var meshes=world.mapRoot.GetComponentsInChildren<MeshFilter>(true).ToDictionary(m=>m.name);
            foreach(string name in new[]{"VOL_A_Wind","VOL_C_Wind","VOL_D5_Wind","PH_A_Tut_MovingPad","PH_A_Tut_WitheredPad"})
                if(meshes.TryGetValue(name,out var mesh))
                {mesh.GetComponent<Renderer>().enabled=false;LastChanges.Add("Hidden preview/decorative renderer only: "+name);}
            // This non-solid parasol occupied the entrance camera's sightline to the moving leaf.
            if(meshes.TryGetValue("PROP_C_ParasolLeaf01",out var parasol))
            {parasol.transform.position+=new Vector3(-12,0,-.5f);LastChanges.Add("C parasol moved 12m west onto the same terrace; no collision or puzzle target");}
            if(meshes.TryGetValue("UG_Torch01",out var torch))
            {torch.transform.position+=new Vector3(-2,0,-3.8f);LastChanges.Add("D1 torch moved onto the south balcony edge, away from the character/camera corridor; no collider");}
            Physics.SyncTransforms();
            foreach(var r in world.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("Safe landing /")))
            {
                var box=r.GetComponent<BoxCollider>();if(box==null||!r.gameObject.activeInHierarchy)continue;
                var b=box.bounds;var supports=new HashSet<string>();bool covered=true;
                foreach(float x in new[]{-.9f,0,.9f})foreach(float z in new[]{-.9f,0,.9f})
                {
                    Vector3 p=new Vector3(b.center.x+b.extents.x*x,b.max.y+.3f,b.center.z+b.extents.z*z);
                    var hits=Physics.RaycastAll(p,Vector3.down,.8f,~(1<<2),QueryTriggerInteraction.Ignore);
                    var match=hits.FirstOrDefault(h=>h.collider!=box&&!h.collider.name.StartsWith("Safe landing /")&&h.collider.GetComponentInParent<EverflowPuzzleLab.LabMover>()==null&&Mathf.Abs(h.point.y-b.max.y)<=.18f);
                    if(match.collider==null)covered=false;else supports.Add(match.collider.name);
                }
                if(covered)
                {
                    r.enabled=false;box.enabled=false;
                    LastChanges.Add("Removed duplicate safe landing skin/collider: "+r.name+"; nine supporting samples on "+string.Join(",",supports));
                }
            }
            Physics.SyncTransforms();
            Debug.Log("[EVERFLOW visual cleanup]\n"+string.Join("\n",LastChanges));
        }
    }
}
