using System;
using System.Linq;
class EditorBuildSettingsScene {
    public string path; public bool enabled;
    public EditorBuildSettingsScene(string p, bool e) { path=p; enabled=e; }
}
enum BuildResult { Succeeded, Failed, Cancelled, Unknown }
static class EFTitleScreen { public const string GameplayScene = "Assets/Scenes/Everflow_Map.unity"; }
static class EFReleaseSetup { public const string TitleScene = "Assets/EverflowMVP/Scenes/Everflow_Title.unity";
        internal static EditorBuildSettingsScene[] CreateReleaseScenes(EditorBuildSettingsScene[] existing)
        {
            return new[] { new EditorBuildSettingsScene(TitleScene, true),
                new EditorBuildSettingsScene(EFTitleScreen.GameplayScene, true) }
                .Concat(existing.Where(s => s.path != TitleScene && s.path != EFTitleScreen.GameplayScene)
                    .Select(s => new EditorBuildSettingsScene(s.path, false))).ToArray();
        }

        internal static void ValidateBuildScenes(EditorBuildSettingsScene[] scenes)
        {
            var enabled = scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Require(enabled.Length >= 2 && enabled[0] == TitleScene && enabled[1] == EFTitleScreen.GameplayScene &&
                enabled.Count(p => p == TitleScene) == 1 && enabled.Count(p => p == EFTitleScreen.GameplayScene) == 1,
                "The title and exhibition map must be the first two enabled scenes, without duplicates. Run Tools > EVERFLOW > Release > 1. Set Up Title and Windows Settings.");
        }

        static void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException("EVERFLOW validation: " + message);
        }
}
static class EFReleaseBuild {

        public static string NextVersion(string current)
        {
            string[] parts = current.Split('.');
            if (parts.Length != 3 || !int.TryParse(parts[0], out int major) || major < 0 ||
                !int.TryParse(parts[1], out int minor) || minor < 0 ||
                !int.TryParse(parts[2], out int patch) || patch < 0)
                throw new InvalidOperationException("Expected major.minor.patch; found " + current);
            return major + "." + minor + "." + checked(patch + 1);
        }
        public static BuildResult Transaction(string previous, string candidate, Action<string> setVersion,
            Func<BuildResult> build, Action commitSuccess)
        {
            bool committed = false;
            try
            {
                setVersion(candidate);
                var result = build();
                if (result == BuildResult.Succeeded)
                {
                    commitSuccess();
                    committed = true;
                }
                return result;
            }
            finally { setVersion(committed ? candidate : previous); }
        }
}
static class Program {
    static int checks;
    static void Check(bool result, string name) { if(!result) throw new Exception("FAIL: "+name); checks++; Console.WriteLine("PASS: "+name); }
    static void Reject(Action action, string name) {
        try { action(); } catch(InvalidOperationException) { Check(true,name); return; }
        throw new Exception("FAIL: accepted "+name);
    }
    static bool Equal(EditorBuildSettingsScene[] a, EditorBuildSettingsScene[] b) =>
        a.Select(s=>(s.path,s.enabled)).SequenceEqual(b.Select(s=>(s.path,s.enabled)));
    public static void Main() {
        string t=EFReleaseSetup.TitleScene, m=EFTitleScreen.GameplayScene;
        const string legacy="Assets/EverflowMVP/Scenes/Everflow_6Puzzles.unity";
        var saved = new EditorBuildSettingsScene[] { new EditorBuildSettingsScene("Assets/EverflowMVP/Scenes/Everflow_Title.unity",true),new EditorBuildSettingsScene("Assets/Scenes/Everflow_Map.unity",true),new EditorBuildSettingsScene("Assets/EverflowMVP/Scenes/Everflow_6Puzzles.unity",true),new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity",false) };
        EFReleaseSetup.ValidateBuildScenes(saved); Check(true,"actual saved settings (Title/Map/MVP enabled) accepted");
        var original = new[] { new EditorBuildSettingsScene(legacy,true), new EditorBuildSettingsScene(t,false),
            new EditorBuildSettingsScene(m,false), new EditorBuildSettingsScene("utility-a",true),
            new EditorBuildSettingsScene(t,true), new EditorBuildSettingsScene(m,true), new EditorBuildSettingsScene("utility-b",false) };
        var snapshot = original.Select(s=>new EditorBuildSettingsScene(s.path,s.enabled)).ToArray();
        var result=EFReleaseSetup.CreateReleaseScenes(original);
        Check(Equal(original,snapshot),"source entries and order preserved in memory");
        Check(result.Length==5&&result[0].path==t&&result[0].enabled&&result[1].path==m&&result[1].enabled,"duplicate title/map coalesced to enabled first two");
        Check(result.Skip(2).Select(s=>s.path).SequenceEqual(new[]{legacy,"utility-a","utility-b"})&&result.Skip(2).All(s=>!s.enabled),"utility order retained disabled");
        EFReleaseSetup.ValidateBuildScenes(result); Check(true,"constructed release list accepted");
        for(int i=0;i<10;i++) { var repeated=EFReleaseSetup.CreateReleaseScenes(result); Check(Equal(result,repeated),"repeat list construction "+(i+1)); result=repeated; }
        var emptyConfigured=EFReleaseSetup.CreateReleaseScenes(Array.Empty<EditorBuildSettingsScene>());
        Check(emptyConfigured.Length==2&&emptyConfigured[0].path==t&&emptyConfigured[1].path==m,"empty input gets Title/Map");
        Reject(()=>EFReleaseSetup.ValidateBuildScenes(Array.Empty<EditorBuildSettingsScene>()),"empty build list rejected");
        Reject(()=>EFReleaseSetup.ValidateBuildScenes(new[]{new EditorBuildSettingsScene(t,true)}),"missing map rejected");
        Reject(()=>EFReleaseSetup.ValidateBuildScenes(new[]{new EditorBuildSettingsScene(m,true),new EditorBuildSettingsScene(t,true)}),"reversed scenes rejected");
        Reject(()=>EFReleaseSetup.ValidateBuildScenes(new[]{new EditorBuildSettingsScene(t,true),new EditorBuildSettingsScene(legacy,true)}),"legacy substitution rejected");
        Reject(()=>EFReleaseSetup.ValidateBuildScenes(new[]{new EditorBuildSettingsScene(t,false),new EditorBuildSettingsScene(m,true)}),"disabled title rejected");
        Reject(()=>EFReleaseSetup.ValidateBuildScenes(new[]{new EditorBuildSettingsScene(t,true),new EditorBuildSettingsScene(m,true),new EditorBuildSettingsScene(t,true)}),"duplicate enabled title rejected");
        Reject(()=>EFReleaseSetup.ValidateBuildScenes(new[]{new EditorBuildSettingsScene(t,true),new EditorBuildSettingsScene(m,true),new EditorBuildSettingsScene(m,true)}),"duplicate enabled map rejected");
        EFReleaseSetup.ValidateBuildScenes(new[]{new EditorBuildSettingsScene("disabled-prefix",false),new EditorBuildSettingsScene(t,true),new EditorBuildSettingsScene(m,true),new EditorBuildSettingsScene("utility",true)});
        Check(true,"disabled prefix and enabled utility accepted");
        Check(EFReleaseBuild.NextVersion("1.0.1")=="1.0.2","patch increment");
        Check(EFReleaseBuild.NextVersion("2.3.99")=="2.3.100","no patch rollover");
        foreach(var state in Enum.GetValues<BuildResult>()) {
            string version="1.0.1"; int commits=0;
            var returned=EFReleaseBuild.Transaction(version,"1.0.2",v=>version=v,()=>{Check(version=="1.0.2","candidate embedded during "+state); return state;},()=>commits++);
            Check(returned==state,"result preserved "+state);
            Check(version==(state==BuildResult.Succeeded?"1.0.2":"1.0.1"),"final version "+state);
            Check(commits==(state==BuildResult.Succeeded?1:0),"single success commit "+state);
        }
        string restored="1.0.1";
        try { EFReleaseBuild.Transaction(restored,"1.0.2",v=>restored=v,()=>throw new OperationCanceledException(),()=>{}); } catch(OperationCanceledException) {}
        Check(restored=="1.0.1","thrown build rollback");
        restored="1.0.1";
        try { EFReleaseBuild.Transaction(restored,"1.0.2",v=>restored=v,()=>BuildResult.Succeeded,()=>throw new InvalidOperationException()); } catch(InvalidOperationException) {}
        Check(restored=="1.0.1","thrown commit rollback");
        Console.WriteLine("TOTAL PASS: "+checks);
    }
}
