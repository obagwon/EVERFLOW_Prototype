using System;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using EverflowPuzzleLab;

namespace EverflowMap.Editor
{
    public static class EFWaterNetworkReview
    {
        static RecorderController recorder;
        static RecorderControllerSettings settings;
        static MovieRecorderSettings movie;
        static double began;
        static int stage;
        public static void State(string state)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Forced-state visual review only in Play.");
            var w=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleWorld>();w.AutomatedCheck=true;w.SetPaused(false);w.Restart();
            if(state=="bypass"||state=="dock")w.bypass.Apply(LabAbility.Bloom);
            if(state=="restored")
            {
                w.channel.motion.position=w.channel.destinations[0];w.channelPose.model.rotation=w.channelPose.aligned;
                w.channelStructure.Apply(LabAbility.Freeze);w.exitGate.root.Apply(LabAbility.Bloom);w.branch.Apply(LabAbility.Flow,2);
            }
            if(state=="dock")w.branch.Apply(LabAbility.Flow,0);
        }
        public static void View(string name)
        {
            var w=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleWorld>();w.AutomatedCheck=true;w.SetPaused(false);
            foreach(var g in UnityEngine.Object.FindObjectsByType<EFV2Guidance>(FindObjectsSortMode.None))g.enabled=false;
            var c=Camera.main;c.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled=false;c.fieldOfView=48;
            Vector3 eye,target;
            switch(name)
            {
                case "highland":eye=new Vector3(-54,18,-55);target=new Vector3(-45,12,-61);break;
                case "spring":eye=new Vector3(-58,-1,-61);target=new Vector3(-48,-4,-68);break;
                case "d5":eye=new Vector3(-62,-5,-56);target=new Vector3(-54,-8,-64);break;
                case "bypass":eye=new Vector3(-67,-5,-53);target=new Vector3(-75,-9,-63);break;
                case "basin":eye=new Vector3(-62,-8,-47.5f);target=new Vector3(-56,-11.8f,-54);break;
                case "gorge":eye=new Vector3(-94,-7,-30);target=new Vector3(-86,-14,-30);break;
                case "garden":eye=new Vector3(-94,-14,2);target=new Vector3(-84,-17.8f,-5);break;
                case "island":eye=new Vector3(-93,-23,12);target=new Vector3(-86,-23,2.65f);break;
                case "pond":eye=new Vector3(-46,8,-14);target=new Vector3(-45,0,-21);break;
                default:eye=new Vector3(-39,4,-28);target=new Vector3(-35,1.9f,-31);break;
            }
            c.transform.position=eye;c.transform.LookAt(target);
        }
        public static void Record()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Record in Play.");
            if(recorder!=null)Stop();
            settings=ScriptableObject.CreateInstance<RecorderControllerSettings>();settings.FrameRate=30;settings.CapFrameRate=true;settings.SetRecordModeToManual();
            movie=ScriptableObject.CreateInstance<MovieRecorderSettings>();movie.name="Continuous map waterways";movie.Enabled=true;movie.CaptureAudio=false;
            movie.OutputFile=System.IO.Path.GetFullPath(EFWaterNetworkTools.Reports+"/waterways-review");
            movie.ImageInputSettings=new GameViewInputSettings {OutputWidth=1280,OutputHeight=720};settings.AddRecorderSettings(movie);
            View("surface");recorder=new RecorderController(settings);recorder.PrepareRecording();recorder.StartRecording();
            began=EditorApplication.timeSinceStartup;stage=0;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(!Application.isPlaying){Stop();return;}
            double elapsed=EditorApplication.timeSinceStartup-began;int next=Math.Min(4,(int)(elapsed/3));
            if(next!=stage){stage=next;View(new[]{"surface","highland","d5","garden","island"}[stage]);}
            if(elapsed>=15)Stop();
        }
        public static void Stop()
        {
            EditorApplication.update-=Tick;
            if(recorder!=null){recorder.StopRecording();recorder=null;}
            if(settings!=null)UnityEngine.Object.DestroyImmediate(settings);
            if(movie!=null)UnityEngine.Object.DestroyImmediate(movie);
        }
    }
}
