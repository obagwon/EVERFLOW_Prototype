#if DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace EverflowMap
{
    // Development players only; this test driver is absent from release builds.
    // It checks the real title route and reloads, not a full playthrough.
    public sealed class EFMvpBuildSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if(!Environment.GetCommandLineArgs().Contains("-everflow-mvp-smoke")) return;
            Application.runInBackground=true;
            var go=new GameObject("Explicit player smoke"); DontDestroyOnLoad(go); go.AddComponent<EFMvpBuildSmoke>();
        }
        IEnumerator Start()
        {
            for(int i=0;i<8;i++) yield return null;
            var title=FindFirstObjectByType<EverflowMVP.EFTitleScreen>();
            if(title==null || title.startButton==null) { Fail("Title/start button missing"); yield break; }
            title.startButton.onClick.Invoke();
            float deadline=Time.realtimeSinceStartup+60;
            while(Time.realtimeSinceStartup<deadline)
            {
                if(FindFirstObjectByType<EFMapWorld>()!=null) break;
                yield return null;
            }
            for(int repeat=0;repeat<3;repeat++)
            {
                for(int i=0;i<10;i++) yield return null;
                var world=FindFirstObjectByType<EFMapWorld>();
                if(world==null || world.gameObject.scene.path!="Assets/Scenes/Everflow_Map_MVP.unity" || world.mvpFinale==null ||
                   world.StageIndex!=0 || world.Completed.Any(v=>v) || world.WheelRunning || world.Ended)
                { Fail("Fresh MVP or reload state invalid, repetition "+repeat); yield break; }
                Debug.Log("PLAYER SMOKE PASS: title/MVP fresh state, repetition "+repeat);
                if(repeat<2) world.RestartWorld();
            }
            Debug.Log("PLAYER SMOKE OVERALL PASS. Not a full physical playthrough or rendered UI inspection.");
            Application.Quit(0);
        }
        static void Fail(string message) { Debug.LogError("PLAYER SMOKE FAIL: "+message); Application.Quit(1); }
    }
}
#endif
