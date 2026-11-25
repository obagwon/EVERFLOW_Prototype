using System;
using UnityEngine;

namespace EverflowMap
{
    // 지형의 '전'(메마름) / '후'(살아남) 두 상태를 저장하는 파일.
    // 칠(텍스처 레이어), 풀·꽃(디테일), 나무를 각각 저장해요. 편집 메뉴: Tools > EVERFLOW > 복원 …
    [CreateAssetMenu(menuName = "EVERFLOW/Terrain States")]
    public sealed class EFTerrainStates : ScriptableObject
    {
        [Serializable]
        public struct Tree
        {
            public Vector3 position;     // 0~1 (지형 기준)
            public int proto;
            public float width, height, rotation;
        }

        [Serializable]
        public sealed class State
        {
            public int alphaRes, layers;
            public Texture2D[] splat = new Texture2D[0];      // 레이어 4개씩 RGBA
            public int detailRes;
            public Texture2D[] detail = new Texture2D[0];     // 디테일 레이어 하나씩 (R = 밀도 0~255)
            public Tree[] trees = new Tree[0];
            public bool Valid => splat != null && splat.Length > 0;
        }

        public State before = new State();
        public State after = new State();

        // ---------------------------------------------------------------- 읽기 (런타임·편집 둘 다)
        public static float[,,] ReadSplat(State s)
        {
            var a = new float[s.alphaRes, s.alphaRes, s.layers];
            for (int k = 0; k < s.splat.Length; k++)
            {
                var px = s.splat[k].GetPixels32();
                for (int y = 0; y < s.alphaRes; y++)
                    for (int x = 0; x < s.alphaRes; x++)
                    {
                        var c = px[y * s.alphaRes + x];
                        for (int ch = 0; ch < 4; ch++)
                        {
                            int l = k * 4 + ch;
                            if (l >= s.layers) break;
                            a[y, x, l] = (ch == 0 ? c.r : ch == 1 ? c.g : ch == 2 ? c.b : c.a) / 255f;
                        }
                    }
            }
            return a;
        }

        public static int[][,] ReadDetail(State s)
        {
            var r = new int[s.detail.Length][,];
            for (int l = 0; l < s.detail.Length; l++)
            {
                var d = new int[s.detailRes, s.detailRes];
                var px = s.detail[l].GetPixels32();
                for (int y = 0; y < s.detailRes; y++)
                    for (int x = 0; x < s.detailRes; x++)
                        d[y, x] = px[y * s.detailRes + x].r;
                r[l] = d;
            }
            return r;
        }
    }
}
