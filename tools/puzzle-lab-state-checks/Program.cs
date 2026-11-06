using System;
using EverflowPuzzleLab;

static class Program
{
    static int count;
    static void Check(bool value,string name) { if(!value) throw new Exception("FAIL "+name); count++; Console.WriteLine("PASS "+name); }
    static void Main()
    {
        Console.WriteLine("MANAGED STATE CHECKS: exact LabState.cs compiled outside Unity. No scene, aiming, physics, platform riding or Play validation.");
        var slots=new LabWorldState[3]; var a=new LabWorldState(); var b=new LabWorldState();
        foreach(LabAbility ability in Enum.GetValues<LabAbility>())
        {
            Check(LabOrbSlots.TryPlace(slots,ability,a),ability+" first placement"); a.Apply(ability,1);
            Check(!LabOrbSlots.TryPlace(slots,ability,b),ability+" second target rejected before recall");
            Check(LabOrbSlots.TryPlace(slots,ability,a),ability+" same target reconfiguration");
            slots[(int)ability]=null;
            Check(ability==LabAbility.Bloom?a.grown:ability==LabAbility.Freeze?a.frozen:a.direction==1, ability+" world change survives recall");
            Check(LabOrbSlots.TryPlace(slots,ability,b),ability+" reuses recalled orb"); b.Apply(ability,0);
            Check(ability==LabAbility.Bloom?a.grown&&b.grown:ability==LabAbility.Freeze?a.frozen&&b.frozen:a.direction==1&&b.direction==0,ability+" sequential world changes coexist");
            LabOrbSlots.Release(slots,b); Check(slots[(int)ability]==null,ability+" target disable/destroy release");
        }
        a.Undo(LabAbility.Freeze); Check(!a.frozen&&b.frozen,"explicit release only changes named target");
        a.Undo(LabAbility.Bloom); Check(!a.grown&&b.grown,"explicit shrink distinct from recall");
        slots[0]=a;slots[1]=b;LabOrbSlots.Release(slots,a);a.Reset();
        Check(slots[0]==null&&slots[1]==b&&b.grown&&b.frozen,"area reset releases matching slots and preserves another area");
        Check(!a.grown&&!a.frozen&&a.direction==-1,"reset restores world state");
        var gate=new LabGateState(); gate.Tick(1,true,false,1.8f,3.6f);
        Check(gate.open>.49f&&!gate.latched,"partial door is not latched"); gate.Tick(2,true,false,1.8f,3.6f);
        Check(gate.open==1&&gate.latched,"wheel fully opens and latches"); gate.Tick(10,false,false,1.8f,3.6f);
        Check(gate.open==1,"door remains after recall / direction change"); gate.Reset(); gate.Tick(3,false,true,1.8f,3.6f);
        Check(gate.latched,"root alternative latches door");
        int truth=0;
        for(int mask=0;mask<32;mask++) for(int direction=-1;direction<3;direction++)
        {
            bool aligned=(mask&1)!=0, upper=(mask&2)!=0, bypass=(mask&4)!=0, open=(mask&8)!=0, ice=(mask&16)!=0;
            bool supply=LabCircuitRules.Supply(aligned,upper,bypass);
            bool expected=((aligned&&!upper)||bypass)&&open&&!ice&&direction==2;
            if(LabCircuitRules.Arriving(supply,direction,open,ice)!=expected) throw new Exception("Circuit truth table mismatch"); truth++;
        }
        Check(truth==128,"128 unique circuit combinations: dock/turbine, closed gate, misalignment and main ice block W");
        Check(LabCircuitRules.Arriving(LabCircuitRules.Supply(true,false,false),2,true,false),"realignment/thaw/branch change restores supply");
        Check(LabCircuitRules.Arriving(LabCircuitRules.Supply(false,true,true),2,true,false),"bypass alternative independent of upper canal freeze");
        var arrival=new LabArrivalState(); arrival.Tick(2.9f,true,3);
        Check(!arrival.success,"under 3 seconds cannot restore"); arrival.Tick(.2f,false,3);
        Check(arrival.stableFor==0&&!arrival.success,"interrupted water resets continuous timer"); arrival.Tick(3,true,3);
        Check(arrival.success,"3 continuous seconds restore"); arrival.Tick(1,false,3);
        Check(arrival.success&&arrival.stableFor==0,"completed restoration retained; live flow still tracked separately"); arrival.Reset();
        Check(!arrival.success&&arrival.stableFor==0,"area reset clears final restoration"); arrival.Tick(1,false,0);
        Check(!arrival.success,"zero inspector threshold cannot succeed without water");
        Console.WriteLine($"TOTAL {count} checks; 0 failures");
    }
}
