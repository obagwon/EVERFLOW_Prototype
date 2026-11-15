using UnityEngine;
namespace EverflowMap
{
    // Reads the existing motor. Animation never moves the controller, camera or puzzle objects.
    [DefaultExecutionOrder(450),DisallowMultipleComponent]
    public sealed class EFV2CharacterAnimator:MonoBehaviour
    {
        public EFMapPlayer player;
        public EFV2PuzzleWorld world;
        public Animator animator;
        public string CurrentMotion {get;private set;}="Locomotion";
        public float MotionSpeed=>animator!=null?animator.GetFloat(Speed):0;
        static readonly int Speed=Animator.StringToHash("Speed");
        float airTime,landingTime;
        int teleport=-1;
        bool wasGrounded=true;
        void Awake()
        {
            if(player==null)player=GetComponent<EFMapPlayer>();
            if(animator==null)animator=GetComponentInChildren<Animator>();
            animator.applyRootMotion=false;animator.updateMode=AnimatorUpdateMode.Normal;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        }
        void Switch(string state,float blend=.09f)
        {
            if(CurrentMotion==state)return;
            CurrentMotion=state;animator.CrossFadeInFixedTime(state,blend,0);
        }
        void ResetPose()
        {
            teleport=player.TeleportVersion;airTime=landingTime=0;wasGrounded=true;
            animator.Rebind();animator.SetFloat(Speed,0);animator.Play("Locomotion",0,0);animator.Update(0);
            CurrentMotion="Locomotion";
        }
        void Update()
        {
            if(player==null||animator==null)return;
            if(teleport!=player.TeleportVersion)ResetPose();
            if(world!=null&&world.SimulationPaused)return;
            float dt=Time.deltaTime;if(dt<=0)return;
            if(!player.enabled)
            {animator.SetFloat(Speed,0,.1f,dt);Switch("Locomotion");airTime=landingTime=0;wasGrounded=true;return;}
            bool grounded=player.Grounded;float speed=player.LocomotionSpeed;
            // Stop feet promptly when the motor stops; retain a softer blend while accelerating.
            animator.SetFloat(Speed,speed,speed<.05f?.045f:.09f,dt);
            if(!grounded)
            {
                airTime+=dt;
                if(player.VerticalSpeed>.2f)Switch("Jump",.06f);
                else if(airTime>.12f)Switch("Fall",.1f);
                landingTime=0;
            }
            else
            {
                if(!wasGrounded&&airTime>.12f&&speed<.2f){landingTime=.65f;Switch("Land",.06f);}
                airTime=0;
                if(speed>.2f)landingTime=0;
                if(landingTime>0)landingTime-=dt;else Switch("Locomotion");
            }
            wasGrounded=grounded;
        }
    }
}
