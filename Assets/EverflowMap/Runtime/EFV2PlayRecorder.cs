#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EverflowMap.Editor
{
    // Observation only: never casts, moves, teleports or changes puzzle state.
    public sealed class EFV2PlayRecorder : MonoBehaviour
    {
        EFV2PuzzleWorld world;
        string file;
        float next;
        bool restored,garden;
        void Start()
        {
            world=FindFirstObjectByType<EFV2PuzzleWorld>();if(world==null){enabled=false;return;}
            const string reports="docs/v2-puzzle-validation";
            Directory.CreateDirectory(reports);
            file=reports+"/observed-play-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+".jsonl";
            Write("SESSION Unity="+Application.unityVersion+" scene="+world.gameObject.scene.path+"; observation only; human identity and absence of external state changes cannot be inferred.");
            world.Recorded+=Write;
        }
        void Write(string message)
        {
            if(file==null || world==null || world.player==null)return;
            File.AppendAllText(file,JsonUtility.ToJson(new Sample{utc=DateTime.UtcNow.ToString("O"),elapsed=world.Elapsed,message=message,position=world.player.transform.position,selected=world.Selected.ToString(),supply=world.Supply,arriving=world.Arriving,restored=world.Restored,garden=world.GardenReached})+"\n");
        }
        void Update()
        {
            if(world==null)return;
            if(Time.unscaledTime>=next){next=Time.unscaledTime+.5f;Write("POSITION");}
            if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame)Write("OBSERVED LEFT CLICK / hover="+(world.Hovered!=null?world.Hovered.label:"none"));
            if(world.Restored&&!restored){restored=true;Write("RESTORED AT elapsed seconds; includes exploration, retries and puzzle thinking time");}
            if(world.GardenReached&&!garden){garden=true;Write("GARDEN REACHED AT elapsed seconds");}
            if(!world.Restored){restored=garden=false;}
        }
        void OnDisable(){if(world!=null)world.Recorded-=Write;Write("SESSION STOP");}
        [Serializable] sealed class Sample
        {
            public string utc,message,selected;
            public float elapsed;
            public Vector3 position;
            public bool supply,arriving,restored,garden;
        }
    }
}
#endif
