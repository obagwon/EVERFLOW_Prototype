"""Compile all current game/editor sources using installed Unity references."""
from pathlib import Path
import json
import re
import subprocess
import sys
sys.path.insert(0,str(Path(__file__).parent))
import verify_v2_exploration as compiler

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/v2-puzzle-validation'
TEMP=ROOT/'Temp/V2PuzzleCompile'

def main():
    OUT.mkdir(parents=True,exist_ok=True);TEMP.mkdir(parents=True,exist_ok=True)
    compiler.OUT=OUT;compiler.TEMP=TEMP
    runtime=compiler.compile_source('Assembly-CSharp')
    compiler.compile_source('Assembly-CSharp-Editor',runtime)
    compiler.compile_source('Assembly-CSharp',player=True)
    (OUT/'compile-result.json').write_text(json.dumps({'runtime_editor':True,'editor':True,'runtime_player':True,
        'method':'Installed Unity 6000.3.21f1 Roslyn, cached Unity/project package references; all EFV2 source files included.',
        'unity_import':False,'unity_play':False},indent=2),encoding='utf-8')

if __name__=='__main__':main()
