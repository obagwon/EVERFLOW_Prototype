using System.Collections.Generic;
using UnityEngine;

namespace EverflowMap
{
    // 뿌리·덩굴 줄기를 점(points)을 따라 원통 메시로 그리는 스크립트. (Splines 패키지 필요 없음)
    //  - Grow 켜짐: 퍼즐 장치(Source)가 열리면 뿌리 끝에서부터 자라남. Source 비우면 오브젝트가 켜질 때 자람.
    //  - Grow 꺼짐: 길이는 그대로, 시든 굵기 → 원래 굵기로 힘이 생김 (B 기둥 덩굴).
    //  - Leaves: 줄기에 붙은 잎. 자랄 때는 그 지점까지 자라야 돋아나고, 시든 상태에선 작게.
    // 편집 화면에서는 다 자란 모습으로 보여요.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class EFVine : MonoBehaviour
    {
        [Tooltip("줄기가 지나는 점 (이 오브젝트 기준 좌표). 뿌리 시작 → 끝 순서")]
        public List<Vector3> points = new List<Vector3>();

        [Header("굵기")]
        public float radiusBase = .3f;
        public float radiusTip = .12f;
        [Tooltip("1 = 동그란 줄기. 1보다 작으면 위아래로 납작한 뿌리 (S 뿌리 길처럼 밟는 뿌리)")]
        [Range(.2f, 1)] public float flatness = 1;
        [Range(3, 24)] public int sides = 9;
        [Range(2, 16)] public int smooth = 6;

        [Header("퍼즐 연결")]
        public EF1123Motion source;
        [Tooltip("켜면 끝에서부터 자라요. 끄면 길이는 그대로 두고 굵기·잎만 살아나요")]
        public bool grow = true;
        [Tooltip("시든 상태의 굵기 비율 (Grow 꺼졌을 때)")]
        [Range(.2f, 1)] public float witheredThickness = 1;
        [Tooltip("시든 상태의 잎 크기 비율")]
        [Range(0, 1)] public float witheredLeaf = 1;
        [Min(.05f)] public float seconds = 1.2f;
        [Min(0)] public float delay;

        [Header("잎")]
        public Transform[] leaves = new Transform[0];
        [Tooltip("각 잎이 붙은 위치 (0 = 뿌리 시작, 1 = 끝)")]
        public float[] leafAt = new float[0];

        [Header("텍스처")]
        [Tooltip("텍스처 한 장이 덮는 줄기 길이 (m)")]
        public float uvLength = 2f;

        Mesh mesh;
        Vector3[] leafScale;
        float value, wait, applied = -1;

        void OnEnable()
        {
            if (mesh == null) { mesh = new Mesh { name = "EFVine", hideFlags = HideFlags.DontSave }; mesh.MarkDynamic(); }
            GetComponent<MeshFilter>().sharedMesh = mesh;
            if (leafScale == null || leafScale.Length != leaves.Length)
            {
                leafScale = new Vector3[leaves.Length];
                for (int i = 0; i < leaves.Length; i++) if (leaves[i] != null) leafScale[i] = leaves[i].localScale;
            }
            value = Application.isPlaying ? ((source != null && source.IsOpen) ? 1 : 0) : 1;
            wait = 0; applied = -1;
            Apply(value);
        }

        void OnDisable()
        {
            // 편집 중 잎 크기가 바뀐 채로 저장되지 않게
            if (leafScale != null)
                for (int i = 0; i < leaves.Length && i < leafScale.Length; i++)
                    if (leaves[i] != null) leaves[i].localScale = leafScale[i];
        }

        void OnValidate() { applied = -1; }

        // 편집 도구가 점·잎을 바꾼 뒤 부르는 함수
        public void Refresh()
        {
            leafScale = new Vector3[leaves.Length];
            for (int i = 0; i < leaves.Length; i++) if (leaves[i] != null) leafScale[i] = leaves[i].localScale;
            applied = -1;
            if (mesh != null) Apply(Application.isPlaying ? value : 1);
        }

        void Update()
        {
            if (!Application.isPlaying) { if (applied < 0) Apply(1); return; }
            bool open = source == null || source.IsOpen;
            if (open && wait < delay) { wait += Time.deltaTime; return; }
            if (!open) wait = 0;
            value = Mathf.MoveTowards(value, open ? 1 : 0, Time.deltaTime / seconds);
            Apply(value);
        }

        void Apply(float v)
        {
            float s = Mathf.SmoothStep(0, 1, v);
            if (Mathf.Approximately(s, applied)) return;
            applied = s;
            float length = grow ? s : 1;
            float thick = grow ? 1 : Mathf.Lerp(witheredThickness, 1, s);
            Build(length, thick);

            for (int i = 0; i < leaves.Length; i++)
            {
                if (leaves[i] == null || leafScale == null || i >= leafScale.Length) continue;
                float at = i < leafAt.Length ? leafAt[i] : 1;
                float pop = grow && length < .999f ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((length - at) / .12f)) : 1;
                float life = grow ? 1 : Mathf.Lerp(witheredLeaf, 1, s);
                leaves[i].localScale = leafScale[i] * (pop * life);
            }
        }

        // ------------------------------------------------------------ 메시
        static readonly List<Vector3> P = new List<Vector3>();
        static readonly List<float> D = new List<float>();
        static readonly List<Vector3> V = new List<Vector3>();
        static readonly List<Vector2> U = new List<Vector2>();
        static readonly List<int> T = new List<int>();

        void Build(float length, float thick)
        {
            mesh.Clear();
            if (points == null || points.Count < 2 || length <= .001f) return;

            // 부드러운 곡선 (Catmull-Rom)
            P.Clear();
            int n = points.Count;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, n - 1)];
                for (int k = 0; k < smooth; k++)
                {
                    float t = k / (float)smooth;
                    P.Add(.5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (3 * p1 - p0 - 3 * p2 + p3) * t * t * t));
                }
            }
            P.Add(points[n - 1]);

            D.Clear(); D.Add(0);
            for (int i = 1; i < P.Count; i++) D.Add(D[i - 1] + Vector3.Distance(P[i - 1], P[i]));
            float total = D[D.Count - 1];
            if (total <= .0001f) return;
            float cut = total * length;

            V.Clear(); U.Clear(); T.Clear();
            Vector3 side = Vector3.zero;
            int rings = 0;
            for (int i = 0; i < P.Count; i++)
            {
                bool last = D[i] >= cut || i == P.Count - 1;
                Vector3 c = P[i];
                float dist = D[i];
                if (D[i] > cut && i > 0)
                {
                    float f = Mathf.InverseLerp(D[i - 1], D[i], cut);
                    c = Vector3.Lerp(P[i - 1], P[i], f); dist = cut;
                }
                Vector3 tan = (P[Mathf.Min(i + 1, P.Count - 1)] - P[Mathf.Max(i - 1, 0)]).normalized;
                if (tan == Vector3.zero) tan = Vector3.forward;

                // 단면 방향: 납작한 뿌리는 항상 위가 위쪽, 둥근 줄기는 비틀림 없이 이어감
                if (flatness < .999f || side == Vector3.zero)
                {
                    side = Vector3.Cross(Vector3.up, tan);
                    if (side.sqrMagnitude < .01f) side = Vector3.Cross(Vector3.forward, tan);
                }
                else side -= tan * Vector3.Dot(side, tan);
                side.Normalize();
                Vector3 up = Vector3.Cross(tan, side).normalized;

                float k01 = dist / total;
                float r = Mathf.Lerp(radiusBase, radiusTip, k01) * thick;
                r *= Mathf.Lerp(.25f, 1, Mathf.Clamp01((cut - dist) / Mathf.Max(radiusBase * 3, .05f)));   // 자라는 끝은 뾰족하게
                float v = dist / Mathf.Max(uvLength, .01f);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2;
                    V.Add(c + side * (Mathf.Cos(a) * r) + up * (Mathf.Sin(a) * r * flatness));
                    U.Add(new Vector2(s / (float)sides, v));
                }
                rings++;
                if (last)
                {
                    V.Add(c + tan * r * .8f); U.Add(new Vector2(.5f, v));       // 끝 마개
                    break;
                }
            }
            int row = sides + 1;
            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < sides; s++)
                {
                    int a = r * row + s, b = a + row;
                    T.Add(a); T.Add(a + 1); T.Add(b);
                    T.Add(a + 1); T.Add(b + 1); T.Add(b);
                }
            int tip = V.Count - 1, lastRing = (rings - 1) * row;
            for (int s = 0; s < sides; s++) { T.Add(lastRing + s); T.Add(lastRing + s + 1); T.Add(tip); }

            mesh.SetVertices(V); mesh.SetUVs(0, U); mesh.SetTriangles(T, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }

        // 편집 도구용: 줄기 위 위치(0~1)의 점과 방향
        public void Sample(float at, out Vector3 pos, out Vector3 dir)
        {
            pos = points.Count > 0 ? points[0] : Vector3.zero; dir = Vector3.forward;
            if (points.Count < 2) return;
            float total = 0;
            for (int i = 1; i < points.Count; i++) total += Vector3.Distance(points[i - 1], points[i]);
            float want = total * Mathf.Clamp01(at), acc = 0;
            for (int i = 1; i < points.Count; i++)
            {
                float d = Vector3.Distance(points[i - 1], points[i]);
                if (acc + d >= want || i == points.Count - 1)
                {
                    pos = Vector3.Lerp(points[i - 1], points[i], d > 0 ? (want - acc) / d : 0);
                    dir = (points[i] - points[i - 1]).normalized;
                    return;
                }
                acc += d;
            }
        }
    }
}
