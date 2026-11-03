"""Prepare opt-in core changes outside Assets while a P3 run owns the Editor.
No project source or scene is modified by this preparation command.
"""
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).resolve().parent


def replace_once(text, old, new):
    assert text.count(old)==1, old[:100]
    return text.replace(old,new,1)


def main():
    world=(ROOT/'Assets/EverflowMap/Runtime/EFMapWorld.cs').read_text(encoding='utf-8-sig')
    world=replace_once(world,'        public bool stabilizeCompletedStages;',
        '        public bool stabilizeCompletedStages;\n        public EFMvpFinale mvpFinale;')
    world=replace_once(world,'            sideWater.enabled = gate4.Done;\n            if (Ended) return;',
        '''            sideWater.enabled = gate4.Done;
            if(mvpFinale!=null)
            {
                mvpFinale.Tick(Time.deltaTime);
                if(WheelRunning) wheel.Rotate(wheelAxis,-25*Time.deltaTime,Space.Self);
                return;
            }
            if (Ended) return;''')
    world=replace_once(world,'if (Ended && WheelRunning) wheel.Rotate','if (Ended && WheelRunning && mvpFinale==null) wheel.Rotate')
    world=replace_once(world,'        public void BeginCinematic(Camera camera)',
        '''        public void StartMvpWheel() { if(mvpFinale!=null) WheelRunning=true; }
        public void FinishMvpEnding()
        {
            if(mvpFinale==null) return;
            WheelRunning=true; Completed[5]=true; Ended=true; endingShown=true;
            Message="EVERFLOW restored. Restart world for a new game, or continue exploring.";
            BeginCinematic(endingCamera);
        }
        public void BeginCinematic(Camera camera)''')
    world=replace_once(world,'            if (Completed[StageIndex]) return;\n            foreach (var node',
        '''            if (Completed[StageIndex]) return;
            if(StageIndex==5 && mvpFinale!=null && mvpFinale.State!=EFMvpFinale.RestorationState.Preparing)
            { Message="Restoration is running; use Skip or New game."; return; }
            foreach (var node''')
    world=replace_once(world,'            foreach (var checkpoint in GetComponentsInChildren<EFMapCheckpoint>())',
        '''            if(StageIndex==5 && mvpFinale!=null) mvpFinale.ResetPreparation();
            foreach (var checkpoint in GetComponentsInChildren<EFMapCheckpoint>())''')
    node=(ROOT/'Assets/EverflowMap/Runtime/EFMapNode.cs').read_text(encoding='utf-8-sig')
    node=replace_once(node,'        public bool pulseAtRoot;', '        public bool pulseAtRoot;\n        public bool bidirectionalAlignment;')
    node=replace_once(node,'Mathf.Abs(Mathf.DeltaAngle(Angle, 0)) <= tolerance;',
        '''(Mathf.Abs(Mathf.DeltaAngle(Angle, 0)) <= tolerance ||
            kind==MapKind.Bridge && bidirectionalAlignment && Mathf.Abs(Mathf.DeltaAngle(Angle,180))<=tolerance);''')
    node=replace_once(node,'            else if (ability == Ability.Bloom &&',
        '''            else if(world.mvpFinale!=null && stage==5 && !world.mvpFinale.CanUse(this,ability,out reason)) { }
            else if (ability == Ability.Bloom &&''')
    node=replace_once(node,'            if (!CanApply(ability, out var reason)) return reason;',
        '''            if (!CanApply(ability, out var reason)) return reason;
            if(world.mvpFinale!=null && this==world.mvpFinale.finalControl && ability==Ability.Pulse &&
               !world.mvpFinale.AcceptFinalPulse()) return world.Message;''')
    (OUT/'EFMapWorld.cs').write_text(world,encoding='utf-8')
    (OUT/'EFMapNode.cs').write_text(node,encoding='utf-8')
    print('Prepared opt-in World/Node sources outside Assets; no scene integration.')


if __name__=='__main__':
    main()
