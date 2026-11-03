using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ability = EverflowGraybox.Ability;
using TestInput = EverflowGraybox.EFInput;

namespace EverflowMap.Editor
{
    // Opt-in smoke checks for the saved copy. Never runs automatically on import.
    public static class EFCanalExperimentTests
    {
        internal const string Pending = "EVERFLOW.P3.Checks.Pending";
        internal const string ReportKey = "EVERFLOW.P3.Checks.Report";
        internal const string SnapshotKey = "EVERFLOW.P3.Checks.Snapshot";
        internal const string OutputKey = "EVERFLOW.P3.Checks.Output";
        const string BackgroundSaved="EVERFLOW.P3.BackgroundSaved";
        const string BackgroundValue="EVERFLOW.P3.BackgroundValue";

        [MenuItem("Tools/EVERFLOW/MVP Experiment/Run P3 Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene edits before running checks.");
            SessionState.SetBool(BackgroundValue,Application.runInBackground);
            SessionState.SetBool(BackgroundSaved,true);
            Application.runInBackground=true;
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            string output = "docs/everflow_mvp/evidence/p3-checks-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".txt";
            var report = new StringBuilder("EVERFLOW P3 experiment checks; UTC " + DateTime.UtcNow.ToString("O") + "\n");
            bool pausedAtRequest=EditorApplication.isPaused;
            EditorApplication.isPaused=false;
            report.AppendLine("[EDITOR] Cleared initial Editor pause for this explicitly requested check; previously paused="+pausedAtRequest+".");
            report.AppendLine("Edit-mode references and API state fixtures are separate from the collision-swept leaf segment. Teleports position fixtures; this is not a complete physical playthrough, user test, or duration measurement.");
            string snapshot = ProtectedSnapshot();
            try
            {
                if (!File.Exists(EFCanalExperimentSetup.ScenePath)) throw new FileNotFoundException("Create the experiment copy before running checks.", EFCanalExperimentSetup.ScenePath);
                var scene = EditorSceneManager.OpenScene(EFCanalExperimentSetup.ScenePath, OpenSceneMode.Single);
                var transforms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
                var worlds = transforms.Select(t => t.GetComponent<EFMapWorld>()).Where(w => w != null).ToArray();
                if (worlds.Length != 1) throw new InvalidOperationException("Expected exactly one experiment world.");
                var world = worlds[0];
                Physics.SyncTransforms();
                EFCanalExperimentSetup.Validate(world);
                if (!world.requireGroundedArrivals || !world.stabilizeCompletedStages) throw new InvalidOperationException("Experiment completion safety flags are not enabled.");
                if (transforms.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) != 0) throw new InvalidOperationException("Missing script in experiment.");
                if (transforms.Count(t => t.GetComponent<EFMapPlayer>() != null) != 1) throw new InvalidOperationException("Duplicate/missing player.");
                if (world.player.follow == null || world.player.view == null || world.player.follow.GetComponent<Camera>() != world.player.view) throw new InvalidOperationException("Gameplay camera references differ.");
                report.AppendLine("PASS [EDIT] Saved copy references, one world/player, no missing scripts, camera owner, completion safety flags.");
                if (snapshot != ProtectedSnapshot()) throw new InvalidOperationException("A protected original asset changed during read-only checks.");
                report.AppendLine("PASS [EDIT] Original map/title, logo, shared map prefabs/materials, model import metadata and build/profile files unchanged by these checks.");
                SessionState.SetString(ReportKey, report.ToString());
                SessionState.SetString(SnapshotKey, snapshot);
                SessionState.SetString(OutputKey, output);
                SessionState.SetBool(Pending, true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error)
            {
                RestoreRunSettings();
                report.AppendLine("FAIL [EDIT] " + error);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output, report.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [InitializeOnLoadMethod]
        static void Register()
        {
            if(AssetDatabase.IsAssetImportWorkerProcess()) return;
            EditorApplication.playModeStateChanged += state =>
            {
                if(state==PlayModeStateChange.EnteredEditMode)
                { SessionState.SetBool(Pending,false); RestoreRunSettings(); return; }
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.SetBool(Pending, false);
                // Entering Play may force a synchronous script recompile after
                // the caller set up this run. Clear a sticky Editor pause here
                // as well, exclusively for our pending test, before frame one.
                EditorApplication.isPaused=false;
                Application.runInBackground=true;
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                Debug.Log("P3 CHECK: creating runtime probe; background execution enabled for this check.");
                new GameObject("P3 experiment smoke probe").AddComponent<EFCanalExperimentProbe>();
            };
        }

        internal static void RestoreRunSettings()
        {
            if(!SessionState.GetBool(BackgroundSaved,false)) return;
            Application.runInBackground=SessionState.GetBool(BackgroundValue,false);
            SessionState.SetBool(BackgroundSaved,false);
        }

        internal static string ProtectedSnapshot()
        {
            var paths = new List<string> { EFMapAudit.ScenePath, EFMapAudit.ScenePath + ".meta", "Assets/EverflowMVP/Scenes/Everflow_Title.unity", "Assets/EverflowMVP/Scenes/Everflow_Title.unity.meta", "ProjectSettings/EditorBuildSettings.asset" };
            foreach (string folder in new[] { "Assets/EverflowMap/Prefabs", "Assets/EverflowMap/Materials", "Assets/logo" })
                if (Directory.Exists(folder)) paths.AddRange(Directory.GetFiles(folder, "*", SearchOption.AllDirectories));
            if (Directory.Exists("Assets/map_ver1/fbx")) paths.AddRange(Directory.GetFiles("Assets/map_ver1/fbx", "*.meta", SearchOption.AllDirectories));
            paths.AddRange(AssetDatabase.FindAssets("t:BuildProfile").Select(AssetDatabase.GUIDToAssetPath));
            var result = new StringBuilder();
            using (var hash = SHA256.Create())
                foreach (string path in paths.Select(p => p.Replace('\\', '/')).Distinct().OrderBy(p => p, StringComparer.Ordinal))
                    result.Append(path).Append(' ').Append(File.Exists(path) ? BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "") : "MISSING").Append('\n');
            return result.ToString();
        }
    }

    // Explicit opt-in local bridge: Enable/Disable menus control a SessionState
    // nonce; the fixed Temp request accepts only p3-checks and p3-repair-access.
    // accepted is an acknowledgement, never a test PASS. Terminal runState and
    // overall are published separately from the most recent request's ack.
    internal static class LocalEditorBridge
    {
        const string StateKey="EVERFLOW.MVP.LocalBridge.State";
        const string EnabledKey="EVERFLOW.MVP.LocalBridge.Enabled";
        const string InflightKey="EVERFLOW.MVP.LocalBridge.Inflight";
        const string PriorOutputKey="EVERFLOW.MVP.LocalBridge.PriorOutput";
        const string SourceStampKey="EVERFLOW.MVP.LocalBridge.SourceStamp";
        const string ScopeKey="EVERFLOW.MVP.LocalBridge.Scope";
        const string DirectoryPath="Temp/mvp-experiment";
        const string RequestPath=DirectoryPath+"/editor-bridge-request.json";
        const string StatusPath=DirectoryPath+"/editor-bridge-status.json";
        const string Limitations="SafeMode has no verified stable public query here. Public compilation-failure/compiling/updating guards apply; SafeMode may prevent this editor code from loading. Accepted is not PASS; stalled never stops Play mode or closes the Editor.";
        static BridgeStatus status;
        static double nextPoll, nextHeartbeat;
        static bool handling;

        [Serializable]
        sealed class BridgeStatus
        {
            public int version=1;
            public int implementationRevision=2;
            public bool enabled;
            public string sessionId="";
            public int consumedSequence, ackSequence;
            public string ackCommand="", ackState="disabled", ackMessage="", updatedUtc="";
            public int runSequence;
            public string runCommand="", runState="none", runStartedUtc="", outputPath="", overall="";
            public string limitations=Limitations;
            public string commandScope="p3";
            public bool editorPlaying, editorPaused, editorCompiling, editorUpdating;
        }
        sealed class Request
        {
            public string sessionId, command, issuedUtc;
            public int sequence;
        }

        [InitializeOnLoadMethod]
        static void Register()
        {
            if(AssetDatabase.IsAssetImportWorkerProcess()) return;
            string saved=SessionState.GetString(StateKey,"");
            try { status=string.IsNullOrEmpty(saved)?new BridgeStatus():JsonUtility.FromJson<BridgeStatus>(saved); }
            catch { status=new BridgeStatus(); }
            if(status==null) status=new BridgeStatus();
            status.enabled=SessionState.GetBool(EnabledKey,false);
            status.implementationRevision=2;
            status.commandScope=SessionState.GetString(ScopeKey,"p3");
            status.limitations=Limitations;
            // Keep the prior observation across domain reloads. Replacing it
            // with the current disk stamp could hide an unimported source edit.
            // This detects source changes; it is not proof of a compiled DLL.
            if(string.IsNullOrEmpty(SessionState.GetString(SourceStampKey,"")))
                SessionState.SetString(SourceStampKey,SourceStamp());
            if(!status.enabled && !SessionState.GetBool(InflightKey,false)) status.ackState="disabled";
            EditorApplication.update-=Tick;
            EditorApplication.update+=Tick;
            Publish();
        }

        [MenuItem("Tools/EVERFLOW/MVP Experiment/Enable Local Check Bridge")]
        public static void Enable()
        {
            if(status==null) Register();
            if(SessionState.GetBool(InflightKey,false))
            { status.ackState="blocked"; status.ackMessage="A previous command is still in flight. Enable cannot replace its session."; Publish(); return; }
            if(status.enabled) { Publish(); return; }
            status=new BridgeStatus { enabled=true, sessionId=Guid.NewGuid().ToString("N"), ackState="enabled", ackMessage="Explicitly enabled for this Editor session; next sequence must exceed consumedSequence." };
            SessionState.SetString(ScopeKey,"p3");
            SessionState.SetBool(EnabledKey,true);
            SessionState.SetBool(InflightKey,false);
            Publish();
        }

        [MenuItem("Tools/EVERFLOW/MVP Experiment/Disable Local Check Bridge")]
        public static void Disable()
        {
            if(status==null) Register();
            status.enabled=false;
            status.ackState="disabled";
            status.ackMessage="New requests disabled. An already accepted command is only observed; it is not cancelled.";
            SessionState.SetBool(EnabledKey,false);
            Publish();
        }

        [MenuItem("Tools/EVERFLOW/MVP Integration/Enable Integration Bridge")]
        public static void EnableIntegration()
        {
            if(status==null) Register();
            if(SessionState.GetBool(InflightKey,false)) throw new InvalidOperationException("Wait for current checks to finish.");
            EFMvpSetup.Guard();
            status=new BridgeStatus {enabled=true,commandScope="mvp",sessionId=Guid.NewGuid().ToString("N"),ackState="enabled",ackMessage="Explicit MVP scope: fixed create/check/publish/title/build commands; no arbitrary execution."};
            SessionState.SetString(ScopeKey,"mvp");
            SessionState.SetBool(EnabledKey,true); SessionState.SetBool(InflightKey,false); Publish();
        }

        static void Tick()
        {
            if(handling || EditorApplication.timeSinceStartup<nextPoll) return;
            nextPoll=EditorApplication.timeSinceStartup+.5;
            try
            {
                if(status==null) Register();
                ObserveInflight();
                if(status.enabled && File.Exists(RequestPath))
                {
                    // A background Editor may defer its ordinary file refresh.
                    // Keep the request unclaimed across compilation so dispatch
                    // uses the newly compiled code, rather than a stale DLL.
                    if(!SessionState.GetBool(InflightKey,false) && !EditorApplication.isPlayingOrWillChangePlaymode &&
                       !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                       Enumerable.Range(0,SceneManager.sceneCount).All(i=>!SceneManager.GetSceneAt(i).isDirty))
                    {
                        string stamp=SourceStamp();
                        if(stamp!=SessionState.GetString(SourceStampKey,""))
                        {
                            SessionState.SetString(SourceStampKey,stamp);
                            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                            return;
                        }
                    }
                    // A refresh can schedule compilation on the next tick.
                    // Leave the authenticated request untouched until that
                    // import completes instead of consuming it as a failure.
                    if(EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                    ConsumeRequest();
                }
                if((status.enabled || SessionState.GetBool(InflightKey,false)) && EditorApplication.timeSinceStartup>=nextHeartbeat)
                { nextHeartbeat=EditorApplication.timeSinceStartup+5; Publish(); }
            }
            catch(Exception error)
            {
                Debug.LogError("EVERFLOW local bridge polling failed: "+error.Message);
                if(status!=null) { status.ackState="failed"; status.ackMessage="Bridge I/O or observation error: "+error.Message; Publish(); }
            }
        }

        static void ConsumeRequest()
        {
            // Claim the fixed filename before parsing/dispatch. A request left
            // behind by reload cannot be dispatched again, even if malformed.
            Directory.CreateDirectory(DirectoryPath);
            string consumed=DirectoryPath+"/editor-bridge-consumed-"+Guid.NewGuid().ToString("N")+".json";
            File.Move(RequestPath,consumed);
            status.ackSequence=0; status.ackCommand="";
            if(new FileInfo(consumed).Length>4096)
            { Reject("Request exceeds the 4096-byte limit."); return; }
            Request request;
            string reason;
            if(!TryParseRequest(File.ReadAllText(consumed),out request,out reason)) { Reject(reason); return; }
            status.ackSequence=request.sequence; status.ackCommand=request.command;
            if(!string.Equals(request.sessionId,status.sessionId,StringComparison.Ordinal)) { Reject("Session nonce mismatch; enable/status must come from the current Editor session."); return; }
            if(request.sequence<=status.consumedSequence) { Reject("Sequence must strictly increase; consumed requests are never retried."); return; }
            // Authenticated new sequence is consumed before age, state guards
            // and accepted acknowledgement, including blocked requests.
            status.consumedSequence=request.sequence;
            Publish();
            DateTimeOffset issued;
            if(!DateTimeOffset.TryParse(request.issuedUtc,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out issued) ||
                !(request.issuedUtc.EndsWith("Z",StringComparison.Ordinal) || request.issuedUtc.EndsWith("+00:00",StringComparison.Ordinal)))
            { Reject("issuedUtc must be a complete UTC timestamp."); return; }
            double age=(DateTimeOffset.UtcNow-issued).TotalSeconds;
            if(age>300 || age< -30) { Reject("Request is older than five minutes or more than thirty seconds in the future."); return; }
            bool p3=request.command=="p3-checks" || request.command=="p3-repair-access";
            bool mvp=new[]{"mvp-create","mvp-checks","mvp-publish","mvp-title-check","mvp-build"}.Contains(request.command);
            if(!p3 && !(mvp && SessionState.GetString(ScopeKey,"p3")=="mvp")) { Reject("Command is not allowlisted for the explicitly enabled scope."); return; }
            reason=BlockedReason();
            if(reason!=null) { status.ackState="blocked"; status.ackMessage=reason+" Request consumed; submit a new sequence after resolving it."; Publish(); return; }

            status.ackState="accepted";
            status.ackMessage="Accepted for dispatch. This acknowledgement is not a test result.";
            status.runSequence=request.sequence; status.runCommand=request.command;
            status.runState="accepted"; status.runStartedUtc=DateTimeOffset.UtcNow.ToString("O",CultureInfo.InvariantCulture);
            status.outputPath=""; status.overall="";
            SessionState.SetString(PriorOutputKey,SessionState.GetString(OutputKeyForCommand(),""));
            SessionState.SetBool(InflightKey,true);
            Publish();
            handling=true;
            try
            {
                if(IsCheckCommand())
                {
                    if(request.command=="mvp-checks") EFMvpChecks.Run();
                    else if(request.command=="mvp-title-check") EFMvpChecks.Title();
                    else EFCanalExperimentTests.Run();
                    status.runState="running";
                    CaptureOutput();
                }
                else
                {
                    if(request.command=="mvp-create") EFMvpSetup.Create();
                    else if(request.command=="mvp-publish") EFMvpSetup.Publish();
                    else if(request.command=="mvp-build") EFMvpSetup.Build();
                    else EFCanalExperimentSetup.RepairAccess();
                    status.runState="completed"; status.overall="NOT_A_TEST";
                    status.ackMessage="Fixed command returned: "+request.command+". Read its artifact/evidence; command completion is not a gameplay PASS.";
                    SessionState.SetBool(InflightKey,false);
                }
            }
            catch(Exception error)
            {
                CaptureOutput();
                status.ackState="failed"; status.runState="failed";
                status.ackMessage="Dispatch failed: "+error.Message;
                status.overall="ERROR";
                SessionState.SetBool(InflightKey,false);
                Debug.LogException(error);
            }
            finally { handling=false; Publish(); }
        }

        static string BlockedReason()
        {
            if(SessionState.GetBool(InflightKey,false)) return "Another command is in flight (including a stalled run).";
            if(Application.isBatchMode) return "The local bridge requires an interactive Editor; batch test commands can exit their batch process.";
            if(EditorApplication.isPlayingOrWillChangePlaymode) return "Editor is playing or changing Play mode.";
            if(EditorApplication.isCompiling) return "Editor is compiling.";
            if(EditorApplication.isUpdating) return "Editor asset database is updating.";
            if(EditorUtility.scriptCompilationFailed) return "Script compilation has failed; SafeMode/compilation recovery requires the user.";
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) return "An open scene has unsaved edits.";
            return null;
        }

        static void CaptureOutput()
        {
            string output=SessionState.GetString(OutputKeyForCommand(),"");
            if(output!=SessionState.GetString(PriorOutputKey,"") && output.StartsWith("docs/everflow_mvp/evidence/",StringComparison.Ordinal) && !output.Contains("..") && output.EndsWith(".txt",StringComparison.Ordinal))
                status.outputPath=output;
        }
        static bool IsCheckCommand()=>status.runCommand=="p3-checks" || status.runCommand=="mvp-checks" || status.runCommand=="mvp-title-check";
        static string OutputKeyForCommand()=>status.runCommand.StartsWith("mvp-",StringComparison.Ordinal)?EFMvpChecks.OutputKey:EFCanalExperimentTests.OutputKey;
        static void ObserveInflight()
        {
            if(!SessionState.GetBool(InflightKey,false)) return;
            if(IsCheckCommand())
            {
                CaptureOutput();
                if(!string.IsNullOrEmpty(status.outputPath) && File.Exists(status.outputPath) && new FileInfo(status.outputPath).Length<=2*1024*1024)
                {
                    string result=File.ReadAllText(status.outputPath);
                    var terminal=System.Text.RegularExpressions.Regex.Match(result,@"Runtime checks passed: \d+; overall (PASS|FAIL)\.");
                    if(terminal.Success || result.Contains("FAIL [EDIT]"))
                    {
                        status.overall=terminal.Success?terminal.Groups[1].Value:"FAIL";
                        status.runState=status.overall=="PASS"?"completed":"failed";
                        SessionState.SetBool(InflightKey,false);
                        Publish(); return;
                    }
                }
            }
            DateTimeOffset start;
            if(IsCheckCommand() && !EditorApplication.isPlayingOrWillChangePlaymode &&
               !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
               DateTimeOffset.TryParse(status.runStartedUtc,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out start) &&
               (DateTimeOffset.UtcNow-start).TotalSeconds>20)
            {
                status.runState="failed"; status.overall="ABORTED";
                status.ackMessage="Play ended without terminal test evidence. No PASS is recorded; no automatic retry was made.";
                SessionState.SetBool(EFCanalExperimentTests.Pending,false);
                EFMvpChecks.CancelPending();
                SessionState.SetBool(InflightKey,false);
                Publish(); return;
            }
            if(DateTimeOffset.TryParse(status.runStartedUtc,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out start) && (DateTimeOffset.UtcNow-start).TotalMinutes>=10 && status.runState!="stalled")
            {
                status.runState="stalled";
                status.ackMessage="No terminal evidence after ten minutes. The command remains in flight; this bridge will not stop Play mode or close the Editor.";
                Publish();
            }
        }
        static void Reject(string reason)
        { status.ackState="rejected"; status.ackMessage=reason+" Request file was consumed."; Publish(); }

        static string SourceStamp()
        {
            using(var hash=SHA256.Create())
            {
                var source=new StringBuilder();
                foreach(string path in Directory.GetFiles("Assets","*.cs",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal))
                {
                    var file=new FileInfo(path);
                    source.Append(path).Append(':').Append(file.LastWriteTimeUtc.Ticks).Append(':').Append(file.Length).Append('\n');
                }
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(source.ToString())));
            }
        }

        static bool TryParseRequest(string json,out Request request,out string reason)
        {
            request=null; reason="Expected one complete JSON object with exactly version, sessionId, sequence, command and issuedUtc.";
            var opening=System.Text.RegularExpressions.Regex.Match(json,@"\A\s*\{");
            if(!opening.Success) return false;
            int at=opening.Length;
            var values=new Dictionary<string,string>(StringComparer.Ordinal);
            bool closed=false;
            while(!closed && values.Count<6)
            {
                var field=System.Text.RegularExpressions.Regex.Match(json.Substring(at),@"\A\s*""(?<key>[A-Za-z]+)""\s*:\s*(?<value>""[A-Za-z0-9_.:+\-]*""|0|[1-9][0-9]*)\s*(?<end>[,}])");
                if(!field.Success) return false;
                string key=field.Groups["key"].Value;
                if(values.ContainsKey(key)) return false;
                values.Add(key,field.Groups["value"].Value);
                at+=field.Length; closed=field.Groups["end"].Value=="}";
            }
            if(!closed || !string.IsNullOrWhiteSpace(json.Substring(at)) || values.Count!=5 ||
                new[]{"version","sessionId","sequence","command","issuedUtc"}.Any(key=>!values.ContainsKey(key)) || values["version"]!="1") return false;
            int sequence;
            if(!int.TryParse(values["sequence"],NumberStyles.None,CultureInfo.InvariantCulture,out sequence) || sequence<=0) return false;
            foreach(string key in new[]{"sessionId","command","issuedUtc"}) if(!values[key].StartsWith("\"",StringComparison.Ordinal) || !values[key].EndsWith("\"",StringComparison.Ordinal)) return false;
            string nonce=values["sessionId"].Trim('"');
            if(!System.Text.RegularExpressions.Regex.IsMatch(nonce,@"\A[0-9a-f]{32}\z")) return false;
            request=new Request {sessionId=nonce,sequence=sequence,command=values["command"].Trim('"'),issuedUtc=values["issuedUtc"].Trim('"')};
            reason=null; return true;
        }

        static void Publish()
        {
            if(status==null) return;
            status.enabled=SessionState.GetBool(EnabledKey,false);
            status.editorPlaying=EditorApplication.isPlaying;
            status.editorPaused=EditorApplication.isPaused;
            status.editorCompiling=EditorApplication.isCompiling;
            status.editorUpdating=EditorApplication.isUpdating;
            status.updatedUtc=DateTimeOffset.UtcNow.ToString("O",CultureInfo.InvariantCulture);
            status.commandScope=SessionState.GetString(ScopeKey,"p3");
            string json=JsonUtility.ToJson(status,true);
            SessionState.SetString(StateKey,json);
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                string temporary=StatusPath+".writing-"+Guid.NewGuid().ToString("N");
                File.WriteAllText(temporary,json);
                if(File.Exists(StatusPath)) File.Replace(temporary,StatusPath,null);
                else File.Move(temporary,StatusPath);
            }
            catch(Exception error) { Debug.LogError("EVERFLOW bridge status write failed: "+error.Message); }
        }
    }

    public sealed class EFCanalExperimentProbe : MonoBehaviour
    {
        EFMapWorld world;
        EFMapPlayer player;
        EFCanalExperiment experiment;
        CharacterController controller;
        StringBuilder report;
        int passed;
        bool approachStarted, approachCompleted;
        string phase = "runtime entry";
        BoxCollider activeSupportFloor;
        readonly List<GameObject> temporaryFixtures = new List<GameObject>();
        readonly List<Collider> ignoredFixtureFloors = new List<Collider>();

        void Check(bool condition, string description)
        {
            report.AppendLine((condition ? "PASS " : "FAIL ") + description);
            Debug.Log("P3 CHECK " + (condition ? "PASS " : "FAIL ") + description);
            if (!condition)
            {
                SafeSupportDiagnostic("failed check: " + description, activeSupportFloor);
                throw new InvalidOperationException(description);
            }
            passed++;
        }

        static string Point(Vector3 value) => string.Format(CultureInfo.InvariantCulture,
            "({0:F6}, {1:F6}, {2:F6})", value.x, value.y, value.z);

        static string ColliderName(Collider body)
        {
            if (body == null) return "none";
            string path = body.name;
            for (var parent = body.transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path + " id=" + body.GetInstanceID() + " layer=" + body.gameObject.layer +
                " enabled=" + body.enabled + " trigger=" + body.isTrigger;
        }

        void SafeSupportDiagnostic(string label, BoxCollider expectedFloor)
        {
            try { SupportDiagnostic(label, expectedFloor); }
            catch (Exception error) { report.AppendLine("DIAGNOSTIC ERROR " + label + ": " + error.GetType().Name + " " + error.Message); }
        }

        void SupportDiagnostic(string label, BoxCollider expectedFloor)
        {
            report.AppendLine("DIAGNOSTIC [SUPPORT] " + label + " frame=" + Time.frameCount);
            if (controller == null || player == null) { report.AppendLine("  Player/controller unavailable."); return; }
            Physics.SyncTransforms();
            Vector3 feet = controller.bounds.center - Vector3.up * controller.bounds.extents.y;
            report.AppendLine("  player=" + Point(player.transform.position) + " feet=" + Point(feet) +
                " controller.center=" + Point(controller.center) + " bounds.center=" + Point(controller.bounds.center) +
                " bounds.extents=" + Point(controller.bounds.extents));
            report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  controller.enabled={0} grounded={1} player.enabled={2} riding={3} height={4:F6} radius={5:F6} skin={6:F6} step={7:F6} minMove={8:F6}",
                controller.enabled, controller.isGrounded, player.enabled, player.Riding,
                controller.height, controller.radius, controller.skinWidth, controller.stepOffset, controller.minMoveDistance));
            var ray = new Ray(feet + Vector3.up * .15f, Vector3.down);
            report.AppendLine("  ray.origin=" + Point(ray.origin) + " direction=" + Point(ray.direction) + " distance=0.400000 mask=all-except-layer-2 triggers=Ignore");
            if (expectedFloor != null)
            {
                var local = expectedFloor.transform.InverseTransformPoint(feet);
                report.AppendLine("  expected=" + ColliderName(expectedFloor) + " position=" + Point(expectedFloor.transform.position) +
                    " scale=" + Point(expectedFloor.transform.lossyScale) + " size=" + Point(expectedFloor.size) +
                    " center=" + Point(expectedFloor.center) + " feet.local=" + Point(local) +
                    " bounds.min=" + Point(expectedFloor.bounds.min) + " bounds.max=" + Point(expectedFloor.bounds.max));
                bool hitExpected = expectedFloor.Raycast(ray, out var designatedHit, .4f);
                report.AppendLine("  designated.Raycast=" + hitExpected + (hitExpected ? string.Format(CultureInfo.InvariantCulture,
                    " distance={0:F6} point={1} normal={2}", designatedHit.distance, Point(designatedHit.point), Point(designatedHit.normal)) : ""));
                report.AppendLine("  IsStandingOn=" + (experiment != null && experiment.IsStandingOn(expectedFloor)));
            }
            bool first = Physics.Raycast(ray, out var firstHit, .4f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            report.AppendLine("  Physics.Raycast.first=" + (first ? ColliderName(firstHit.collider) + " point=" + Point(firstHit.point) : "none"));
            var hits = Physics.RaycastAll(ray, .4f, ~(1 << 2), QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).ToArray();
            report.AppendLine("  Physics.RaycastAll.count=" + hits.Length);
            foreach (var hit in hits)
                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "    distance={0:F6} point={1} normal={2} collider={3}", hit.distance, Point(hit.point), Point(hit.normal), ColliderName(hit.collider)));
            var overlaps = Physics.OverlapBox(feet, new Vector3(.45f, .4f, .45f), Quaternion.identity,
                ~(1 << 2), QueryTriggerInteraction.Ignore);
            report.AppendLine("  Solids overlapping feet diagnostic box=" + string.Join(" | ", overlaps.Select(ColliderName)));
        }

        void Bind()
        {
            world = FindFirstObjectByType<EFMapWorld>();
            if (world == null) throw new InvalidOperationException("World missing after scene entry/reload.");
            player = world.player;
            controller = player.GetComponent<CharacterController>();
            experiment = world.GetComponentInChildren<EFCanalExperiment>(true);
            if (experiment == null) throw new InvalidOperationException("Experiment missing after scene entry/reload.");
            world.SetPaused(false);
            if (world.CinematicActive) world.EndCinematic();
        }

        IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        IEnumerator GroundAt(Vector3 position, BoxCollider expectedFloor = null, string label = "ground fixture")
        {
            activeSupportFloor = expectedFloor;
            report.AppendLine("DIAGNOSTIC [GROUND FIXTURE] " + label + " requested=" + Point(position));
            player.Teleport(position + Vector3.up * .5f);
            Physics.SyncTransforms();
            for (int i = 0; i < 30; i++)
            {
                var flags = controller.Move(Vector3.down * .1f);
                yield return null;
                report.AppendLine("  move=" + (i + 1) + " position=" + Point(player.transform.position) +
                    " flags=" + flags + " grounded=" + controller.isGrounded);
                if (controller.isGrounded) break;
            }
            Physics.SyncTransforms();
            SafeSupportDiagnostic(label + " settled", expectedFloor);
        }

        BoxCollider FixtureFloor(string name, Vector3 top, Vector3 size)
        {
            var go = new GameObject(name);
            temporaryFixtures.Add(go);
            go.transform.position = top - Vector3.up * (size.y * .5f);
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }

        IEnumerator SupportRegressionFixtures()
        {
            // Isolated runtime geometry exercises the support predicate only.
            // This never changes the saved deck or proves its approach route.
            Vector3 top = experiment.departureCheckpoint.position + Vector3.up * 80 + Vector3.right * 100;
            var designated = FixtureFloor("Support regression designated floor", top, new Vector3(6, .5f, 6));
            var overlapping = FixtureFloor("Support regression overlapping solid", top, new Vector3(4, .5f, 4));
            yield return GroundAt(top + Vector3.up * 1.12f, designated, "coplanar overlap predicate fixture");
            Check(controller.isGrounded && experiment.IsStandingOn(designated), "[SUPPORT REGRESSION FIXTURE] Coplanar overlapping solids permit designated deck support");
            overlapping.transform.position += Vector3.up * .005f;
            Physics.SyncTransforms();
            yield return GroundAt(top + Vector3.up * 1.125f, designated, "5 mm seam difference predicate fixture");
            Check(controller.isGrounded && experiment.IsStandingOn(designated), "[SUPPORT REGRESSION FIXTURE] 5 mm overlapping seam difference remains valid support");
            overlapping.transform.position += Vector3.up * .115f;
            Physics.SyncTransforms();
            yield return GroundAt(top + Vector3.up * 1.24f, designated, "12 cm higher solid predicate fixture");
            Check(controller.isGrounded && !experiment.IsStandingOn(designated), "[SUPPORT REGRESSION FIXTURE] Grounding on a different solid 12 cm higher cannot count as deck support");
            overlapping.enabled = false;
            var lower = FixtureFloor("Support regression lower terrain surrogate", top - Vector3.up * .9f, new Vector3(4, .5f, 4));
            Physics.IgnoreCollision(controller, designated, true);
            ignoredFixtureFloors.Add(designated);
            Physics.SyncTransforms();
            yield return GroundAt(top + Vector3.up * .22f, designated, "lower terrain surrogate predicate fixture");
            Check(controller.isGrounded && !experiment.IsStandingOn(designated), "[SUPPORT REGRESSION FIXTURE] Grounding on lower terrain surrogate cannot count as raised deck support");
            Physics.IgnoreCollision(controller, designated, false);
            ignoredFixtureFloors.Remove(designated);
            lower.enabled = false;
            player.Teleport(top + Vector3.up * 3);
            controller.Move(Vector3.down * .01f);
            Physics.SyncTransforms();
            SafeSupportDiagnostic("airborne predicate fixture", designated);
            Check(!controller.isGrounded && !experiment.IsStandingOn(designated), "[SUPPORT REGRESSION FIXTURE] Airborne passage above the deck is rejected");
            yield return GroundAt(top + Vector3.right * 2.95f + Vector3.up * 1.12f,
                designated, "near edge predicate fixture");
            Check(controller.isGrounded && experiment.IsStandingOn(designated), "[SUPPORT REGRESSION FIXTURE] Grounded center 5 cm inside a broad deck edge is accepted");
            designated.enabled = false;
            CleanupTemporaryFixtures();
            activeSupportFloor = null;
            yield return null;
        }

        void CleanupTemporaryFixtures()
        {
            foreach (var floor in ignoredFixtureFloors)
                if (controller != null && floor != null) Physics.IgnoreCollision(controller, floor, false);
            ignoredFixtureFloors.Clear();
            foreach (var go in temporaryFixtures) if (go != null) Destroy(go);
            temporaryFixtures.Clear();
        }

        IEnumerator WalkOffParkedLeaf()
        {
            // The existing ride parks its physical leaf slightly above the deck.
            // Step off through real player input instead of treating leaf support
            // as deck support or teleporting to the next assertion's position.
            activeSupportFloor = experiment.arrivalFloor;
            Vector3 start = player.transform.position;
            Vector3 target = start + Vector3.right * 2.2f;
            report.AppendLine("DIAGNOSTIC [PHYSICAL WALK] Parked leaf to adjacent deck; start=" + Point(start) +
                " target=" + Point(target) + " maxFrames=180 radius=0.150000; world/experiment automatic updates remain disabled.");
            SafeSupportDiagnostic("parked leaf endpoint before walking", experiment.arrivalFloor);
            player.enabled = true;
            int frames = 0;
            float remaining = Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up).magnitude;
            while (remaining > .15f && frames < 180)
            {
                Vector3 direction = Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up).normalized;
                Vector3 forward = player.follow.PlanarForward;
                Vector3 right = player.follow.PlanarRight;
                TestInput.TestMovement = new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward));
                report.AppendLine("  walk.frame=" + (frames + 1) + " before=" + Point(player.transform.position) +
                    " forward=" + Point(forward) + " right=" + Point(right) +
                    " input=" + Point(new Vector3(TestInput.TestMovement.x, TestInput.TestMovement.y, 0)));
                yield return null;
                frames++;
                remaining = Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up).magnitude;
            }
            TestInput.TestMovement = Vector2.zero;
            report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  walk.complete frames={0} position={1} horizontalRemaining={2:F6} actualZDrift={3:F6}",
                frames, Point(player.transform.position), remaining, player.transform.position.z - start.z));
            Check(remaining <= .15f, "[PHYSICAL SEGMENT] Real player movement steps 2.2 m sideways from parked leaf within target tolerance");
            yield return Frames(20);
            player.enabled = false;
            SafeSupportDiagnostic("after actual walk off parked leaf and settling", experiment.arrivalFloor);
        }

        IEnumerator AdvanceFixture(int stage)
        {
            Vector3 target = world.stages[stage].arrival.position;
            var floor = new GameObject("Test fixture support P" + (stage + 1));
            temporaryFixtures.Add(floor);
            floor.transform.position = target - Vector3.up * 1.3f;
            var box = floor.AddComponent<BoxCollider>();
            box.size = new Vector3(2, .5f, 2);
            Physics.SyncTransforms();
            yield return GroundAt(target, box, "prior-stage " + stage);
            Check(controller.isGrounded, "[API FIXTURE] Grounded test support before prior-stage assistance " + stage);
            Check(world.TryPassage(stage) && world.StageIndex == stage + 1 && world.Completed[stage], "[API FIXTURE] Prior-stage passage completion " + stage + " (fixture support; no route claim)");
            Destroy(floor);
            yield return null;
        }

        IEnumerator Tests()
        {
            yield return Frames(2);
            Bind();
            TestInput.TestMode = true;
            TestInput.TestMovement = Vector2.zero;
            TestInput.TestPress = KeyCode.None;
            EFMapInput.TestClick = false;
            EFMapInput.TestLookDelta = Vector2.zero;
            Time.captureDeltaTime = 1f / 60;
            Check(world.gameObject.scene.path == EFCanalExperimentSetup.ScenePath, "[RUNTIME] Entered saved experimental scene");
            Check(world.StageIndex == 0 && world.Completed.All(v => !v) && !world.WheelRunning && !world.Ended, "[RUNTIME] Fresh world starts with six incomplete stages");
            // Disable automatic progression while arranging deliberately synthetic fixtures.
            world.enabled = false; player.enabled = false; experiment.enabled = false;
            for (int stage = 0; stage < EFCanalExperiment.Stage; stage++) yield return AdvanceFixture(stage);
            world.SetCheckpoint(world.stages[EFCanalExperiment.Stage].spawn.position);
            activeSupportFloor = null;
            phase = "physical canal approach and camera-ray helper";
            approachStarted = true;
            yield return EFCanalApproachChecks.Run(world, experiment, Check, line => report.AppendLine(line));
            approachCompleted = true;
            report.AppendLine("EXECUTED [AUTOMATED APPROACH] Existing canal spawn through courtyard and side stairs to prepared departure deck, actual player movement, programmatically aimed camera-ray ability inputs, and gate animation over frames. This is not human camera operation or a full game playthrough.");
            phase = "support regression fixtures";
            yield return SupportRegressionFixtures();
            phase = "API preparation and checkpoint fixtures";
            Check(!experiment.water.Filled && !experiment.water.Frozen, "[API FIXTURE] Canal starts dry and unfrozen");
            experiment.water.Apply(Ability.Freeze);
            Check(!experiment.water.Frozen && !experiment.water.Filled, "[API FIXTURE] Dry FREEZE changes no water state");
            player.selected = Ability.Pulse;
            Vector3 pumpPoint = experiment.source.indicators[0].bounds.center;
            player.Teleport(pumpPoint + Vector3.right * (player.castRange + 3));
            Check(!player.CanCastAt(experiment.source, pumpPoint, out var denial) && denial.Contains("far"), "[API FIXTURE] Real player range guard rejects distant pump");
            Check(!experiment.source.CanApply(Ability.Bloom, out _), "[API FIXTURE] Wrong pump ability rejected");
            experiment.source.Apply(Ability.Pulse);
            experiment.gate.Tick(0);
            foreach (var action in experiment.gate.actions) action.Tick(action.Duration);
            experiment.gate.Tick(0); experiment.water.Tick(0);
            Check(experiment.source.SourceOn && experiment.gate.Done && experiment.water.Filled, "[API FIXTURE] PULSE plus gate animation final sample fills water (not elapsed-time playback)");
            experiment.TickLocalState(0);
            Check(experiment.lowWaterSurfaces.Length > 0 && experiment.lowWaterSurfaces.All(r => r != null && r.enabled), "[API FIXTURE] Prepared low basin visibly contains water");
            experiment.source.Apply(Ability.Pulse);
            experiment.water.Apply(Ability.Freeze);
            Check(experiment.water.Frozen && experiment.water.Satisfied, "[API FIXTURE] Filled water freezes");
            experiment.water.Apply(Ability.Freeze);
            Check(!experiment.water.Frozen, "[API FIXTURE] Repeated FREEZE releases before completion");
            experiment.water.Apply(Ability.Freeze);
            Vector3 oldCheckpoint = world.Checkpoint;
            player.Teleport(experiment.departureCheckpoint.position + Vector3.up * 2);
            controller.Move(Vector3.down * .001f);
            experiment.TickLocalState(.5f);
            Check(!experiment.DepartureSaved && world.Checkpoint == oldCheckpoint && !experiment.IsStandingOn(experiment.departureFloor), "[API FIXTURE] Airborne deck visit cannot save checkpoint");
            yield return GroundAt(experiment.departureCheckpoint.position, experiment.departureFloor, "actual saved departure checkpoint");
            Check(experiment.IsStandingOn(experiment.departureFloor), "[COLLISION FIXTURE] CharacterController actually supported by departure deck");
            experiment.TickLocalState(.1f);
            Check(!experiment.DepartureSaved, "[API FIXTURE] Checkpoint waits for stable grounded interval");
            experiment.TickLocalState(.1f);
            Check(experiment.DepartureSaved && world.Checkpoint == experiment.departureCheckpoint.position, "[API FIXTURE] Prepared deck saves checkpoint after stable support");
            Vector3 preparedCheckpoint = world.Checkpoint;
            player.Teleport(experiment.fallRecovery.center);
            experiment.TickLocalState(0);
            Check(Vector3.Distance(player.transform.position, preparedCheckpoint) < .01f && experiment.source.SourceOn && experiment.water.Frozen && !world.Completed[3], "[API FIXTURE] Local below-canal recovery volume returns player without losing preparation or granting completion");
            player.Teleport(preparedCheckpoint + Vector3.down * 50);
            world.Respawn();
            Check(experiment.source.SourceOn && experiment.gate.Done && experiment.water.Frozen && experiment.DepartureSaved && world.Checkpoint == preparedCheckpoint, "[API FIXTURE] Explicit fall recovery preserves prepared pump/gate/ice and deck checkpoint");
            // This segment uses the existing real capsule sweep, not node state injection.
            phase = "physical leaf ride and walk onto arrival deck";
            player.Teleport(experiment.water.rideVisual.position + Vector3.up * 1.1f);
            Check(player.TryRide() && player.Riding, "[PHYSICAL SEGMENT] Leaf boarding API accepts prepared nearby leaf (teleported start fixture)");
            player.enabled = true;
            int rideFrames = 0;
            while (player.Riding && rideFrames++ < 700) yield return null;
            Check(player.SlideUsed && !player.Riding, "[PHYSICAL SEGMENT] Existing collision-swept leaf ride reaches its endpoint without recovery");
            activeSupportFloor = experiment.arrivalFloor;
            yield return Frames(25);
            yield return WalkOffParkedLeaf();
            Check(experiment.IsStandingOn(experiment.arrivalFloor), "[PHYSICAL SEGMENT] Actual walk from parked leaf settles onto real far-side deck");
            Check(!world.IsAtArrival(EFCanalExperiment.Stage), "[PHYSICAL SEGMENT] Landing precedes progression line; further walking required");
            var arrival = world.stages[EFCanalExperiment.Stage].arrival;
            phase = "arrival assistance and completed-state fixtures";
            player.Teleport(arrival.position + Vector3.up * .7f);
            controller.Move(Vector3.down * .001f);
            Check(!world.IsAtArrival(EFCanalExperiment.Stage) && !world.TryPassage(EFCanalExperiment.Stage), "[API FIXTURE] Airborne crossing inside arrival bounds does not complete");
            world.Retry(); experiment.TickLocalState(0);
            Check(!experiment.source.SourceOn && !experiment.water.Frozen && !experiment.DepartureSaved && !player.SlideUsed, "[API FIXTURE] R retry API resets current preparation, ride flag and local checkpoint flag");
            yield return GroundAt(arrival.position, experiment.arrivalFloor, "actual saved arrival deck");
            Check(experiment.IsStandingOn(experiment.arrivalFloor) && !world.ConditionsMet(EFCanalExperiment.Stage), "[COLLISION FIXTURE] Real exit support with intentionally unsolved requirements");
            Check(world.TryPassage(EFCanalExperiment.Stage), "[API FIXTURE] Grounded safe-exit assistance remains generous");
            Check(world.Completed[3] && world.StageIndex == 4 && experiment.source.SourceOn && experiment.gate.Done && experiment.water.Filled && experiment.water.Frozen && experiment.water.support.All(c => c != null && c.enabled), "[API FIXTURE] Assisted completion retains safe supplied ice route");
            Check(world.FallFloor < experiment.arrivalFloor.bounds.max.y - 16, "[API FIXTURE] Next-stage fall floor permits descent to original low plateau");
            experiment.water.Apply(Ability.Freeze);
            world.Respawn(); world.Retry(); world.Respawn();
            Check(world.Completed.Take(4).All(v => v) && experiment.water.Frozen && experiment.water.Filled, "[API FIXTURE] Reentry/repeated recovery/current retry cannot regress completed canal");
            foreach (var node in world.stages[5].nodes) node.ApplyCompletedState();
            Check(!world.Completed[5] && !world.WheelRunning && world.stages[5].nodes.All(n => !n.Done), "[API FIXTURE] Completion stabilization API cannot solve final stage");
            world.enabled = true; player.enabled = true; experiment.enabled = true;
            phase = "experimental scene restart";
            for (int restart = 0; restart < 2; restart++)
            {
                world.RestartWorld();
                yield return Frames(8);
                Bind();
                Check(world.gameObject.scene.path == EFCanalExperimentSetup.ScenePath && world.StageIndex == 0 && world.Completed.All(v => !v) && !world.WheelRunning && !world.Ended && !experiment.source.SourceOn && !experiment.water.Frozen && !experiment.DepartureSaved, "[RUNTIME] Actual restart API reloads experimental copy and clears all experiment states, repetition " + (restart + 1));
                Check(FindObjectsByType<EFMapPlayer>(FindObjectsSortMode.None).Length == 1 && Time.timeScale == 1, "[RUNTIME] Restart leaves one player and normal time scale");
            }
            Check(SessionState.GetString(EFCanalExperimentTests.SnapshotKey, "") == EFCanalExperimentTests.ProtectedSnapshot(), "[PRESERVATION] Protected original/prefab/title/HUD material/logo/build/profile hashes unchanged by complete check run");
            phase = "all scheduled checks finished";
        }

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            report = new StringBuilder(SessionState.GetString(EFCanalExperimentTests.ReportKey, ""));
            var stack = new Stack<IEnumerator>();
            stack.Push(Tests());
            bool failed = false;
            while (stack.Count > 0)
            {
                bool more = false; object next = null;
                try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
                catch (Exception error)
                {
                    failed = true; report.AppendLine("FAIL [RUNTIME] " + error);
                    SafeSupportDiagnostic("failure active support", activeSupportFloor);
                    if (experiment != null)
                    {
                        SafeSupportDiagnostic("failure departure deck", experiment.departureFloor);
                        SafeSupportDiagnostic("failure arrival deck", experiment.arrivalFloor);
                    }
                    Debug.LogException(error); break;
                }
                if (!more)
                {
                    var completed = stack.Pop();
                    try { (completed as IDisposable)?.Dispose(); }
                    catch (Exception error) { failed = true; report.AppendLine("FAIL [ITERATOR CLEANUP] " + error); break; }
                    continue;
                }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
            while (stack.Count > 0)
            {
                try { (stack.Pop() as IDisposable)?.Dispose(); }
                catch (Exception error) { failed = true; report.AppendLine("FAIL [ITERATOR CLEANUP] " + error); }
            }
            CleanupTemporaryFixtures();
            TestInput.TestMode = false; TestInput.TestMovement = Vector2.zero; TestInput.TestPress = KeyCode.None;
            EFMapInput.TestClick = false; EFMapInput.TestLookDelta = Vector2.zero; Time.captureDeltaTime = 0;
            report.AppendLine("Runtime checks passed: " + passed + "; overall " + (failed ? "FAIL" : "PASS") + ".");
            report.AppendLine("Last phase: " + phase + (failed ? "; subsequent phases were not executed in this run." : "."));
            if (!approachStarted) report.AppendLine("NOT EXECUTED: automated canal approach and programmatically aimed camera-ray helper in this run.");
            else if (!approachCompleted) report.AppendLine("INCOMPLETE: automated canal approach/camera-ray helper; only its individual recorded PASS results were reached, and subsequent phases were not executed.");
            report.AppendLine("NOT VERIFIED: full start-to-end physical traversal, human/OS-controlled camera play, human comprehension/fun/time, title-to-experiment routing, final PULSE/restoration/skip, standalone player build.");
            string output = SessionState.GetString(EFCanalExperimentTests.OutputKey, "docs/everflow_mvp/evidence/p3-checks-fallback-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, report.ToString());
            Debug.Log("P3 EXPERIMENT CHECKS " + (failed ? "FAIL" : "PASS") + "; evidence=" + output);
            EFCanalExperimentTests.RestoreRunSettings();
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            else EditorApplication.ExitPlaymode();
        }
    }

    // Called explicitly by the P3 probe. This does not create/save a scene or
    // skip blocked geometry. Retry is used only at the two reset boundaries.
    internal static class EFCanalApproachChecks
    {
        internal static IEnumerator Run(EFMapWorld w, EFCanalExperiment e,
            Action<bool, string> check, Action<string> log, bool keepPreparedForTraversal=false)
        {
            if (w == null || e == null || w.player == null) throw new InvalidOperationException("Approach references missing.");
            var player = w.player;
            bool playerEnabled = player.enabled, experimentEnabled = e.enabled, worldEnabled = w.enabled;
            var work = new Stack<IEnumerator>();
            try
            {
                var probe = new Approach(w, e, check, log);
                probe.Require(TestInput.TestMode && w.StageIndex == EFCanalExperiment.Stage && !worldEnabled && !playerEnabled && !experimentEnabled,
                    "[PHYSICAL APPROACH] Entry contract: active canal after prior-stage fixtures, test input on, world/player/experiment initially disabled");
                probe.Require(e.originalTerrain != null && player.follow != null && player.follow.enabled && player.view != null && player.view.enabled && w.GameInputActive,
                    "[PHYSICAL APPROACH] Original terrain and active gameplay camera/input available");
                ClearInput();
                w.Retry();
                player.enabled = true;
                e.enabled = true;
                work.Push(probe.Sequence());
                // Drive nested routines here so a failure in Walk/Aim unwinds
                // this finally even if the caller only advances Run.MoveNext().
                while (work.Count > 0)
                {
                    var current = work.Peek();
                    if (!current.MoveNext())
                    {
                        work.Pop();
                        (current as IDisposable)?.Dispose();
                        continue;
                    }
                    if (current.Current is IEnumerator nested) work.Push(nested);
                    else yield return current.Current;
                }
            }
            finally
            {
                while (work.Count > 0) (work.Pop() as IDisposable)?.Dispose();
                ClearInput();
                try
                {
                    if (!keepPreparedForTraversal && w != null && w.StageIndex == EFCanalExperiment.Stage)
                    {
                        w.Retry();
                        if (e != null) e.TickLocalState(0);
                        log?.Invoke("[PHYSICAL APPROACH] Cleanup: Retry returned to dry canal spawn; helper input cleared.");
                    }
                }
                finally
                {
                    if (player != null) player.enabled = playerEnabled;
                    if (e != null) e.enabled = experimentEnabled;
                    if (w != null) w.enabled = worldEnabled;
                    ClearInput();
                }
            }
        }

        static void ClearInput()
        {
            TestInput.TestMovement = Vector2.zero;
            TestInput.TestPress = KeyCode.None;
            EFMapInput.TestClick = false;
            EFMapInput.TestLookDelta = Vector2.zero;
        }

        sealed class Approach
        {
            readonly EFMapWorld world;
            readonly EFCanalExperiment experiment;
            readonly EFMapPlayer player;
            readonly CharacterController controller;
            readonly Action<bool, string> check;
            readonly Action<string> log;
            Vector3 Origin => experiment.originalTerrain.position;
            Vector3 Local => player.transform.position - Origin;

            internal Approach(EFMapWorld w, EFCanalExperiment e, Action<bool, string> c, Action<string> l)
            { world=w; experiment=e; player=w.player; controller=player.GetComponent<CharacterController>(); check=c; log=l; }

            internal void Require(bool condition, string message)
            {
                check(condition,message);
                if (!condition) throw new InvalidOperationException(message);
            }
            void Log(string message) { log?.Invoke(message); }
            static string Point(Vector3 p) => string.Format(CultureInfo.InvariantCulture,"({0:F4},{1:F4},{2:F4})",p.x,p.y,p.z);
            IEnumerator Frames(int count) { for (int i=0;i<count;i++) yield return null; }

            internal IEnumerator Sequence()
            {
                yield return Frames(8);
                Log("[PHYSICAL APPROACH] Start at existing spawn local="+Point(Local)+"; no fixture teleport, jump, disabled collider or manual movement method used.");
                Require(controller != null && controller.enabled && !experiment.water.Filled && !experiment.water.Frozen,
                    "[PHYSICAL APPROACH] Retry starts a dry canal with the real CharacterController enabled");
                // The old planning waypoint y=9 is a rough hint; the recorded
                // terrain at (10,-44) is y10.782. Check actual grounded height.
                yield return WalkTo(new Vector3(10,9,-44),float.NegativeInfinity,float.PositiveInfinity,"existing ridge");
                yield return WalkTo(new Vector3(2,15.3f,-48),float.NegativeInfinity,float.PositiveInfinity,"new approach stair entrance");
                yield return WalkTo(new Vector3(-7,22.8f,-48),23.3f,24.6f,"approach stairs to courtyard");
                yield return WalkTo(new Vector3(-11,23.92f,-49),23.3f,24.6f,"low courtyard observation space");

                var low = experiment.GetComponentsInChildren<BoxCollider>(true).Single(c=>c.name=="Ability target - low basin water");
                var high = experiment.water.GetComponentsInChildren<BoxCollider>(true).Single(c=>c.name=="Ability target - slide surface");
                yield return ClickAt(experiment.water,low.bounds.center,Ability.Freeze,false,"dry low basin FREEZE");
                Require(!experiment.water.Frozen && !experiment.water.Filled,"[RAY INPUT] Dry FREEZE preserves dry/unfrozen water");
                yield return ClickAt(experiment.source,experiment.source.indicators[0].bounds.center,Ability.Pulse,true,"low pump PULSE");
                Require(experiment.source.SourceOn,"[RAY INPUT] Actual pump click powers its source");
                float fillStart=Time.time;
                yield return Frames(100);
                Log("[RAY INPUT] Real gate elapsed seconds="+(Time.time-fillStart).ToString("F4",CultureInfo.InvariantCulture)+"; gate.Done="+experiment.gate.Done+" water.Filled="+experiment.water.Filled);
                Require(experiment.gate.Done && experiment.water.Filled,"[RAY INPUT] Gate animation updates over actual frames and fills the canal");
                yield return ClickAt(experiment.water,low.bounds.center,Ability.Freeze,true,"filled low basin FREEZE");
                Require(experiment.water.Frozen,"[RAY INPUT] Low basin click freezes the connected water");
                yield return ClickAt(experiment.water,low.bounds.center,Ability.Freeze,true,"low basin release");
                Require(!experiment.water.Frozen,"[RAY INPUT] Repeated low basin click releases the same water before completion");

                // The pump base is solid. Follow the open south edge of the
                // courtyard, then enter the first tread instead of walking
                // diagonally through that machine or the side of higher steps.
                yield return WalkTo(new Vector3(-12,22.8f,-53.1f),23.3f,24.9f,"courtyard lane west of solid pump");
                yield return WalkTo(new Vector3(-7.1f,22.8f,-53.1f),23.3f,24.9f,"courtyard lane south of solid pump");
                yield return WalkTo(new Vector3(-6.9f,22.8f,-52),23.3f,24.9f,"short side stair entrance");
                var sideSummit=experiment.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Short side access 25");
                yield return WalkTo(sideSummit.position-Origin+Vector3.up*1.37f,31.7f,33,"side stair summit outside deck");
                yield return WalkTo(experiment.departureCheckpoint.position-Origin,31.7f,33,"raised departure deck");
                Require(experiment.IsStandingOn(experiment.departureFloor),"[PHYSICAL APPROACH] Walking up the side stairs reaches the designated departure deck");
                yield return ClickAt(experiment.water,high.bounds.center,Ability.Freeze,true,"high slide surface FREEZE");
                Require(experiment.water.Frozen,"[RAY INPUT] High surface actual click also freezes the connected water");
                yield return Frames(20);
                Log("[PHYSICAL APPROACH] Stable deck local="+Point(Local)+" saved="+experiment.DepartureSaved+" checkpoint="+Point(world.Checkpoint-Origin));
                Require(experiment.DepartureSaved && Vector3.Distance(world.Checkpoint,experiment.departureCheckpoint.position)<.01f,
                    "[PHYSICAL APPROACH] Prepared canal checkpoint saves after actual grounded approach and stable deck contact");
                Log("[PHYSICAL APPROACH] Scope: existing canal spawn to prepared departure deck with real movement and camera-ray clicks; no full game, human comprehension or playtime measurement.");
            }

            IEnumerator WalkTo(Vector3 localTarget,float minimumY,float maximumY,string name)
            {
                Vector3 target=Origin+localTarget;
                float best=float.PositiveInfinity;
                int noProgress=0, frames=0;
                Log("[PHYSICAL APPROACH] Begin "+name+" target.local="+Point(localTarget)+" grounded centerY range=["+minimumY+","+maximumY+"] start="+Point(Local));
                for (;frames<600;frames++)
                {
                    Vector3 delta=Vector3.ProjectOnPlane(target-player.transform.position,Vector3.up);
                    if(delta.magnitude<=.3f) break;
                    if(delta.magnitude<best-.03f) { best=delta.magnitude; noProgress=0; }
                    else noProgress++;
                    if(frames%90==0 || noProgress>=120)
                    {
                        Log("[PHYSICAL APPROACH] "+name+" frame="+frames+" local="+Point(Local)+" distanceXZ="+delta.magnitude.ToString("F4",CultureInfo.InvariantCulture)+" grounded="+controller.isGrounded+" noProgress="+noProgress);
                        LogFrontObstacle(delta);
                    }
                    if(noProgress>=120) break;
                    Vector3 direction=delta.normalized;
                    TestInput.TestMovement=new Vector2(Vector3.Dot(direction,player.follow.PlanarRight),Vector3.Dot(direction,player.follow.PlanarForward));
                    yield return null;
                }
                TestInput.TestMovement=Vector2.zero;
                yield return Frames(8);
                float remaining=Vector3.ProjectOnPlane(target-player.transform.position,Vector3.up).magnitude;
                Log("[PHYSICAL APPROACH] End "+name+" frames="+frames+" local="+Point(Local)+" distanceXZ="+remaining.ToString("F4",CultureInfo.InvariantCulture)+" grounded="+controller.isGrounded);
                bool groundContact=LogGroundSupport(name);
                Require(remaining<=.35f && controller.isGrounded && groundContact && Local.y>=minimumY && Local.y<=maximumY && world.StageIndex==EFCanalExperiment.Stage,
                    "[PHYSICAL APPROACH] "+name+" reached on real ground within expected height (600-frame cap, 120-frame stall cap)");
            }

            bool LogGroundSupport(string name)
            {
                Vector3 feet=controller.bounds.center-Vector3.up*controller.bounds.extents.y;
                // On a stair corner the capsule can be supported beside its
                // center ray. Sample only inside its actual foot footprint;
                // grounded state and the strict vertical contact range remain.
                for(int sample=0;sample<9;sample++)
                {
                    float angle=(sample-1)*Mathf.PI/4;
                    Vector3 offset=sample==0?Vector3.zero:new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*controller.radius*.8f;
                    foreach(var hit in Physics.RaycastAll(feet+offset+Vector3.up*.2f,Vector3.down,.7f,~(1<<2),QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                    {
                        if(hit.collider.transform.IsChildOf(player.transform)) continue;
                        if(hit.normal.y<=.5f || Mathf.Abs(feet.y-hit.point.y)>.3f) continue;
                        Log("[PHYSICAL APPROACH] Ground "+name+" collider="+hit.collider.name+" point.local="+Point(hit.point-Origin)+
                            " feet.local="+Point(feet-Origin)+" footprint.offset="+Point(offset)+" normal="+Point(hit.normal)+" rayDistance="+hit.distance.ToString("F4",CultureInfo.InvariantCulture));
                        return true;
                    }
                }
                Log("[PHYSICAL APPROACH] Ground "+name+": no close upward-facing solid within the actual controller footprint.");
                return false;
            }

            void LogFrontObstacle(Vector3 direction)
            {
                if(direction.sqrMagnitude<.001f) return;
                Bounds bounds=controller.bounds;
                float radius=Mathf.Max(bounds.extents.x,bounds.extents.z);
                float halfAxis=Mathf.Max(0,bounds.extents.y-radius);
                Vector3 bottom=bounds.center-Vector3.up*halfAxis, top=bounds.center+Vector3.up*halfAxis;
                foreach(var hit in Physics.CapsuleCastAll(bottom,top,radius,direction.normalized,.9f,~(1<<2),QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                    if(!hit.collider.transform.IsChildOf(player.transform))
                        Log("[PHYSICAL APPROACH] Nearby solid="+hit.collider.name+" distance="+hit.distance.ToString("F4",CultureInfo.InvariantCulture)+" hit="+Point(hit.point)+" normal="+Point(hit.normal));
                foreach(var hit in Physics.CapsuleCastAll(bottom,top,radius,Vector3.up,.2f,~(1<<2),QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                    if(!hit.collider.transform.IsChildOf(player.transform))
                        Log("[PHYSICAL APPROACH] Head clearance solid="+hit.collider.name+" distance="+hit.distance.ToString("F4",CultureInfo.InvariantCulture)+" hit="+Point(hit.point)+" normal="+Point(hit.normal));
            }

            IEnumerator ClickAt(EFMapNode node,Vector3 point,Ability ability,bool expectedApplied,string name)
            {
                TestInput.TestMovement=Vector2.zero;
                EFMapInput.TestLookDelta=Vector2.zero;
                // Iterate the ordinary follow camera to solve shoulder parallax.
                // Neither its transform, collision settings nor cast range changes.
                for(int i=0;i<40;i++)
                {
                    Vector3 delta=point-player.view.transform.position;
                    if(delta.sqrMagnitude>.001f)
                    {
                        Vector3 angles=Quaternion.LookRotation(delta).eulerAngles;
                        player.follow.yaw=angles.y;
                        player.follow.pitch=Mathf.Clamp(Mathf.DeltaAngle(0,angles.x),player.follow.minPitch,player.follow.maxPitch);
                    }
                    yield return null;
                }
                TestInput.TestPress=ability==Ability.Freeze?KeyCode.Alpha2:ability==Ability.Pulse?KeyCode.Alpha3:KeyCode.Alpha1;
                yield return null;
                TestInput.TestPress=KeyCode.None;
                yield return null;
                Log("[RAY INPUT] "+name+" local="+Point(Local)+" target="+node.name+" targetPoint="+Point(point)+" range3D="+Vector3.Distance(player.transform.position,point).ToString("F4",CultureInfo.InvariantCulture)+
                    " camera="+Point(player.view.transform.position)+" yaw="+player.follow.yaw+" pitch="+player.follow.pitch+
                    " hovered="+(player.Hovered==null?"none":player.Hovered.name)+" collider="+(player.AimCollider==null?"none":player.AimCollider.name)+" ready="+player.AimReady+" reason="+player.AimReason);
                Require(player.selected==ability && player.Hovered==node,
                    "[RAY INPUT] "+name+": actual selected ability and center camera ray identify the intended receiver");
                int beforeFrame=player.LastCastFrame;
                EFMapInput.TestClick=true;
                yield return null;
                EFMapInput.TestClick=false;
                yield return null;
                Log("[RAY INPUT] "+name+" result="+player.LastCastResult+" castFrame="+player.LastCastFrame+" previousFrame="+beforeFrame);
                Require(player.LastCastFrame>beforeFrame && player.LastCastResult!=null &&
                    player.LastCastResult.StartsWith(expectedApplied?"Applied:":"Blocked:",StringComparison.Ordinal),
                    "[RAY INPUT] "+name+": real player click "+(expectedApplied?"applies":"rejects")+" ability");
                if(!expectedApplied)
                    Require(player.LastCastResult.IndexOf("PULSE",StringComparison.OrdinalIgnoreCase)>=0,
                        "[RAY INPUT] Dry FREEZE is rejected for missing supplied water rather than range or obstruction");
            }
        }
    }
}
