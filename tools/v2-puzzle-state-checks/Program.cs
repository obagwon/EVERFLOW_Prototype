using System;
using EverflowMap;
using EverflowPuzzleLab;

static class Program
{
    static int checks;
    static void Check(bool condition,string text)
    {
        if(!condition) throw new Exception("FAIL "+text);
        checks++;Console.WriteLine("PASS "+text);
    }
    static void Main()
    {
        Console.WriteLine("OFFLINE MANAGED STATE CHECKS. Exact production classes. No Unity scene, input, physics, rendering or Play.");
        foreach(LabAbility ability in Enum.GetValues<LabAbility>())
        {
            var slot=new EFV2OrbSlot<LabWorldState>();var a=new LabWorldState();var b=new LabWorldState();
            Check(!slot.Launch(null,0,false),ability+" invalid aim cannot consume an orb");
            Check(slot.Launch(a,1,false),ability+" starts ready and accepts first target");
            int first=slot.revision;
            Check(!slot.Launch(a,1,false)&&slot.revision==first,ability+" repeated clicks do not restart flight");
            Check(slot.Arrive(first),ability+" first flight arrives exactly once");a.Apply(ability,1);
            Check(!slot.Arrive(first),ability+" duplicate arrival rejected");
            Check(slot.Launch(b,0,false)&&ReferenceEquals(slot.target,b),ability+" retargets immediately without manual recall");
            Check(ability==LabAbility.Bloom?a.grown:ability==LabAbility.Freeze?a.frozen:a.direction==1,ability+" previous completed state remains on transfer");
            Check(!slot.Arrive(first),ability+" old revision cannot affect new target");
            Check(slot.Arrive(slot.revision),ability+" new target receives the same single orb");b.Apply(ability,0);
            Check(!slot.RecallIfFar(24,24),ability+" distance boundary is explicit");
            Check(slot.RecallIfFar(24.01f,24)&&slot.target==null&&slot.phase==EFV2OrbPhase.Returning,ability+" distance recall works independently of new casts");
            Check(ability==LabAbility.Bloom?a.grown&&b.grown:ability==LabAbility.Freeze?a.frozen&&b.frozen:a.direction==1&&b.direction==0,ability+" automatic distance recall preserves both results");
            Check(slot.Launch(a,0,false),ability+" reusable during return animation");
            slot.Returned();Check(slot.phase==EFV2OrbPhase.Flying,ability+" stale return completion cannot cancel reuse");
            int flight=slot.revision;slot.Recall();
            Check(!slot.Arrive(flight)&&slot.target==null,ability+" respawn cancels incomplete arrival without ghost ownership");
            slot.Returned();Check(slot.phase==EFV2OrbPhase.Ready,ability+" respawn leaves one reusable orb");
            Check(ability==LabAbility.Bloom?b.grown:ability==LabAbility.Freeze?b.frozen:b.direction==0,ability+" respawn preserves remote completed work");
        }
        var ice=new LabWorldState();ice.Apply(LabAbility.Freeze,0);ice.Undo(LabAbility.Freeze);
        Check(!ice.frozen,"explicit thaw clears ice without resetting other world state");
        var gate=new LabGateState();gate.Tick(1,true,false,3,8);
        Check(!gate.latched&&gate.open>0,"partial opening does not latch");gate.Tick(.2f,false,false,3,8);
        Check(gate.open<.375f,"partial unpowered door can close");gate.Tick(10,true,false,3,8);
        Check(gate.latched&&gate.open==1,"G wheel solution fully opens and latches");gate.Tick(10,false,false,3,8);
        Check(gate.latched&&gate.open==1,"G remains open after FLOW moves to another device");gate.Reset();gate.Tick(10,false,true,3,6.5f);
        Check(gate.latched,"D4 root alternative fully opens and latches");gate.Tick(10,false,false,3,6.5f);
        Check(gate.open==1,"D4 stays open after branch switches to main exit");
        int rows=0;
        for(int bits=0;bits<64;bits++) for(int branch=-1;branch<3;branch++)
        {
            bool aligned=(bits&1)!=0,structure=(bits&2)!=0,upper=(bits&4)!=0,bypass=(bits&8)!=0,open=(bits&16)!=0,iceBlocked=(bits&32)!=0;
            bool supply=EFV2CircuitRules.Supply(aligned,structure,upper,bypass);
            bool expected=((aligned&&structure&&!upper)||bypass)&&open&&!iceBlocked&&branch==2;
            if(LabCircuitRules.Arriving(supply,branch,open,iceBlocked)!=expected)throw new Exception("Circuit truth table row "+bits+" branch "+branch);
            rows++;
        }
        Check(rows==256,"all 256 structure alignment / fixed / upper ice / bypass / gate / main ice / branch combinations");
        Check(!EFV2CircuitRules.Supply(true,false,false,false),"D5 needs structure freeze after alignment");
        Check(EFV2CircuitRules.Supply(true,true,false,false),"D5 aligned and fixed route supplies water");
        Check(EFV2CircuitRules.Supply(false,false,true,true),"D5 bypass supplies water independently of broken or internally frozen upper channel");
        Check(!LabCircuitRules.Arriving(true,0,true,false),"dock is a leaf shortcut, never final supply");
        Check(!LabCircuitRules.Arriving(true,1,true,false),"small wheel branch opens gate but does not feed W");
        var arrival=new LabArrivalState();arrival.Tick(2.99f,true,3);
        Check(!arrival.success,"under three seconds cannot restore");arrival.Tick(.1f,false,3);
        Check(arrival.stableFor==0&&!arrival.success,"interrupted supply resets continuous timer");arrival.Tick(3,true,3);
        Check(arrival.success,"three continuous seconds restore");arrival.Tick(1,false,3);
        Check(arrival.success&&arrival.stableFor==0,"restoration stays complete while live supply can stop");arrival.Reset();
        Check(!arrival.success&&arrival.stableFor==0,"restart clears finale and timer");
        Console.WriteLine($"TOTAL {checks} checks + {rows} circuit combinations; 0 failures. NOT A PLAYTHROUGH.");
    }
}
