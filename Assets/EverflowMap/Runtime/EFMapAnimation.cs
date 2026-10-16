using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace EverflowMap
{
    // The FBX take is 1..120 at its imported frameRate. Only its authored action
    // range is evaluated. A manual graph prevents autoplay, loops and root motion.
    public sealed class EFMapAnimation : MonoBehaviour
    {
        public AnimationClip clip;
        public int takeFirstFrame = 1;
        public int firstFrame = 1;
        public int lastFrame = 30;
        public bool Started { get; private set; }
        public bool Complete { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration => clip == null ? 0 : (lastFrame - firstFrame) / clip.frameRate;
        PlayableGraph graph;
        AnimationClipPlayable playable;
        bool ready;

        void Awake() { Initialize(); ResetPose(); }
        void Initialize()
        {
            if (ready || clip == null) return;
            ready = true;
            var animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create(name + " one shot");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetSpeed(0);
            AnimationPlayableOutput.Create(graph, "FBX", animator).SetSourcePlayable(playable);
            graph.Play();
        }
        public void Begin() { if (!Started && clip != null) { Started = true; Complete = false; } }
        public void ResetPose()
        {
            Initialize(); Started = Complete = false; Elapsed = 0; Sample(0);
        }
        public void Tick(float dt)
        {
            if (!Started || Complete || clip == null) return;
            Elapsed = Mathf.Min(Duration, Elapsed + dt);
            Sample(Elapsed);
            Complete = Elapsed >= Duration;
        }
        void Update() { Tick(Time.deltaTime); }
        void Sample(float seconds)
        {
            if (!graph.IsValid()) return;
            playable.SetTime(Mathf.Clamp((firstFrame - takeFirstFrame) / clip.frameRate + seconds, 0, clip.length));
            graph.Evaluate(0);
        }
        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
    }
}
