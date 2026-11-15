using System;
using System.Collections.Generic;
using UnityEngine;
using EverflowPuzzleLab;

namespace EverflowMap
{
    // Observes completed state changes; never applies abilities, moves devices or owns input.
    [DisallowMultipleComponent,DefaultExecutionOrder(900)]
    public sealed class EFDemoFeedback:MonoBehaviour
    {
        public EFV2PuzzleWorld world;
        public Material particleMaterial,lineMaterial;
        [Range(0,1)] public float effectsVolume=.55f,ambientVolume=.18f;
        public bool hideWorldDebugLabels=true;
        public string Notice {get;private set;}="";
        public float NoticeUntil {get;private set;}
        public int EventCount {get;private set;}
        public event Action<string> Presented;
        public string D5Status=>world.upperIce.Frozen&&!world.bypass.Grown?"내부 물 동결 · 해동 필요":world.Supply?world.bypass.Grown?"우회 공급 연결됨":"정렬·고정 완료 · 물 공급 중":world.channelStructure.Frozen?world.Aligned?"정렬·고정 완료 · 공급 확인 중":"어긋난 채 고정됨 · 해제 후 재정렬":world.Aligned?"정렬됨 · 지금 구조물을 고정하세요":"수로 이동 중 · 연결 홈에 맞추세요";
        readonly Color[] palette={new Color(.57f,.88f,.39f),new Color(.47f,.83f,.96f),new Color(.46f,.69f,1)};
        struct Snapshot {public bool grown,frozen;public int direction;public Snapshot(LabTarget t){grown=t.Grown;frozen=t.Frozen;direction=t.Direction;}}
        readonly Dictionary<LabTarget,Snapshot> previous=new Dictionary<LabTarget,Snapshot>();
        ParticleSystem[] bursts;LineRenderer[] rings;float[] ringAge;Vector3[] ringCenters;Color[] ringColors;
        AudioSource[] voices,motion;AudioSource waterBed,wheelBed;
        AudioClip[] clips;TrailRenderer[] trails;
        LineRenderer socket;
        bool initialized,paused,supplied,restored,garden;
        float lastElapsed,d5ReadyAt,noticeCooldown;Vector3 lastPlayer;
        readonly float[] gateAmounts=new float[2];readonly bool[] latched=new bool[2];
        Transform root;
        public void Start()
        {
            if(initialized||world==null||!world.Built)return;
            root=new GameObject("Demo feedback - bounded runtime pool").transform;root.SetParent(transform,false);
            clips=new AudioClip[12];for(int i=0;i<clips.Length;i++)clips[i]=Tone(i);
            voices=new AudioSource[8];bursts=new ParticleSystem[8];rings=new LineRenderer[8];ringAge=new float[8];ringCenters=new Vector3[8];ringColors=new Color[8];
            for(int i=0;i<8;i++)
            {
                voices[i]=Source("Effect voice "+i,false);
                var go=new GameObject("Impact burst "+i);go.transform.SetParent(root,false);go.layer=2;
                var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var m=ps.main;m.loop=false;m.playOnAwake=false;m.maxParticles=24;m.startLifetime=.55f;m.startSpeed=new ParticleSystem.MinMaxCurve(.6f,1.3f);m.startSize=new ParticleSystem.MinMaxCurve(.04f,.09f);m.simulationSpace=ParticleSystemSimulationSpace.World;m.startColor=palette[0];
                var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.15f;
                var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.7f,0),new GradientAlphaKey(0,1)});fade.color=gradient;
                var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
                var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=particleMaterial;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                bursts[i]=ps;rings[i]=Line("Impact ring "+i);rings[i].enabled=false;ringAge[i]=9;
            }
            motion=new[]{Source("G stone movement",true),Source("D4 stone movement",true)};
            foreach(var s in motion)s.clip=clips[9];
            waterBed=Source("Nearby running water",true);waterBed.clip=clips[10];waterBed.volume=0;waterBed.Play();
            wheelBed=Source("Restored wheel",true);wheelBed.clip=clips[11];wheelBed.volume=0;wheelBed.Play();
            socket=Line("D5 alignment socket");socket.widthMultiplier=.045f;
            trails=new TrailRenderer[3];for(int i=0;i<3;i++)
            {
                var trail=world.orbs[i].gameObject.AddComponent<TrailRenderer>();trail.sharedMaterial=lineMaterial;trail.time=.23f;trail.startWidth=.075f;trail.endWidth=0;trail.minVertexDistance=.06f;
                trail.startColor=palette[i];trail.endColor=new Color(palette[i].r,palette[i].g,palette[i].b,0);trail.emitting=false;trails[i]=trail;
            }
            initialized=true;ResetObservation();
            // The existing HUD already translates goals, target state and flow choices.
            // Remove duplicate floating test annotations without touching their state updates.
            if(hideWorldDebugLabels)foreach(var text in FindObjectsByType<TextMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {if(text.gameObject.scene!=world.gameObject.scene)continue;var renderer=text.GetComponent<Renderer>();if(renderer!=null)renderer.enabled=false;}
        }
        LineRenderer Line(string name)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);go.layer=2;var line=go.AddComponent<LineRenderer>();line.sharedMaterial=lineMaterial;line.useWorldSpace=true;line.loop=true;line.positionCount=32;line.widthMultiplier=.035f;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return line;
        }
        AudioSource Source(string name,bool loop)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);var s=go.AddComponent<AudioSource>();s.playOnAwake=false;s.loop=loop;s.spatialBlend=1;s.minDistance=3;s.maxDistance=38;s.rolloffMode=AudioRolloffMode.Linear;s.dopplerLevel=0;return s;
        }
        void ResetObservation()
        {
            previous.Clear();foreach(var t in world.targets)previous[t]=new Snapshot(t);
            supplied=world.Supply;restored=world.Restored;garden=world.GardenReached;lastElapsed=world.Elapsed;lastPlayer=world.player.transform.position;
            for(int i=0;i<2;i++){var g=i==0?world.ruinGate:world.exitGate;gateAmounts[i]=g.OpenAmount;latched[i]=g.Latched;motion[i].Stop();}
            foreach(var s in voices)s.Stop();foreach(var ps in bursts)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            for(int i=0;i<rings.Length;i++){rings[i].enabled=false;ringAge[i]=9;}foreach(var trail in trails)trail.Clear();Notice="";NoticeUntil=0;d5ReadyAt=0;noticeCooldown=0;
            waterBed.volume=wheelBed.volume=0;waterBed.Stop();wheelBed.Stop();waterBed.Play();wheelBed.Play();
        }
        void LateUpdate()
        {
            if(!initialized){Start();return;}
            if(world.Elapsed<lastElapsed-.1f)ResetObservation();lastElapsed=world.Elapsed;
            paused=world.SimulationPaused;
            if(paused)return;
            if(Vector3.Distance(lastPlayer,world.player.transform.position)>8){foreach(var t in trails)t.Clear();foreach(var s in voices)s.Stop();}
            lastPlayer=world.player.transform.position;
            for(int i=0;i<3;i++)trails[i].emitting=world.Slots[i].phase==EFV2OrbPhase.Flying;
            foreach(var t in world.targets)
            {
                if(!previous.TryGetValue(t,out var old)){previous[t]=new Snapshot(t);continue;}
                if(t.Grown!=old.grown&&t.Grown)Impact(t.transform.position,0,"성장 완료",0);
                else if(t.Frozen!=old.frozen)Impact(t.transform.position,1,t==world.channelStructure?D5Status:t.Frozen?(t.water?"물 동결 · 물길 차단":"구조물 고정"):t.water?"해동 · 물 흐름 재개":"고정 해제",t.Frozen?1:3);
                else if(t.Direction!=old.direction&&t.Direction>=0)Impact(t.transform.position,2,"흐름 전환 · "+t.directions[t.Direction].Split('/')[0].Trim(),2);
                previous[t]=new Snapshot(t);
            }
            for(int i=0;i<2;i++)
            {
                var gate=i==0?world.ruinGate:world.exitGate;bool moving=Mathf.Abs(gate.OpenAmount-gateAmounts[i])>.00001f;
                motion[i].transform.position=gate.door.position;motion[i].volume=effectsVolume*.18f;
                if(moving&&!motion[i].isPlaying)motion[i].Play();else if(!moving&&motion[i].isPlaying)motion[i].Stop();
                if(gate.Latched&&!latched[i])Impact(gate.door.position,2,i==0?"유적 석문 개방 · 지하로 이동하세요":"출구 수문 개방 · 주 출구로 물을 보내세요",5);
                gateAmounts[i]=gate.OpenAmount;latched[i]=gate.Latched;
            }
            if(world.Supply&&!supplied)Impact(world.connectionPoint.position,2,"물 공급 연결됨",6);supplied=world.Supply;
            if(world.Restored&&!restored)Impact(world.wheel.position,2,"물레방아 복원 · 정원으로 이동하세요",7,true);restored=world.Restored;
            if(world.GardenReached&&!garden)Impact(world.player.transform.position+Vector3.up,0,"정원 복원 완료 · 계속 탐험할 수 있습니다",8,true);garden=world.GardenReached;
            UpdateSocket();
            float dt=Time.deltaTime;
            for(int i=0;i<rings.Length;i++)
            {
                ringAge[i]+=dt;if(ringAge[i]>.65f){rings[i].enabled=false;continue;}
                DrawRing(rings[i],ringCenters[i],.2f+ringAge[i]*1.3f,ringColors[i],(1-ringAge[i]/.65f)*.65f);
            }
            var water=world.GetComponentInChildren<EFV2WaterIntegration>();if(water==null)water=FindFirstObjectByType<EFV2WaterIntegration>();
            Vector3 nearest=world.player.transform.position;float distance=999;
            if(water!=null)foreach(var s in water.segments)
            {
                if(!s.wet||s.frozen||s.suppressPresentation||s.speed<=0)continue;
                var p=s.renderer.bounds.ClosestPoint(world.player.transform.position);float d=Vector3.Distance(p,world.player.transform.position);
                if(d<distance){nearest=p;distance=d;}
            }
            waterBed.transform.position=nearest;waterBed.volume=Mathf.MoveTowards(waterBed.volume,ambientVolume*Mathf.Clamp01(1-distance/22),dt*.15f);
            wheelBed.transform.position=world.wheel.position;wheelBed.volume=Mathf.MoveTowards(wheelBed.volume,world.Restored?ambientVolume*.65f:0,dt*.15f);
        }
        void UpdateSocket()
        {
            Color c=world.upperIce.Frozen?palette[1]:world.Supply||world.Aligned?palette[0]:world.channelStructure.Frozen?new Color(1,.43f,.28f):new Color(.95f,.72f,.3f);
            DrawRing(socket,new Vector3(-55.6f,-5.48f,-68),.75f,c,.65f);
            if(world.Aligned&&!world.channelStructure.Frozen&&!world.Supply)
            {if(d5ReadyAt<=0){d5ReadyAt=Time.time;Announce("수로 정렬됨 · FREEZE로 구조물을 고정하세요");}}
            else d5ReadyAt=0;
        }
        static void DrawRing(LineRenderer ring,Vector3 p,float radius,Color color,float alpha)
        {
            ring.enabled=true;color.a=alpha;ring.startColor=ring.endColor=color;
            for(int j=0;j<32;j++){float a=j*Mathf.PI*2/32;ring.SetPosition(j,p+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
        }
        public void Impact(Vector3 point,int ability,string text,int sound,bool milestone=false)
        {
            if(!initialized||world.SimulationPaused)return;EventCount++;int index=EventCount%8;
            var ps=bursts[index];ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.transform.position=point;
            var main=ps.main;main.startColor=palette[Mathf.Clamp(ability,0,2)];ps.Play();ps.Emit(milestone?24:12);
            ringAge[index]=0;ringCenters[index]=point;ringColors[index]=palette[Mathf.Clamp(ability,0,2)];
            var source=voices[index];source.Stop();source.transform.position=point;source.clip=clips[sound];source.spatialBlend=milestone?0:1;source.volume=effectsVolume*(milestone?.65f:.4f);source.Play();
            Announce(text);
            Presented?.Invoke(text);
        }
        void Announce(string text){if(Time.time<noticeCooldown)return;Notice=text;NoticeUntil=Time.time+2.3f;noticeCooldown=Time.time+.18f;}
        AudioClip Tone(int kind)
        {
            int rate=24000;float duration=kind>=9?4:kind==7||kind==8?1.1f:kind==5||kind==6?.55f:.23f;
            float[] samples=new float[(int)(rate*duration)];var random=new System.Random(91+kind);float filtered=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate,u=t/duration;filtered=Mathf.Lerp(filtered,(float)(random.NextDouble()*2-1),kind==10?.025f:.13f);
                float f=kind==0?520:kind==1?1050:kind==2?330:kind==3?720:kind==5?390:kind==6?523:190;
                if(kind==7||kind==8)f=new[]{523f,659f,784f}[(int)(t/.18f)%3];
                float tone=Mathf.Sin(2*Mathf.PI*(f*t+(kind==2?160:40)*t*t));
                float envelope=kind>=9?1:Mathf.Sin(Mathf.PI*Mathf.Clamp01(u))*Mathf.Exp(-u*2);
                float value=kind==9?filtered*.22f+Mathf.Sin(t*87)*.035f:kind==10?filtered*.65f:kind==11?filtered*.12f+Mathf.Sin(t*94)*.035f:tone*.22f+filtered*(kind==1?.13f:.05f);
                float edge=Mathf.Min(1,Mathf.Min(t,duration-t)/.015f);samples[i]=Mathf.Clamp(value*envelope*edge,-.4f,.4f);
            }
            var clip=AudioClip.Create("EVERFLOW feedback "+kind,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        void OnDisable(){if(root!=null)foreach(var s in root.GetComponentsInChildren<AudioSource>())s.Stop();}
        void OnEnable(){if(initialized)ResetObservation();}
        void OnDestroy(){if(clips!=null)foreach(var clip in clips)if(clip!=null)Destroy(clip);}
    }
}
