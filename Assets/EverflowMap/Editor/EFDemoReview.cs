using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace EverflowMap.Editor
{
    public static class EFDemoReview
    {
        static RecorderController recorder;static RecorderControllerSettings settings;static MovieRecorderSettings movie;
        static EFDemoFeedback feedback;static double began;static readonly HashSet<string> captured=new HashSet<string>();
        public static void Start(string filename="continuous-demo")
        {
            if(!Application.isPlaying)throw new Exception("Record in Play.");
            Directory.CreateDirectory("docs/demo-feedback");captured.Clear();
            feedback=UnityEngine.Object.FindFirstObjectByType<EFDemoFeedback>();feedback.Presented+=Capture;
            settings=ScriptableObject.CreateInstance<RecorderControllerSettings>();settings.FrameRate=30;settings.CapFrameRate=true;settings.SetRecordModeToManual();
            movie=ScriptableObject.CreateInstance<MovieRecorderSettings>();movie.name="EVERFLOW continuous feedback route";movie.Enabled=true;movie.CaptureAudio=true;
            movie.OutputFile=Path.GetFullPath("docs/demo-feedback/"+filename);movie.ImageInputSettings=new GameViewInputSettings{OutputWidth=1280,OutputHeight=720};settings.AddRecorderSettings(movie);
            recorder=new RecorderController(settings);recorder.PrepareRecording();recorder.StartRecording();began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;
        }
        static void Capture(string text)
        {
            string key=text.Contains("성장")?"bloom":text.Contains("물레방아")?"restored":text.Contains("정원 복원")?"garden":text.Contains("개방")?"gate":text.Contains("공급 연결")?"supply":text.Contains("흐름 전환")?"flow":text.Contains("해동")?"thaw":"freeze";
            if(captured.Add(key))ScreenCapture.CaptureScreenshot("docs/demo-feedback/"+key+"-play.png");
        }
        static void Tick()
        {
            if(!Application.isPlaying||EditorApplication.timeSinceStartup-began>280||EditorApplication.timeSinceStartup-began>10&&SessionState.GetString(EFV2PuzzlePlayChecks.ModeKey,"").Length==0)Stop();
        }
        public static void Stop()
        {
            EditorApplication.update-=Tick;if(feedback!=null)feedback.Presented-=Capture;
            if(recorder!=null){recorder.StopRecording();recorder=null;}
            if(settings!=null)UnityEngine.Object.DestroyImmediate(settings);if(movie!=null)UnityEngine.Object.DestroyImmediate(movie);
        }
    }
}
