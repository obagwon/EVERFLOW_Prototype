using System;
using UnityEngine;
using Ability = EverflowGraybox.Ability;

namespace EverflowMap
{
    // Scene-local integration. This component is absent from the preserved map.
    [DefaultExecutionOrder(100)]
    public sealed class EFMvpFinale : MonoBehaviour
    {
        public enum RestorationState { Preparing, Restoring, Finished }
        public EFMapWorld world;
        public EFCanalExperiment grounding;
        public EFMapNode growth, machine, connection, gate, finalControl;
        public BoxCollider bloomRoute, nearDeck, frozenRoute, farDeck;
        public Transform nearCheckpoint, farCheckpoint;
        public Bounds recoveryBounds;
        public Camera reactionCamera;
        public Light restoredLight;
        public AudioSource feedback;
        public RestorationState State { get; private set; }
        public bool Crossed { get; private set; }
        public bool BridgeVisited { get; private set; }
        public float Elapsed { get; private set; }
        public string Phase { get; private set; } = "Prepare the waterway";
        public bool Prepared => growth.Done && machine.SourceOn && connection.Satisfied;
        bool nearSaved, wheelStarted;
        float flowElapsed, wheelAt;
        AudioClip chime;

        void Awake()
        {
            if(feedback!=null)
            {
                const int rate=22050;
                var samples=new float[rate/5];
                for(int i=0;i<samples.Length;i++)
                    samples[i]=Mathf.Sin(2*Mathf.PI*660*i/rate)*.12f*(1-i/(float)samples.Length);
                chime=AudioClip.Create("EVERFLOW restoration chime",samples.Length,1,rate,false);
                chime.SetData(samples,0);
            }
            ResetPreparation();
        }

        public bool CanUse(EFMapNode node, Ability ability, out string reason)
        {
            reason=null;
            if(node==finalControl) return CanPulse(ability,out reason);
            if(State!=RestorationState.Preparing) reason="Restoration is already running. Skip or restart from the screen.";
            else if(node==machine && !growth.Done) reason="BLOOM the approach roots first.";
            else if(node==connection && !machine.SourceOn) reason="PULSE the maintenance device first.";
            return reason==null;
        }
        public bool CanPulse(Ability ability,out string reason)
        {
            reason=null;
            if(ability!=Ability.Pulse) reason="Final control needs PULSE (3).";
            else if(State!=RestorationState.Preparing) reason="Restoration has already been accepted.";
            else if(world.StageIndex!=5 || world.Completed[5] || !world.GameInputActive) reason="Final control is not active.";
            else if(!Prepared) reason="Prepare BLOOM approach, PULSE maintenance and FREEZE connection first.";
            else if(!Crossed || !grounding.IsStandingOn(farDeck)) reason="Walk across the frozen connection onto the final deck before the last PULSE.";
            return reason==null;
        }
        public bool AcceptFinalPulse()
        {
            if(!CanPulse(Ability.Pulse,out var reason)) { world.Message=reason; return false; }
            State=RestorationState.Restoring;
            Elapsed=flowElapsed=0;
            Phase="Final control accepted";
            world.Message="Restoration started. Space or Skip completes the same final state.";
            Sound();
            world.BeginCinematic(reactionCamera);
            return true;
        }

        void LateUpdate()
        {
            if(world==null || world.StageIndex!=5) return;
            if(State!=RestorationState.Preparing)
            {
                if(State==RestorationState.Restoring && world.player.transform.position.y<farDeck.bounds.max.y-2)
                    world.player.Teleport(farCheckpoint.position);
                return;
            }
            bloomRoute.enabled=growth.Done;
            bloomRoute.GetComponent<Renderer>().enabled=growth.Done;
            frozenRoute.enabled=Prepared;
            frozenRoute.GetComponent<Renderer>().enabled=Prepared;
            if(!world.GameInputActive) return;
            if(growth.Done && machine.SourceOn && grounding.IsStandingOn(nearDeck) && !nearSaved)
            { nearSaved=true; world.SetCheckpoint(nearCheckpoint.position); }
            // Witness entry from the maintenance half of the connection.
            // Touching its far edge after a shortcut onto the final deck is
            // not a traversal. This broad half-route zone has no timing gate.
            var across=Vector3.ProjectOnPlane(farCheckpoint.position-nearCheckpoint.position,Vector3.up);
            float progress=Vector3.Dot(Vector3.ProjectOnPlane(world.player.transform.position-nearCheckpoint.position,Vector3.up),across)/Mathf.Max(.01f,across.sqrMagnitude);
            if(Prepared && nearSaved && progress<=.5f && grounding.IsStandingOn(frozenRoute)) BridgeVisited=true;
            if(Prepared && BridgeVisited && grounding.IsStandingOn(farDeck) && !Crossed)
            { Crossed=true; world.SetCheckpoint(farCheckpoint.position); world.Message="Connection crossed. Aim at the final crystal and PULSE (3)."; }
            if(nearSaved && recoveryBounds.Contains(world.player.transform.position) && world.player.transform.position.y<frozenRoute.bounds.max.y-2)
                world.Respawn();
        }

        // Called by EFMapWorld, its single owner of ending and input state.
        public void Tick(float dt)
        {
            if(State!=RestorationState.Restoring) return;
            if(EFMapInput.KeyDown(KeyCode.Space)) { CompleteRestoration(); return; }
            Elapsed+=Mathf.Max(0,dt);
            if(Elapsed>=.6f)
            { gate.enabled=true; Phase="Sluice opening"; }
            if(!gate.Done) return;
            flowElapsed+=Mathf.Max(0,dt);
            Phase="Waterway reconnecting";
            for(int i=0;i<world.finalFlow.Length;i++) world.finalFlow[i].SetActive(flowElapsed>=i*.35f);
            if(flowElapsed>=world.finalFlow.Length*.35f+.3f && !wheelStarted)
            {
                wheelStarted=true; wheelAt=Elapsed;
                world.StartMvpWheel();
                foreach(var action in world.stages[5].restoration) action.Begin();
                Sound();
            }
            if(wheelStarted)
            {
                Phase="Vegetation and light returning";
                if(restoredLight!=null) restoredLight.intensity=Mathf.Lerp(0,3,(Elapsed-wheelAt)/2);
                if(Elapsed-wheelAt>=4) CompleteRestoration();
            }
        }
        public void CompleteRestoration()
        {
            if(State!=RestorationState.Restoring) return;
            // Normal and every skip time converge on these exact assignments.
            foreach(var action in gate.actions) { action.Begin(); action.Tick(action.Duration); }
            gate.Tick(0); gate.enabled=true;
            foreach(var flow in world.finalFlow) flow.SetActive(true);
            foreach(var action in world.stages[5].restoration) { action.Begin(); action.Tick(action.Duration); }
            if(restoredLight!=null) restoredLight.intensity=3;
            State=RestorationState.Finished;
            Phase="EVERFLOW restored";
            world.FinishMvpEnding();
            Sound();
        }
        public void ResetPreparation()
        {
            State=RestorationState.Preparing; Crossed=BridgeVisited=nearSaved=wheelStarted=false;
            Elapsed=flowElapsed=wheelAt=0; Phase="Prepare the waterway";
            if(gate!=null) gate.enabled=false;
            if(bloomRoute!=null) { bloomRoute.enabled=false; bloomRoute.GetComponent<Renderer>().enabled=false; }
            if(frozenRoute!=null) { frozenRoute.enabled=false; frozenRoute.GetComponent<Renderer>().enabled=false; }
            if(restoredLight!=null) restoredLight.intensity=0;
        }
        void Sound() { if(feedback!=null && chime!=null) feedback.PlayOneShot(chime); }
        void OnDestroy() { if(chime!=null) Destroy(chime); }
        void OnGUI()
        {
            if(State!=RestorationState.Restoring || world.Paused) return;
            GUI.Box(new Rect(Screen.width/2-200,Screen.height-115,400,100),Phase);
            if(GUI.Button(new Rect(Screen.width/2-185,Screen.height-78,180,45),"Skip (Space)")) CompleteRestoration();
            if(GUI.Button(new Rect(Screen.width/2+5,Screen.height-78,180,45),"New game")) world.RestartWorld();
        }
    }
}
