from pathlib import Path
import xml.etree.ElementTree as E
import subprocess
ns={'m':'http://schemas.microsoft.com/developer/msbuild/2003'}
root=Path.cwd()
out=root/'Temp/ui-progress'
csc=Path('C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data/DotNetSdkRoslyn/csc.dll')
results=[]
for mode in ('runtime-editor','editor','runtime-player'):
    editor=mode=='editor'
    proj=E.parse('Assembly-CSharp'+('-Editor' if editor else '')+'.csproj')
    refs=[x.text for x in proj.findall('.//m:Reference/m:HintPath',ns)]
    refs=[str((root/Path(x)).resolve()) if not Path(x).is_absolute() else x for x in refs]
    if editor: refs.append(str(out/'Assembly-CSharp.dll'))
    scripts=[p for p in Path('Assets').rglob('*.cs') if ('Editor' in p.parts)==editor]
    assembly='Assembly-CSharp'+('-Editor' if editor else '-Player' if mode=='runtime-player' else '')
    defines=proj.find('.//m:DefineConstants',ns).text
    if mode=='runtime-player': defines=';'.join(d for d in defines.split(';') if not d.startswith('UNITY_EDITOR'))
    lines=['-nologo','-target:library','-langversion:9.0','-nostdlib+','-out:"'+str(out/(assembly+'.dll'))+'"','-define:'+defines]
    lines+=['-r:"'+r+'"' for r in dict.fromkeys(refs)]
    lines+=['"'+str(p)+'"' for p in scripts]
    rsp=out/(assembly+'.rsp');rsp.write_text('\n'.join(lines),encoding='utf-8')
    result=subprocess.run(['dotnet',str(csc),'@'+str(rsp)],capture_output=True,text=True)
    (out/(assembly+'-compile.txt')).write_text(result.stdout+result.stderr,encoding='utf-8')
    results.append({'mode':mode,'scripts':len(scripts),'exit_code':result.returncode,'diagnostics':result.stdout+result.stderr})
    print(assembly, 'exit', result.returncode, 'scripts',len(scripts)); print(result.stdout+result.stderr)
    if result.returncode: break

import json
Path('docs/ui-progress/compile-results.json').write_text(json.dumps({'kind':'Roslyn compilation against installed Unity 6000.3.21f1/project DLLs; not Unity import or a player build','results':results},indent=2),encoding='utf-8')
raise SystemExit(0 if all(r['exit_code']==0 for r in results) else 1)
