using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EverflowPuzzleLab.Editor
{
    public static class PuzzleLabValidation
    {
        static void Need(bool valid,string message) { if(!valid) throw new InvalidOperationException("PuzzleLab references: "+message); }
        [MenuItem("Tools/EVERFLOW/v0.2 Puzzle Lab/Validate Open Lab References")]
        public static void ValidateOpen()
        {
            var lab=UnityEngine.Object.FindFirstObjectByType<PuzzleLab>(); Need(lab!=null,"Open the saved lab first.");
            Validate(lab,lab.areas.Length==1); Debug.Log("PUZZLELAB REFERENCE VALIDATION PASS (not Play verification)");
        }
        public static void Validate(PuzzleLab lab,bool onlyT01)
        {
            Need(lab.areas!=null&&lab.areas.Length==(onlyT01?1:5),"Expected T01 or full five reset areas");
            Need(lab.slots.Length==3&&lab.orbs.Length==3&&lab.orbs.All(x=>x!=null),"Exactly three visible orb slots");
            Need(lab.player!=null&&lab.player.puzzleLabLocomotion&&lab.player.world==null,"Standalone reused locomotion");
            Need(lab.player.follow!=null&&lab.player.follow.player==lab.player&&lab.player.view==lab.player.follow.GetComponent<Camera>(),"Camera references");
            var scene=lab.gameObject.scene;
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            Need(all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0,"Missing scripts");
            Need(all.Count(t=>t.GetComponent<EverflowMap.EFMapPlayer>()!=null)==1,"One player");
            Need(all.All(t=>t.GetComponent<EverflowMap.EFMapWorld>()==null),"Legacy stage manager must not exist");
            foreach(var area in lab.areas)
            {
                Need(area!=null&&area.lab==lab&&area.spawn!=null&&area.sign!=null,"Area, safe spawn and sign");
                Need(area.bounds.Contains(area.spawn.position),area.title+" spawn inside area bounds");
                foreach(var t in area.GetComponentsInChildren<LabTarget>(true))
                {
                    Need(t.area==area&&t.owner==lab&&!string.IsNullOrEmpty(t.label),"Target owner/name "+t.name);
                    Need(t.acceptsBloom||t.acceptsFreeze||t.acceptsFlow,"Ability type "+t.name);
                    if(t.acceptsBloom) Need(t.growth!=null||t.grownObjects.Length>0,"Growth result "+t.name);
                    if(t.acceptsFlow) Need(t.directions.Length>=2&&t.directions.Length<=4&&t.arrows.Length==t.directions.Length&&t.arrows.All(x=>x!=null),"2-4 FLOW arrows "+t.name);
                    if(t.water) Need(t.acceptsFreeze&&t.iceSupport!=null&&t.GetComponents<Collider>().Any(c=>c.isTrigger),"Separate water aim and ice support "+t.name);
                    Need(t.GetComponent<Collider>()!=null,"Physical target collider "+t.name);
                }
                foreach(var m in area.GetComponentsInChildren<LabMover>())
                    Need(m.target!=null&&m.motion!=null&&m.flow!=null&&m.destinations.Length==m.flow.directions.Length&&m.speed>0,"Mover references "+m.name);
                foreach(var g in area.GetComponentsInChildren<LabGate>())
                    Need(g.root!=null&&g.flow!=null&&g.door!=null&&g.wheel!=null&&g.latch!=null,"Gate references "+g.name);
            }
            if(!onlyT01)
            {
                var c=lab.areas[4].canal;
                Need(c!=null&&c.aqueduct!=null&&c.branch.directions.Length==3&&c.gate!=null&&c.wheel!=null,"Integrated circuit devices");
                Need(c.upperIce!=c.aqueduct.target&&c.mainIce!=c.upperIce,"Structure and two water regions are distinct targets");
                Need(c.connectedWater.Length>0&&c.bypassWater.Length>0&&c.dockWater.Length>0&&c.turbineWater.Length>0&&c.mainWater.Length>0&&c.gateApproachWater.Length>0&&c.exitWater.Length>0,"Every circuit branch has its own visual water");
                Need(c.successSeconds>0&&c.connectionTolerance>0,"Final timer and connection tolerance");
            }
        }
    }
}
