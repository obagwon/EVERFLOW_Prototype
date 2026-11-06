"""Offline compiler/state verification. Does not launch, automate or simulate Unity Play.

Uses this project's cached Unity response files and the installed matching Editor's
Roslyn compiler. Outputs go to ignored Temp; text evidence goes to docs/puzzle-lab.
Run from the project root: python tools/verify_v02_lab.py
"""
from pathlib import Path
import datetime
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs/puzzle-lab"
TEMP = ROOT / "Temp/PuzzleLabCompile"
UNITY = Path("C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data")


def compile_assembly(name, runtime_dll=None, player=False):
    candidates = sorted((ROOT / "Library/Bee/artifacts").glob(f"*/{name}.rsp"),
                        key=lambda p: p.stat().st_mtime, reverse=True)
    if not candidates:
        raise RuntimeError(f"No cached Unity response file for {name}; compile in Unity instead.")
    if not player:
        candidates = [p for p in candidates if re.search(r"^-define:UNITY_EDITOR$",p.read_text(encoding="utf-8-sig"),re.M)]
    if not candidates:
        raise RuntimeError(f"No cached Editor response file for {name}; compile in Unity instead.")
    lines = candidates[0].read_text(encoding="utf-8-sig").splitlines()
    lines = [line for line in lines if not re.match(r"[-/](out|refout|additionalfile|analyzer|analyzerconfig):", line, re.I)]
    if player:
        lines = [line for line in lines if not re.match(r"-define:UNITY_EDITOR(?:_WIN|_64)?$", line)]
    else:
        # The newest cached runtime response can be from a standalone build.
        lines += ["-define:UNITY_EDITOR", "-define:UNITY_EDITOR_WIN", "-define:UNITY_EDITOR_64"]
    if runtime_dll is not None:
        lines = [f'-r:"{runtime_dll.as_posix()}"' if re.match(r'-r:".*[/\\]Assembly-CSharp(?:\.ref)?\.dll"$', line) else line for line in lines]
    folder = "Editor" if name.endswith("Editor") else "Runtime"
    for source in sorted((ROOT / f"Assets/EverflowPuzzleLab/{folder}").glob("*.cs")):
        if not any(source.name in line and "EverflowPuzzleLab" in line for line in lines):
            lines.append(f'"{source.as_posix()}"')
    suffix = "-player" if player else ""
    target = TEMP / f"{name}{suffix}.dll"
    lines.append(f'-out:"{target.as_posix()}"')
    response = TEMP / f"{name}{suffix}.rsp"
    response.write_text("\n".join(lines) + "\n", encoding="utf-8")
    result = subprocess.run([str(UNITY / "NetCoreRuntime/dotnet.exe"), str(UNITY / "DotNetSdkRoslyn/csc.dll"), f"@{response}"],
                            cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=90)
    output = result.stdout + result.stderr
    (OUT / f"{name}{suffix}-compile.txt").write_text(
        f"Offline Roslyn compile against cached Unity 6000.3.21f1 references. Exit={result.returncode}\n"
        "This is not Unity Editor import, scene/reference validation or Play.\n" + output, encoding="utf-8")
    if result.returncode:
        raise RuntimeError(output)
    print(f"PASS offline compile {name}{suffix}")
    return target


def main():
    OUT.mkdir(exist_ok=True, parents=True)
    TEMP.mkdir(exist_ok=True, parents=True)
    runtime = compile_assembly("Assembly-CSharp")
    compile_assembly("Assembly-CSharp-Editor", runtime)
    compile_assembly("Assembly-CSharp", player=True)
    result = subprocess.run(["dotnet", "run", "--project", "tools/puzzle-lab-state-checks/StateChecks.csproj"],
                            cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=90)
    (OUT / "managed-state-checks.txt").write_text(result.stdout + result.stderr, encoding="utf-8")
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
    print(result.stdout.splitlines()[-1])
    summary = {
        "utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "offline_runtime_compile": "pass", "offline_editor_compile": "pass",
        "offline_player_define_compile": "pass", "managed_state_tests": "pass",
        "unity_scene_created": (ROOT / "Assets/Scenes/Everflow_V02_PuzzleLab.unity").is_file(),
        "unity_import_and_reference_validation": "not executed: Licensing Client IPC refused",
        "unity_play_physics_aim_riding": "not executed: Licensing Client IPC refused",
        "human_controls_visibility": "not verified",
    }
    (OUT / "verification.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
