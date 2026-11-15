using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EverflowMap
{
    // Scene-local stonework and water meshes. The puzzle world still owns the circuit.
    [DisallowMultipleComponent]
    public sealed class EFV2UndergroundWorks : MonoBehaviour
    {
        static Vector3 P(float x, float y, float z) => new Vector3(x, y, z);
        public static readonly Vector3 D5StairFoot = P(-40.2f,-7.45f,-63.35f);
        public static readonly Vector3 D5StairTop = P(-43.5f,-5.95f,-63.35f);
        public static readonly Vector3 D5GalleryTurn = P(-46,-5.95f,-63.9f);
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> materials = new List<Material>();
        Dictionary<string, Transform> model;
        Dictionary<string, Transform> supports;
        EFV2WaterIntegration water;
        Material wall, lining, coping, tread;
        GameObject intake, channelWater;

        public static EFV2UndergroundWorks Build(EFV2PuzzleWorld world, Transform runtime)
        {
            var root = new GameObject("Underground waterworks / forebay, cistern and dry galleries");
            root.transform.SetParent(runtime, false);
            var works = root.AddComponent<EFV2UndergroundWorks>();
            works.water = world.gameObject.scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<EFV2WaterIntegration>(true)).Single(w => w.world == world);
            works.model = world.mapRoot.GetComponentsInChildren<MeshFilter>(true).ToDictionary(f => f.name, f => f.transform);
            works.supports = runtime.GetComponentsInChildren<BoxCollider>(true)
                .Where(c => c.name != "Corridor guard").GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First().transform);
            works.Create(world);
            return works;
        }

        public void ShowAlignedIntake(bool aligned)
        {
            if (intake.activeSelf != aligned) intake.SetActive(aligned);
            if (channelWater.activeSelf != aligned) channelWater.SetActive(aligned);
        }

        Material Stone(string name, Color color, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            material.SetFloat("_Smoothness", smoothness);
            materials.Add(material);
            return material;
        }

        void Create(EFV2PuzzleWorld world)
        {
            wall = Stone("Waterworks / cool rock", new Color(.27f, .31f, .34f), .12f);
            lining = Stone("Waterworks / wet masonry", new Color(.29f, .39f, .39f), .27f);
            coping = Stone("Waterworks / pale coping", new Color(.58f, .58f, .48f), .16f);
            tread = Stone("Waterworks / dry flagstone", new Color(.43f, .46f, .44f), .13f);
            foreach (var pair in model)
            {
                string name = pair.Key;
                if (name.StartsWith("HALL_Wall") || name.StartsWith("ROOF_") || name == "HALL_Floor")
                    pair.Value.GetComponent<MeshRenderer>().sharedMaterial = wall;
                else if (name.StartsWith("UG_Pilaster") || name.StartsWith("CH_"))
                    pair.Value.GetComponent<MeshRenderer>().sharedMaterial = lining;
                else if (name.StartsWith("D1_Stairs_Step") || name.StartsWith("D3_DockStairs_Step") ||
                         name == "D3_Workbench" || name == "D3_Dock" || name == "D2_Hub")
                    pair.Value.GetComponent<MeshRenderer>().sharedMaterial = tread;
            }
            Ceiling();
            AlignSouthWindow();
            UpperWorks(world);
            Reservoir();
            Galleries();
        }

        void Ceiling()
        {
            // The enclosed hall ends before the side tunnel.
            Block("Hall sealed inner ceiling", P(-55.5f,1.355f,-53.5f), P(45.0f,.39f,35.0f), wall, solid:true);
            Block("Hall north-west corner joint", P(-77.90f,-5.42f,-70.89f), P(.46f,13.18f,.48f), lining);
            Block("Hall north-east corner joint", P(-33.10f,-5.42f,-70.89f), P(.46f,13.18f,.48f), lining);
            HideModel("FX_LightShaft01", false);
            HideModel("FX_LightShaft02", false);
            foreach(float z in new[]{-68.4f,-62.2f,-55.9f,-49.7f,-43.5f,-37.4f})
            {
                Block("Hall transverse stone rib", P(-55.5f,1.03f,z), P(44.85f,.37f,.55f), lining);
                Block("Hall transverse rib key", P(-55.5f,.816f,z), P(2.7f,.11f,.68f), coping);
            }
        }

        void AlignSouthWindow()
        {
            var frame = model["HALL_SouthWindowArch"];
            // Center the original frame in the wall with its faces clear of both wall surfaces.
            // Narrow the inner bevels enough to sit inside the four-meter opening.
            frame.position += Vector3.forward * .10f;
            frame.localScale = Vector3.Scale(frame.localScale, P(.94f,1,1.52f));
            model["UG_CaveRock03"].position += Vector3.back * 1.10f;
        }

        void UpperWorks(EFV2PuzzleWorld world)
        {
            var moving = world.channelPose.model;
            var feed = Segment("W_H_to_D5_Fall");
            var spring = new WaterPath(P(-46, 1.5f, -71.25f), 1.28f, .18f);
            spring.Drop(P(-46.92f, -5.06f, -70.08f), .84f);
            spring.Turn(P(-47, -5.07f, -69.66f), P(-46.92f, -5.12f, -69.24f), .66f);
            Replace(feed, spring);
            MoveImpact(feed, P(-46.92f, -5.06f, -70.08f));
            Block("Spring inlet lintel", P(-46.04f,1.01f,-70.91f), P(1.86f,.34f,.61f), lining);
            foreach (float x in new[] { -46.89f, -45.19f })
                Block("Spring inlet jamb", P(x,.61f,-70.96f), P(.24f,.56f,.47f), coping);

            // The forebay outlet clears the swept rim of the moving trough. Its water drops
            // into the original trough floor, which was above the old ground-level sheet.
            Block("Headbox footing", P(-46.92f, -5.74f, -70.05f), P(2.0f, .52f, 1.57f), lining);
            Block("Headbox floor", P(-46.92f, -5.40f, -69.95f), P(1.52f, .42f, 1.45f), lining);
            Block("Headbox north wall", P(-46.92f, -5.01f, -70.77f), P(1.82f, .46f, .18f), coping);
            Block("Headbox east wall", P(-46.12f, -5.01f, -70.11f), P(.18f, .46f, 1.46f), coping);
            Block("Headbox west wall / north", P(-47.72f, -5.01f, -70.58f), P(.18f, .46f, .52f), coping);
            Block("Headbox west wall / south", P(-47.72f, -5.01f, -69.63f), P(.18f, .46f, .49f), coping);
            Block("Headbox overflow sill", P(-47.72f, -5.25f, -70.12f), P(.22f, .22f, .41f), lining);
            Block("Elevated stone spout", P(-46.92f, -5.22f, -69.16f), P(.96f, .14f, .46f), lining);
            foreach (float x in new[] { -47.39f, -46.45f })
                Block("Spout cheek", P(x, -5.09f, -69.18f), P(.12f, .25f, .48f), coping);
            model["UG_Torch05"].position += Vector3.right * 1.8f;
            InstallD5Rails(world);

            var mouth = new WaterPath(P(-46.92f, -5.12f, -69.24f), .66f, .045f);
            mouth.Line(P(-46.92f, -5.135f, -68.94f), .63f);
            mouth.Drop(P(-46.93f, -5.563f, -68.02f), .58f);
            intake = NewWater("W_D5_IntakeMouth", transform, feed, mouth).renderer.gameObject;
            var inner = new WaterPath(P(4.46f, .443f, 0), 1.20f);
            inner.Line(P(-4.48f, .438f, 0), 1.20f);
            var trough = NewWater("W_D5_ChannelInterior", moving, feed, inner, true);
            trough.ice = EFV2WaterIce.D5Internal;
            channelWater = trough.renderer.gameObject;
            ShowAlignedIntake(false);

            HideModel("CH_D5_Fixed_Rim1", true);
            HideModel("CH_D5_Fixed_Rim2", true);
            var outlet = new WaterPath(P(-55.45f, -5.566f, -68), 1.17f);
            outlet.Turn(P(-56.11f, -5.66f, -68), P(-56, -5.82f, -67.31f), 1.30f);
            outlet.Line(P(-56, -5.85f, -64), 1.78f);
            Replace(Segment("W_D5_Fixed"), outlet);
            Block("D5 receiving bowl", P(-56.37f, -5.975f, -68.03f), P(1.61f, .17f, 1.46f), lining);
            Block("D5 tailrace invert", P(-56.1f, -5.945f, -66.01f), P(2.08f, .16f, 4.06f), lining);
            Block("D5 tailrace west wall", P(-57.08f, -5.75f, -66.23f), P(.21f, .40f, 4.30f), coping, solid:true);
            Block("D5 tailrace east wall", P(-55.05f, -5.75f, -65.39f), P(.21f, .40f, 2.79f), coping, solid:true);
            Block("D5 receiving corner", P(-56.35f, -5.75f, -68.79f), P(1.56f, .40f, .23f), coping);
            var grate = new WaterPath(P(-47.48f, -5.10f, -70.10f), .34f, .045f);
            grate.Line(P(-47.80f, -5.125f, -70.10f), .36f);
            grate.Drop(P(-48.19f, -5.96f, -70.12f), .30f);
            var overflow = NewWater("W_D5_HeadboxOverflow", transform, feed, grate);
            world.leakWater = world.leakWater.Append(overflow.renderer.gameObject).ToArray();
            Block("Headbox overflow drain", P(-48.24f, -5.99f, -70.12f), P(.70f, .10f, .69f), lining);
            for (int i = 0; i < 4; i++)
                Block("Overflow drain grate", P(-48.48f + i * .14f, -5.92f, -70.12f), P(.05f, .08f, .61f), coping);

            // The overflow reappears from a culvert below the gallery. The last reach
            // uses the existing lower creek's level and meeting point at the cistern.
            var spill = Segment("W_D5_SpillPool");
            var drain = new WaterPath(P(-49, -6.24f, -63.75f), .94f, .13f);
            drain.Drop(P(-49, -11.85f, -62.90f), 1.43f);
            drain.Line(P(-49, -11.85f, -59), 1.80f);
            Replace(spill, drain);
            MoveImpact(spill, P(-49, -11.85f, -62.90f));
            Block("Culvert lintel", P(-49, -6.12f, -63.98f), P(1.46f, .22f, .42f), coping);
            foreach (float x in new[] { -49.63f, -48.37f })
                Block("Culvert jamb", P(x, -6.35f, -63.99f), P(.20f, .60f, .42f), lining);
        }

        void InstallD5Rails(EFV2PuzzleWorld world)
        {
            var mechanism=world.channelPose;
            var mover=world.channel;
            var metal=Stone("Waterworks / aged rail metal",new Color(.38f,.34f,.22f),.42f);
            foreach(float x in new[]{-2.85f,2.85f})
            foreach(float z in new[]{-1.16f,1.16f})
            {
                var mount=P(x,.095f,z);
                Vector3 home=mover.home+mechanism.initial*mount;
                var bogie=new GameObject("D5 moving guide shoe");
                bogie.transform.SetParent(mechanism.model,false);
                bogie.transform.localPosition=mount+Vector3.up*.105f;
                var shoe=GameObject.CreatePrimitive(PrimitiveType.Cube);
                shoe.name="D5 bronze guide shoe";
                shoe.transform.SetParent(bogie.transform,false);
                shoe.transform.localScale=P(.38f,.21f,.31f);
                shoe.GetComponent<MeshRenderer>().sharedMaterial=metal;
                shoe.GetComponent<Collider>().enabled=false;
                Block("D5 shared rail footing",home-Vector3.up*.065f,P(.35f,.14f,.31f),lining);
                for(int branch=0;branch<mover.destinations.Length;branch++)
                {
                    var previous=home;
                    for(int i=1;i<=8;i++)
                    {
                        float t=i/8f;
                        Vector3 next=Vector3.Lerp(mover.home,mover.destinations[branch],t)+
                            Quaternion.Slerp(mechanism.initial,mechanism.DestinationRotation(branch),t)*mount;
                        Vector3 line=next-previous;
                        Block("D5 curved guide rail",(next+previous)*.5f,P(.105f,.072f,line.magnitude+.013f),metal,Quaternion.LookRotation(line));
                        previous=next;
                    }
                    Block("D5 rail end stop",previous+Vector3.up*.025f,P(.18f,.11f,.18f),coping);
                }
            }
        }

        void Reservoir()
        {
            const float y = -11.85f;
            Vector3[] bank = { P(-64.48f,y,-59), P(-47.52f,y,-59), P(-47,y,-58.48f), P(-47,y,-47.52f),
                P(-47.52f,y,-47), P(-64.48f,y,-47), P(-65,y,-47.52f), P(-65,y,-58.48f) };
            var basin = Segment("W_D2_Basin");
            var perimeter = new List<Vector3>();
            for (int edge = 0; edge < bank.Length; edge++)
            {
                var a = bank[edge];var b = bank[(edge + 1) % bank.Length];
                int count = Mathf.CeilToInt(Vector3.Distance(a, b) / .60f);
                for (int i = 0; i < count; i++) perimeter.Add(Vector3.Lerp(a, b, i / (float)count));
            }
            Vector3 center = P(-56,y,-53);
            var vertices = new List<Vector3>{basin.filter.transform.InverseTransformPoint(center)};
            var uv = new List<Vector2>{new Vector2(center.x, center.z)};
            var colors = new List<Color>{new Color(0,0,1,1)};
            var tris = new List<int>();
            const int rings = 16;
            int span = perimeter.Count;
            for (int ring = 1; ring <= rings; ring++)
            for (int i = 0; i < span; i++)
            {
                var point = Vector3.Lerp(center, perimeter[i], ring / (float)rings);
                vertices.Add(basin.filter.transform.InverseTransformPoint(point));
                uv.Add(new Vector2(point.x, point.z));
                colors.Add(new Color(0, 0, Mathf.Clamp01((rings - ring) * .65f), 1));
                int a = (ring - 1) * span + i + 1;
                int b = (ring - 1) * span + (i + 1) % span + 1;
                if (ring == 1) tris.AddRange(new[] { 0, b, a });
                else tris.AddRange(new[] { a - span, b - span, a, b - span, b, a });
            }
            SetMesh(basin, MakeMesh("Cistern / clipped stone shoreline", vertices, tris, uv, colors));

            // Segment the retaining edge at the three outlets and the dock crossing.
            Kerb(P(-64.47f,y,-47),P(-47.53f,y,-47), false);
            Kerb(P(-47,y,-58.48f),P(-47,y,-53.25f), false);
            Kerb(P(-47,y,-50.75f),P(-47,y,-47.52f), false);
            Kerb(P(-65,y,-56.38f),P(-65,y,-54.12f), false);
            Kerb(P(-65,y,-51.36f),P(-65,y,-50.12f), false);
            Kerb(P(-64.0f,y,-59),P(-57.05f,y,-59), false);
            Kerb(P(-54.95f,y,-59),P(-50.02f,y,-59), false);
            Kerb(P(-47.98f,y,-59),P(-47.52f,y,-59), false);
            for (int edge = 1; edge < bank.Length; edge += 2)
                Kerb(bank[edge], bank[(edge + 1) % bank.Length], false);

            Block("D2 turbine plinth", P(-73.21f,-12.07f,-52.76f), P(1.62f,.40f,2.54f), lining);
            HideModel("CH_Exit_Tunnel_Rim1", true);
            HideModel("CH_Exit_Tunnel_Rim2", true);
            Kerb(P(-78.05f,y,-50.04f),P(-81.10f,y,-50.04f), false);
            Kerb(P(-78.05f,y,-46.95f),P(-81.10f,y,-46.95f), false);
            supports["D4 threshold - follows opened gate passage"].position += Vector3.up * .14f;
            foreach (float z in new[] { -50.70f, -46.26f })
            {
                Block("D4 portal jamb", P(-77.65f,-8.36f,z), P(.78f,7.02f,.62f), lining);
                Block("D4 portal jamb cap", P(-77.65f,-5.03f,z), P(.98f,.29f,.88f), coping);
            }
            Block("D4 elevated lintel", P(-77.65f,-4.69f,-48.48f), P(.90f,.49f,5.08f), coping);
        }

        void Galleries()
        {
            var side = new[] { "D5 west service walkway", "D5 service bridge west ramp", "D5 dry channel crossing",
                "D5 service bridge east ramp", "D5 east service walkway" };
            foreach (string name in side) HideSupport(name, true);
            Slab("D5 joined service gallery", new[] { P(-70.50f,-5.95f,-63.9f), P(-58.6f,-5.95f,-63.9f),
                P(-57.6f,-5.35f,-63.9f), P(-54.4f,-5.35f,-63.9f), P(-53.4f,-5.95f,-63.9f), D5GalleryTurn, D5StairTop }, 2.4f, .36f, solid:true);
            var balustrade = model["D3_Balustrade_N"];
            var railBounds = balustrade.GetComponent<MeshFilter>().sharedMesh.bounds;
            // Keep the far end fixed and seat the near end against the new stair cheek.
            float railScale = (railBounds.max.z + 61.95f) / railBounds.size.z;
            balustrade.localScale = P(1,1,railScale);
            balustrade.localPosition += Vector3.forward * (railBounds.max.z * (1-railScale));
            // The rebuilt steps and upper ledge cover the old rope bridge's walking line.
            HideModel("D3_RopeBridge", true);
            StairCheeks("D1", -59.5f, 3, -41.5f, -53.0f, -3.20f, -11.76f, -12.03f);
            StairCheeks("D3 dock", -43.8f, 2.5f, -54.5f, -59.95f, -11.35f, -7.49f, -12.08f);
            JoinLowerWalks("D2 perimeter connector", "D2 to D4 dry perimeter", "D4 west perimeter connector", "D4 dry control approach", "D4 control landing");

            foreach (float x in new[] { -69.4f, -60.9f, -51.7f, -40.3f })
            {
                Block("North hall wall pier", P(x,-6.25f,-70.70f), P(.73f,13.3f,.77f), lining);
                Block("North hall pier capital", P(x,-.30f,-70.56f), P(1.10f,.36f,.92f), coping);
            }
            Block("North hall waterline course", P(-55.58f,-10.97f,-70.92f), P(43.0f,.32f,.40f), lining);
            Block("North vault spring course / east", P(-39.2f,1.39f,-70.75f), P(11.4f,.45f,.62f), lining);
            Block("North vault spring course / west", P(-62.6f,1.39f,-70.75f), P(29.9f,.45f,.62f), lining);
            Block("West vault spring course", P(-77.92f,1.39f,-53.1f), P(.62f,.45f,35.8f), lining);
        }

        void StairCheeks(string label, float x, float width, float start, float end, float topA, float topB, float bottom)
        {
            foreach (int sign in new[] { -1, 1 })
            {
                float side = x + sign * (width * .5f + .035f);
                var a = P(side,topA,start);var b = P(side,topB,end);
                Slab(label + " continuous stair stringer", new[] { a, b }, .105f, Mathf.Max(topA, topB) - bottom, bottom, lining);
            }
        }

        void HideModel(string name, bool removeCollider)
        {
            var original = model[name];
            original.GetComponent<MeshRenderer>().enabled = false;
            if (removeCollider)
                foreach (var c in original.GetComponents<Collider>()) c.enabled = false;
        }

        void HideSupport(string name, bool removeCollision = false)
        {
            Transform part = supports.TryGetValue(name, out var support) ? support :
                transform.parent.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
            foreach (var renderer in part.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled = false;
            if(removeCollision)
                foreach(var collider in part.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
        }

        void JoinLowerWalks(params string[] names)
        {
            var boxes = names.Select(n => supports[n].GetComponent<BoxCollider>().bounds).ToArray();
            var xs = boxes.SelectMany(b => new[] { b.min.x, b.max.x }).Distinct().OrderBy(x => x).ToArray();
            var zs = boxes.SelectMany(b => new[] { b.min.z, b.max.z }).Distinct().OrderBy(z => z).ToArray();
            const float top = -11.5f, bottom = -11.87f;
            var skin = new Solid();
            bool Covered(int x, int z) => x >= 0 && z >= 0 && x < xs.Length - 1 && z < zs.Length - 1 &&
                boxes.Any(b => (xs[x]+xs[x+1])*.5f > b.min.x && (xs[x]+xs[x+1])*.5f < b.max.x &&
                    (zs[z]+zs[z+1])*.5f > b.min.z && (zs[z]+zs[z+1])*.5f < b.max.z);
            for (int x = 0; x < xs.Length - 1; x++)
            for (int z = 0; z < zs.Length - 1; z++)
            {
                if (!Covered(x,z)) continue;
                var a = P(xs[x],top,zs[z]); var b = P(xs[x+1],top,zs[z]);
                var c = P(xs[x+1],top,zs[z+1]); var d = P(xs[x],top,zs[z+1]);
                skin.Quad(a,d,c,b);
                if (!Covered(x-1,z)) skin.Wall(d,a,bottom);
                if (!Covered(x+1,z)) skin.Wall(b,c,bottom);
                if (!Covered(x,z-1)) skin.Wall(a,b,bottom);
                if (!Covered(x,z+1)) skin.Wall(c,d,bottom);
            }
            foreach (string name in names) HideSupport(name);
            Install("D2 to D4 / joined dry perimeter", skin, tread);
        }

        void Slab(string name, Vector3[] path, float width, float depth, float floor = float.NaN, Material material = null, bool solid=false)
        {
            var mesh = new Solid();
            Vector3 main = path[1]-path[0];
            var right = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(main, Vector3.up).normalized) * width * .5f;
            Vector3 Bottom(Vector3 p) { p.y = float.IsNaN(floor) ? p.y - depth : floor; return p; }
            for (int i = 1; i < path.Length; i++)
            {
                var a = path[i-1]-right;var b = path[i-1]+right;var c = path[i]+right;var d = path[i]-right;
                mesh.Quad(a,d,c,b);
                mesh.Quad(a,Bottom(a),Bottom(d),d);mesh.Quad(b,c,Bottom(c),Bottom(b));
                mesh.Quad(Bottom(a),Bottom(b),Bottom(c),Bottom(d));
                if (i == 1) mesh.Quad(a,b,Bottom(b),Bottom(a));
                if (i == path.Length-1) mesh.Quad(d,Bottom(d),Bottom(c),c);
            }
            Install(name, mesh, material != null ? material : tread, solid);
        }

        void Kerb(Vector3 a, Vector3 b, bool solid)
        {
            var p = (a + b) * .5f;
            float length = Vector3.Distance(a, b);
            var rotation = Quaternion.LookRotation(b - a);
            Block("Cistern dressed retaining edge", p+Vector3.up*.015f, P(.36f,.42f,length+.015f), lining, rotation, solid);
            Block("Cistern pale coping", p+Vector3.up*.22f, P(.42f,.095f,length+.025f), coping, rotation, solid);
        }

        void Block(string name, Vector3 position, Vector3 size, Material material, Quaternion? rotation = null, bool solid = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, true);
            go.transform.SetPositionAndRotation(position, rotation ?? Quaternion.identity);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<BoxCollider>().enabled = solid;
        }

        sealed class Solid
        {
            internal readonly List<Vector3> vertices = new List<Vector3>();
            internal readonly List<int> triangles = new List<int>();
            internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = vertices.Count;vertices.AddRange(new[] { a, b, c, d });
                triangles.AddRange(new[] { i, i+1, i+2, i, i+2, i+3 });
            }
            internal void Wall(Vector3 a, Vector3 b, float bottom) => Quad(a,b,P(b.x,bottom,b.z),P(a.x,bottom,a.z));
        }

        void Install(string name, Solid solid, Material material, bool collision=false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var mesh = MakeMesh(name, solid.vertices, solid.triangles);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }

        EFV2WaterSegment Segment(string label) => water.segments.Single(s => s.label == label);

        EFV2WaterSegment NewWater(string label, Transform parent, EFV2WaterSegment style, WaterPath path, bool local = false)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var segment = new EFV2WaterSegment { label = label, filter = go.AddComponent<MeshFilter>(), renderer = go.AddComponent<MeshRenderer>(),
                materialOverride = style.materialOverride, networkSurface = true, pathFlow = true, speed = .64f, foam = .18f };
            water.segments = water.segments.Append(segment).ToArray();
            Replace(segment, path, local);
            segment.direction = (path.nodes[path.nodes.Count-1].position - path.nodes[0].position).normalized;
            return segment;
        }

        void MoveImpact(EFV2WaterSegment source, Vector3 point)
        {
            source.impactPoints = new[] { point };
            foreach (var effect in water.GetComponentsInChildren<EFWaterImpact>(true).Where(i => i.sourceLabel == source.label))
                effect.transform.position = point + Vector3.up * .045f;
        }

        void Replace(EFV2WaterSegment segment, WaterPath path, bool local = false)
        {
            var space = segment.filter.transform;
            var vertices = new List<Vector3>();var uvs = new List<Vector2>();var progress = new List<Vector2>();
            var colors = new List<Color>();var indices = new List<int>();var tangents = new List<Vector4>();var normals = new List<Vector3>();
            float distance = 0, travel = 0, total = 0;
            for (int i = 1; i < path.nodes.Count; i++) total += Vector3.Distance(path.nodes[i-1].position, path.nodes[i].position);
            const int across = 8;
            for (int i = 0; i < path.nodes.Count; i++)
            {
                var node = path.nodes[i];
                if (i > 0)
                {
                    float step = Vector3.Distance(node.position, path.nodes[i-1].position);
                    distance += step;
                    travel += step / Mathf.Lerp(.72f, 3.5f, node.fall);
                }
                var dir = path.nodes[Mathf.Min(path.nodes.Count-1,i+1)].position-path.nodes[Mathf.Max(0,i-1)].position;
                var right = Vector3.Cross(Vector3.up, dir).normalized;
                if (right.sqrMagnitude < .5f) right = Vector3.right;
                var tangent = local ? right : space.InverseTransformDirection(right);
                var normal = Vector3.Cross(dir, right).normalized;
                if (!local) normal = space.InverseTransformDirection(normal);
                for (int j = 0; j <= across; j++)
                {
                    float u = j/(float)across;
                    var p = node.position + right * ((u-.5f) * node.width);
                    vertices.Add(local ? p : space.InverseTransformPoint(p));
                    uvs.Add(new Vector2(u * node.width, travel));
                    progress.Add(new Vector2(distance / total, 0));
                    float pin = Mathf.Clamp01(distance/.30f)*Mathf.Clamp01((total-distance)/.30f)*Mathf.Sin(u*Mathf.PI);
                    colors.Add(new Color(node.fall, node.foam, pin, j == 0 || j == across ? .20f : 1));
                    tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1));
                    normals.Add(normal);
                    if (i == 0 || j == 0) continue;
                    int a = (i-1)*(across+1)+j-1, b = a+1, c = i*(across+1)+j-1, d = c+1;
                    indices.AddRange(new[] { a,c,b,b,c,d });
                }
            }
            if (path.depth > 0)
                CloseWaterBody(path.depth, across+1, vertices, indices, uvs, progress, colors, tangents, normals);
            var mesh = MakeMesh(segment.label + " / waterworks", vertices, indices, uvs, colors);
            mesh.SetUVs(1, progress);mesh.SetTangents(tangents);
            SetMesh(segment, mesh);
            segment.networkWorldUV = false;
        }

        static void CloseWaterBody(float depth,int stride,List<Vector3> vertices,List<int> indices,List<Vector2> uv,
            List<Vector2> progress,List<Color> colors,List<Vector4> tangents,List<Vector3> normals)
        {
            int topCount=vertices.Count, topIndices=indices.Count;
            for (int i=0;i<topCount;i++)
            {
                float thickness=depth*Mathf.Lerp(.17f,1,colors[i].r);
                vertices.Add(vertices[i]-normals[i]*thickness);
                uv.Add(uv[i]);progress.Add(new Vector2(progress[i].x,1));tangents.Add(tangents[i]);
                colors.Add(new Color(colors[i].r,0,0,colors[i].a));
            }
            for (int i=0;i<topIndices;i+=3)
                indices.AddRange(new[]{indices[i]+topCount,indices[i+2]+topCount,indices[i+1]+topCount});

            void CloseEdge(int a,int b)
            {
                int start=vertices.Count;
                foreach (int src in new[]{a,b,a+topCount,b+topCount})
                {
                    vertices.Add(vertices[src]);uv.Add(uv[src]);progress.Add(new Vector2(progress[src].x,1));
                    colors.Add(new Color(colors[src].r,0,0,.80f));tangents.Add(tangents[src]);
                }
                indices.AddRange(new[]{start,start+1,start+2,start+1,start+3,start+2});
            }
            int lastRow=topCount-stride;
            for (int row=0;row<lastRow;row+=stride)
            {CloseEdge(row,row+stride);CloseEdge(row+2*stride-1,row+stride-1);}
            for (int i=0;i<stride-1;i++)
            {CloseEdge(i+1,i);CloseEdge(lastRow+i,lastRow+i+1);}
        }

        void SetMesh(EFV2WaterSegment segment, Mesh mesh)
        {
            segment.preparedMesh = mesh;
            segment.filter.sharedMesh = mesh;
        }

        Mesh MakeMesh(string name, List<Vector3> vertices, List<int> indices, List<Vector2> uv = null, List<Color> colors = null)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);
            if (uv != null) mesh.SetUVs(0,uv);
            if (colors != null) mesh.SetColors(colors);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            if (uv != null) mesh.RecalculateTangents();
            meshes.Add(mesh);
            return mesh;
        }

        sealed class WaterPath
        {
            internal readonly List<(Vector3 position,float width,float fall,float foam)> nodes = new List<(Vector3,float,float,float)>();
            internal readonly float depth;
            internal WaterPath(Vector3 start, float width, float depth = 0)
            { this.depth=depth;nodes.Add((start,width,0,0)); }
            internal void Line(Vector3 end, float width) => Append(end,width,Vector3.Lerp);
            internal void Turn(Vector3 control, Vector3 end, float width) => Append(end,width,
                (start,finish,t) => Vector3.Lerp(Vector3.Lerp(start,control,t),Vector3.Lerp(control,finish,t),t));
            internal void Drop(Vector3 end, float width) => Append(end,width,(start,finish,t) =>
                P(Mathf.Lerp(start.x,finish.x,t),Mathf.Lerp(start.y,finish.y,t*t),Mathf.Lerp(start.z,finish.z,t)), true);

            void Append(Vector3 end,float width,System.Func<Vector3,Vector3,float,Vector3> curve,bool fall=false)
            {
                var start = nodes[nodes.Count-1];
                int steps = Mathf.Max(fall?16:4,Mathf.CeilToInt(Vector3.Distance(start.position,end)/.24f));
                for (int i = 1; i <= steps; i++)
                {
                    float t = i/(float)steps;
                    nodes.Add((curve(start.position,end,t),Mathf.Lerp(start.width,width,t),
                        fall ? Mathf.SmoothStep(0,1,t*4) : 0,fall ? Mathf.SmoothStep(0,.65f,(t-.78f)/.22f) : 0));
                }
            }
        }

        void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}
