"""Execute exact production list/version methods with minimal managed API substitutes.

This does not exercise Unity native APIs, scenes, Configure side effects, or Play mode.
"""
from pathlib import Path
import json
import re
import subprocess

root = Path(__file__).resolve().parents[1]
out = root / 'docs/release-integration/verification-logic'
out.mkdir(parents=True, exist_ok=True)
setup = (root / 'Assets/EverflowMVP/Editor/EFReleaseSetup.cs').read_text(encoding='utf-8-sig')
build = (root / 'Assets/EverflowMVP/Editor/EFReleaseBuild.cs').read_text(encoding='utf-8-sig')
title = (root / 'Assets/EverflowMVP/Runtime/EFTitleScreen.cs').read_text(encoding='utf-8-sig')


def method(source, name):
    # These selected methods contain no brace-bearing strings/comments.
    match = re.search(r'^\s*(?:internal |public |private )?static [^\n]+\b' + name + r'\(', source, re.M)
    if not match:
        raise ValueError('Method missing: ' + name)
    start = source.index('{', match.start())
    depth = 0
    for index in range(start, len(source)):
        depth += (source[index] == '{') - (source[index] == '}')
        if depth == 0:
            return source[match.start():index + 1]
    raise ValueError('Unbalanced method: ' + name)


title_path = re.search(r'const string TitleScene = "([^"]+)"', setup).group(1)
map_path = re.search(r'const string GameplayScene = "([^"]+)"', title).group(1)
saved_settings = (root / 'ProjectSettings/EditorBuildSettings.asset').read_text(encoding='utf-8-sig')
saved = re.findall(r'- enabled: ([01])\s+path: ([^\r\n]+)', saved_settings)
source = '''using System;
using System.Linq;
class EditorBuildSettingsScene {
    public string path; public bool enabled;
    public EditorBuildSettingsScene(string p, bool e) { path=p; enabled=e; }
}
enum BuildResult { Succeeded, Failed, Cancelled, Unknown }
static class EFTitleScreen { public const string GameplayScene = MAP_PATH; }
static class EFReleaseSetup { public const string TitleScene = TITLE_PATH;
SETUP_METHODS
}
static class EFReleaseBuild {
BUILD_METHODS
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
        var saved = new EditorBuildSettingsScene[] { SAVED_SETTINGS };
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
'''
source = source.replace('MAP_PATH', json.dumps(map_path)).replace('TITLE_PATH', json.dumps(title_path))
source = source.replace('SETUP_METHODS', '\n'.join(method(setup, name) for name in ('CreateReleaseScenes', 'ValidateBuildScenes', 'Require')))
source = source.replace('BUILD_METHODS', '\n'.join(method(build, name) for name in ('NextVersion', 'Transaction')))
source = source.replace('SAVED_SETTINGS', ','.join('new EditorBuildSettingsScene(' + json.dumps(path) + ',' + ('true' if enabled == '1' else 'false') + ')' for enabled, path in saved))
(out / 'Program.cs').write_text(source, encoding='utf-8')
refs = Path('C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/9.0.14/ref/net9.0')
csc = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data/DotNetSdkRoslyn/csc.dll'
response = out / 'compile.rsp'
response.write_text('\n'.join(['-nologo', '-target:exe', '-langversion:9.0', '-nostdlib+', '-out:"' + str(out / 'Verification.dll') + '"'] + ['-r:"' + str(p) + '"' for p in refs.glob('*.dll')] + ['"' + str(out / 'Program.cs') + '"']), encoding='utf-8')
compile_result = subprocess.run(['dotnet', csc, '@' + str(response)], capture_output=True, text=True)
(out / 'compile.txt').write_text(compile_result.stdout + compile_result.stderr, encoding='utf-8')
report = {'kind': 'Exact production method bodies with minimal managed scene-entry/BuildResult substitutes; not Unity API/Configure/Play execution', 'compile_exit_code': compile_result.returncode}
if compile_result.returncode == 0:
    (out / 'Verification.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net9.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': '9.0.14'}}}), encoding='utf-8')
    result = subprocess.run(['dotnet', str(out / 'Verification.dll')], capture_output=True, text=True)
    output = result.stdout + result.stderr
    (out / 'execution.txt').write_text(output, encoding='utf-8')
    report.update({'execution_exit_code': result.returncode, 'output': output, 'pass': result.returncode == 0})
else:
    report.update({'diagnostics': compile_result.stdout + compile_result.stderr, 'pass': False})
(root / 'docs/release-integration/verification-logic-results.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
raise SystemExit(0 if report['pass'] else 1)
