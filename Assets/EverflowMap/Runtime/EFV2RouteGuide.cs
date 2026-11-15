using System;
using System.Collections.Generic;
using UnityEngine;

namespace EverflowMap
{
    // Walkable connections in the V2 puzzle scene. This graph only drives the HUD.
    internal sealed class EFV2RouteGuide
    {
        internal enum Area { Start, Shore, Mound, Link, B, BFar, C, CHigh, Ruins, Tower, Tunnel, Balcony, LowerStairs, Basin, Bench, Service, ExitApproach, Exit, GardenWalk, Garden }

        [Flags]
        internal enum Access
        {
            Walk = 0, ARoot = 1, ALeaf = 2, BRoot = 4, BStone = 8, CLeaf = 16,
            RuinGate = 32, Vine = 64, Ice = 128, ReturnWalk = 256, ExitGate = 512
        }

        internal readonly struct Point
        {
            internal readonly Area Area;
            internal readonly string Name;
            internal readonly Vector3 Position;

            internal Point(Area area, string name, Vector3 position)
            {
                Area = area;
                Name = name;
                Position = position;
            }
        }

        readonly struct Connection
        {
            internal readonly int From, To;
            internal readonly Access Required;
            internal readonly float Length;
            internal readonly string Between;

            internal Connection(int from, int to, Access required, float length, string between)
            {
                From = from;
                To = to;
                Required = required;
                Length = length;
                Between = between;
            }

            internal bool Open(Access access) => (access & Required) == Required;
        }

        readonly List<Point> points = new List<Point>();
        readonly List<Connection> connections = new List<Connection>();
        readonly float[] distance;
        readonly int[] next;
        readonly bool[] visited;
        int cachedGoal = -1;
        Access cachedAccess;
        int localConnection = -1;
        float localFraction;

        internal readonly int Mound, Shore, BNear, BFar, CBoard, CHigh, Ruins, Gate, Balcony, Basin, Bench, Alignment, Bypass, Exit, Garden;

        internal EFV2RouteGuide()
        {
            int start = Add(Area.Start, "시작 전망대", -46, .2f, -6);
            Shore = Add(Area.Shore, "연못 물가", -46, .15f, -13.3f);
            int root = Add(Area.Shore, "뿌리 계단 아래", -40.4f, .05f, -15);
            int leaf = Add(Area.Shore, "연못 잎 탑승부", -49, .28f, -16.2f);
            Mound = Add(Area.Mound, "첫 둔덕", -45, 6.05f, -28);
            int rootTop = Add(Area.Mound, "뿌리 계단 위", -40.4f, 6.05f, -27);
            int leafTop = Add(Area.Mound, "연못 잎 착지부", -48.5f, 6.05f, -27.5f);
            Connect(Access.Walk, start, Shore, root);
            Connect(Access.Walk, Shore, leaf);
            Connect(Access.ARoot, "A · 둔덕행 뿌리 계단", root, rootTop);
            Connect(Access.ALeaf, "A · 연못 잎 이동 구간", leaf, leafTop);
            Connect(Access.Walk, rootTop, Mound, leafTop);

            int bTurn = Add(Area.Link, "B 입구 돌다리", -40.4f, 6.05f, -29);
            int bBridge = Add(Area.Link, "B 입구 돌다리", -36, 6, -33);
            int bApproach = Add(Area.Link, "B 쪽 연결길", -30.2f, 6, -37.7f);
            int bEntry = Add(Area.B, "뿌리 테라스 입구", -27, 6.1f, -40.5f);
            BNear = Add(Area.B, "뿌리 다리 앞", -26.4f, 6.08f, -41.5f);
            int bRootEnd = Add(Area.BFar, "뿌리 다리 건너편", -26.4f, 6.08f, -50.8f);
            int bStoneStart = Add(Area.B, "돌 발판 탑승부", -24, 6.08f, -42.2f);
            int bStoneEnd = Add(Area.BFar, "돌 발판 착지부", -24, 6.08f, -49.8f);
            BFar = Add(Area.BFar, "B 건너편 계단 입구", -25, 6.1f, -52);
            int bTop = Add(Area.Ruins, "B 계단 위", -34.5f, 15.15f, -58.8f);
            Ruins = Add(Area.Ruins, "고지 유적 마당", -35, 15.16f, -60.5f);
            Connect(Access.Walk, Mound, bTurn, bBridge, bApproach, bEntry, BNear);
            Connect(Access.Walk, bEntry, bStoneStart);
            Connect(Access.BRoot, "B · 뿌리 다리", BNear, bRootEnd);
            Connect(Access.BStone, "B · 돌 발판 이동 구간", bStoneStart, bStoneEnd);
            Connect(Access.Walk, bRootEnd, BFar);
            Connect(Access.Walk, "B · 유적행 계단", BFar, bTop);
            Connect(Access.Walk, bTop, Ruins);
            Connect(Access.Walk, bStoneEnd, BFar);

            int cTurn = Add(Area.Link, "C 입구 돌다리", -50.8f, 6.08f, -30.1f);
            int cBridge = Add(Area.Link, "C 쪽 연결길", -54.5f, 6, -34);
            int cApproach = Add(Area.Link, "C 쪽 연결길 끝", -57.5f, 6, -37.5f);
            int cEntry = Add(Area.C, "바람 정원 입구", -64, 6.12f, -40);
            CBoard = Add(Area.C, "큰 잎 탑승부", -64, 6.25f, -44.2f);
            int cLeafEnd = Add(Area.CHigh, "큰 잎 착지부", -62, 11.45f, -56.4f);
            CHigh = Add(Area.CHigh, "위쪽 선반", -62, 11.85f, -59);
            int cStairs = Add(Area.CHigh, "C 계단 입구", -61, 11.9f, -60);
            int cTop = Add(Area.Ruins, "C 계단 위", -57, 15.15f, -62);
            int cPaving = Add(Area.Ruins, "유적 서쪽 회랑", -53.8f, 15.15f, -63);
            int link = Add(Area.Link, "B ↔ C 연결길", -44, 6.1f, -40);
            Connect(Access.Walk, Mound, cTurn, cBridge, cApproach, cEntry, CBoard);
            Connect(Access.Walk, bEntry, link, cEntry);
            Connect(Access.CLeaf, "C · 큰 잎 이동 구간", CBoard, cLeafEnd);
            Connect(Access.Walk, cLeafEnd, CHigh, cStairs);
            Connect(Access.Walk, "C · 유적행 계단", cStairs, cTop);
            Connect(Access.Walk, cTop, cPaving, Ruins);

            Gate = Add(Area.Ruins, "G 석문 앞", -29.75f, 15.06f, -60.2f);
            int tower = Add(Area.Tower, "탑 계단 입구", -30.65f, 15.02f, -63.1f);
            Connect(Access.Walk, Ruins, Gate);
            Connect(Access.RuinGate, Gate, tower);
            for (int i = 0; i < 6; i++)
            {
                float x = i % 2 == 0 ? -30.65f : -28.85f;
                float z = i % 2 == 0 ? -68.9f : -63.1f;
                float y = 12.02f - 3 * i;
                int landing = Add(Area.Tower, $"탑 {6 - i}층 계단참", x, y, z);
                Connect(Access.Walk, tower, landing);
                tower = landing;
                if (i == 5) break;
                tower = Add(Area.Tower, $"탑 {6 - i}층 다음 계단", -59.5f - x, y, z);
                Connect(Access.Walk, landing, tower);
            }

            int tunnelStart = Add(Area.Tunnel, "터널 입구", -30.5f, -2.98f, -63.1f);
            int tunnelCorner = Add(Area.Tunnel, "터널 모퉁이", -30.5f, -3, -37);
            int tunnelEnd = Add(Area.Tunnel, "발코니 입구", -44, -3, -37);
            int balconyEntry = Add(Area.Balcony, "D1 발코니 입구", -46, -3, -38);
            Balcony = Add(Area.Balcony, "D1 발코니", -48, -2.97f, -38.7f);
            int lowerTop = Add(Area.LowerStairs, "수조행 계단 입구", -59.5f, -3, -40);
            int lowerMid = Add(Area.LowerStairs, "수조행 계단참", -59.5f, -7.4f, -47);
            int lowerBase = Add(Area.LowerStairs, "수조행 계단 아래", -59.5f, -11.5f, -53);
            Basin = Add(Area.Basin, "D2 중앙 분기대", -56, -11.1f, -52);
            Connect(Access.Walk, tower, tunnelStart, tunnelCorner, tunnelEnd, balconyEntry, Balcony, lowerTop, lowerMid, lowerBase, Basin);

            int vineTop = Add(Area.Balcony, "발코니 덩굴 앞", -40, -3, -37.5f);
            int vineBase = Add(Area.Bench, "덩굴 착지부", -39, -7.45f, -45.2f);
            Bench = Add(Area.Bench, "D3 작업대", -40, -7.46f, -57);
            int iceBank = Add(Area.Basin, "수조 얼음길 입구", -49, -11.57f, -52);
            int dock = Add(Area.Basin, "정비 부두", -43.8f, -11.4f, -52);
            int dockStairs = Add(Area.Basin, "정비 부두 계단 입구", -43.8f, -11.4f, -54);
            int dockTop = Add(Area.Bench, "작업대 계단 위", -43.8f, -7.5f, -59.9f);
            int returnTop = Add(Area.Bench, "작업대 복귀길 입구", -45.15f, -7.45f, -59.2f);
            int returnBase = Add(Area.Basin, "복귀길 아래", -50, -11.5f, -59.2f);
            int returnTurn = Add(Area.Basin, "복귀길 모퉁이", -56, -11.5f, -59.2f);
            Connect(Access.Walk, tunnelEnd, vineTop);
            Connect(Access.Vine, "D1 → D3 · 발코니 덩굴", vineTop, vineBase);
            Connect(Access.Walk, vineBase, Bench);
            Connect(Access.Walk, Basin, iceBank);
            Connect(Access.Ice, iceBank, dock);
            Connect(Access.Walk, dock, dockStairs, dockTop, Bench);
            Connect(Access.ReturnWalk, Bench, returnTop, returnBase, returnTurn, Basin);

            Alignment = Add(Area.Bench, "가동 수로 조준 위치", -40, -7.45f, -61.7f);
            int serviceFoot = Add(Area.Bench, "D5 정비 계단 아래", EFV2UndergroundWorks.D5StairFoot);
            int serviceTop = Add(Area.Service, "D5 정비 통로 입구", EFV2UndergroundWorks.D5StairTop);
            int serviceTurn = Add(Area.Service, "정비 통로 모퉁이", EFV2UndergroundWorks.D5GalleryTurn);
            int serviceMid = Add(Area.Service, "정비 통로", -56, -5.35f, -63.9f);
            int serviceWest = Add(Area.Service, "정비 통로 서쪽", -68, -5.95f, -64.5f);
            int bypassStairs = Add(Area.Service, "우회 수로 계단", -70.1f, -5.95f, -65);
            Bypass = Add(Area.Service, "우회 수로 뿌리 앞", -74, -8.9f, -64.8f);
            Connect(Access.Walk, Bench, Alignment, serviceFoot, serviceTop, serviceTurn, serviceMid, serviceWest, bypassStairs, Bypass);

            int exitTurn = Add(Area.ExitApproach, "수조 남쪽 회랑", -56, -11.5f, -55.5f);
            int exitWest = Add(Area.ExitApproach, "회랑 서쪽 모퉁이", -71, -11.5f, -55.5f);
            int exitNorth = Add(Area.Exit, "수문 쪽 회랑", -71, -11.5f, -46.5f);
            int exitLanding = Add(Area.Exit, "수문 조작 발판", -75.5f, -11.46f, -46.5f);
            Exit = Add(Area.Exit, "D4 출구 수문 앞", -75.5f, -11.46f, -47.5f);
            int exitOutside = Add(Area.GardenWalk, "수문 바깥", -79.5f, -11.9f, -48.1f);
            int gardenTop = Add(Area.GardenWalk, "정원행 내리막 입구", -82.3f, -11.9f, -47);
            int gardenBase = Add(Area.GardenWalk, "정원행 내리막 아래", -82.3f, -17.35f, -13.3f);
            int gardenTurn = Add(Area.GardenWalk, "정원행 회랑 모퉁이", -83.4f, -17.35f, -13.3f);
            int gardenWalk = Add(Area.Garden, "정원 앞 산책길", -83.4f, -17.35f, -2);
            Garden = Add(Area.Garden, "복원 정원 입구", -84, -17.28f, 0);
            Connect(Access.Walk, Basin, exitTurn, exitWest, exitNorth, exitLanding, Exit);
            Connect(Access.ExitGate, Exit, exitOutside);
            Connect(Access.Walk, exitOutside, gardenTop, gardenBase, gardenTurn);
            Connect(Access.Walk, "W · 정원행 회랑", gardenTurn, gardenWalk);
            Connect(Access.Walk, gardenWalk, Garden);

            distance = new float[points.Count];
            next = new int[points.Count];
            visited = new bool[points.Count];
        }

        int Add(Area area, string name, float x, float y, float z) => Add(area, name, new Vector3(x, y, z));

        int Add(Area area, string name, Vector3 point)
        {
            points.Add(new Point(area, name, point));
            return points.Count - 1;
        }

        void Connect(Access required, params int[] path) => Connect(required, null, path);

        void Connect(Access required, string between, params int[] path)
        {
            for (int i = 1; i < path.Length; i++)
            {
                int from = path[i - 1], to = path[i];
                connections.Add(new Connection(from, to, required, Vector3.Distance(points[from].Position, points[to].Position), between));
            }
        }

        static Vector3 FloorWeighted(Vector3 v) => new Vector3(v.x, v.y * 3, v.z);

        internal bool Locate(Vector3 feet, Access access, out Area area, out float floor, out string between)
        {
            area = default;
            floor = feet.y;
            between = null;
            localConnection = -1;
            float best = 36;
            for (int i = 0; i < connections.Count; i++)
            {
                var edge = connections[i];
                if (!edge.Open(access)) continue;
                Vector3 from = points[edge.From].Position, to = points[edge.To].Position;
                Vector3 delta = FloorWeighted(to - from);
                float fraction = Mathf.Clamp01(Vector3.Dot(FloorWeighted(feet - from), delta) / delta.sqrMagnitude);
                Vector3 onPath = Vector3.Lerp(from, to, fraction);
                float score = FloorWeighted(feet - onPath).sqrMagnitude;
                if (Mathf.Abs(feet.y - onPath.y) > 1.55f || score >= best) continue;
                best = score;
                localConnection = i;
                localFraction = fraction;
                floor = onPath.y;
                bool namedTransition = edge.Between != null;
                between = namedTransition && fraction > .12f && fraction < .88f ? edge.Between : null;
                area = points[fraction < (namedTransition ? .88f : .5f) ? edge.From : edge.To].Area;
            }
            return localConnection >= 0;
        }

        internal bool Next(int goal, Access access, Vector3 feet, out Point point)
        {
            point = default;
            if (goal < 0 || localConnection < 0) return false;
            if (goal != cachedGoal || access != cachedAccess) BuildDistances(goal, access);

            var edge = connections[localConnection];
            float fromCost = localFraction * edge.Length + distance[edge.From];
            float toCost = (1 - localFraction) * edge.Length + distance[edge.To];
            if (float.IsPositiveInfinity(Mathf.Min(fromCost, toCost))) return false;
            int target = fromCost <= toCost ? edge.From : edge.To;
            while (target >= 0 && (points[target].Position - feet).sqrMagnitude < .65f * .65f)
                target = next[target];
            if (target < 0) return false;
            point = points[target];
            return true;
        }

        void BuildDistances(int goal, Access access)
        {
            cachedGoal = goal;
            cachedAccess = access;
            Array.Fill(distance, float.PositiveInfinity);
            Array.Fill(next, -1);
            Array.Clear(visited, 0, visited.Length);
            distance[goal] = 0;
            for (int i = 0; i < points.Count; i++)
            {
                int nearest = -1;
                float best = float.PositiveInfinity;
                for (int j = 0; j < points.Count; j++)
                {
                    if (visited[j] || distance[j] >= best) continue;
                    nearest = j;
                    best = distance[j];
                }
                if (nearest < 0) break;
                visited[nearest] = true;
                foreach (var edge in connections)
                {
                    if (!edge.Open(access)) continue;
                    int other = edge.From == nearest ? edge.To : edge.To == nearest ? edge.From : -1;
                    if (other < 0 || best + edge.Length >= distance[other]) continue;
                    distance[other] = best + edge.Length;
                    next[other] = nearest;
                }
            }
        }
    }
}
