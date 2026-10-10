using System.Collections.Generic;
using UnityEngine;

// Attach ONLY this script to an empty GameObject in a NEW empty test scene.
// Everything below is created in Play mode and disappears when Play stops.
public class EFPrototype : MonoBehaviour
{
    readonly List<EFNode> nodes = new List<EFNode>();
    readonly List<Material> materials = new List<Material>();
    EFPlayer player;
    Material baseMaterial;

    void Start()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) { Debug.LogError("Use a URP or Built-in 3D project."); enabled = false; return; }
        baseMaterial = new Material(shader);
        materials.Add(baseMaterial);

        Cube("Floor", new Vector3(0, -.5f, 6), new Vector3(28, 1, 32), new Color(.22f, .24f, .28f));
        EFNode source = Node("A_Source [3]", new Vector3(-6, .75f, 1), new Vector3(1.5f, 1.5f, 1.5f));
        source.isPowerSource = true;
        EFNode vine = Node("A_Vine [1]", new Vector3(0, .5f, 1), Vector3.one);
        vine.acceptsLife = vine.needsGrowthToConduct = true;
        vine.grownShape = Cube("GrownVine", new Vector3(0, .25f, 1), new Vector3(10, .3f, .5f), Color.green);
        vine.grownShape.transform.SetParent(vine.transform, true);
        vine.grownShape.SetActive(false);
        EFNode door = Node("A_Door [network only]", new Vector3(6, 1.5f, 1), new Vector3(1, 3, 4));
        door.isDoor = true;
        source.outputs = new[] { vine };
        vine.outputs = new[] { door };

        EFNode machine = Node("B_Machine [3 then 2]", new Vector3(-6, 1.5f, 10), new Vector3(6, .5f, 1.5f), true);
        machine.isPowerSource = machine.acceptsFreeze = machine.isRotatingMachine = true;

        EFNode waterSource = Node("C_WaterSource [3]", new Vector3(2, .75f, 8), new Vector3(1.5f, 1.5f, 1.5f));
        waterSource.isPowerSource = true;
        EFNode water = Node("C_Water [2 after filling]", new Vector3(6, .25f, 10), new Vector3(5, .5f, 6));
        water.isWater = water.acceptsFreeze = true;
        waterSource.outputs = new[] { water };

        var network = gameObject.AddComponent<EFPowerNetwork>();
        network.nodes = nodes.ToArray();
        foreach (EFNode node in nodes) node.ResetState();

        Camera camera = Camera.main;
        if (camera == null)
        {
            var cameraObject = new GameObject("LabCamera");
            cameraObject.transform.SetParent(transform);
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();
        }
        camera.orthographic = false;
        camera.fieldOfView = 55;
        GameObject character = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        character.name = "Player";
        character.transform.SetParent(transform);
        character.transform.position = new Vector3(0, 1.2f, -6);
        character.layer = 2; // Ignore Raycast
        var originalCollider = character.GetComponent<CapsuleCollider>();
        originalCollider.enabled = false;
        Destroy(originalCollider);
        var controller = character.AddComponent<CharacterController>();
        controller.height = 2;
        controller.radius = .45f;
        controller.stepOffset = .3f;
        player = character.AddComponent<EFPlayer>();
        player.view = camera;
        player.lab = this;
        Paint(character, Color.white);
    }

    EFNode Node(string label, Vector3 position, Vector3 scale, bool moving = false)
    {
        GameObject obj = Cube(label, position, scale, Color.gray);
        if (moving)
        {
            var body = obj.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }
        EFNode node = obj.AddComponent<EFNode>();
        nodes.Add(node);
        return node;
    }

    GameObject Cube(string label, Vector3 position, Vector3 scale, Color color)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = label;
        obj.transform.SetParent(transform);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        Paint(obj, color);
        return obj;
    }

    void Paint(GameObject obj, Color color)
    {
        var material = new Material(baseMaterial) { color = color };
        materials.Add(material);
        obj.GetComponent<Renderer>().sharedMaterial = material;
    }

    public void ResetLab()
    {
        foreach (EFNode node in nodes) node.ResetState();
        if (player != null) { player.Respawn(); player.feedback = "All states reset"; }
    }

    void OnGUI()
    {
        if (player == null) return;
        GUI.Box(new Rect(10, 10, 740, 140), "EVERFLOW - mechanics lab (not finished puzzles)");
        GUI.Label(new Rect(20, 35, 710, 25), "WASD: move | Space: jump | 1 LIFE | 2 FREEZE | 3 ACTIVATE | Click: use | R: reset");
        GUI.Label(new Rect(20, 60, 710, 25), "Selected: " + player.selected + " | " + player.feedback);
        GUI.Label(new Rect(20, 85, 710, 25), player.hovered != null ? player.hovered.name + " | " + player.hovered.StateText : "Point at a named object; walk closer if needed.");
        GUI.Label(new Rect(20, 110, 710, 25), "A: Source -> grown Vine -> Door | B: activate + freeze machine | C: fill + freeze water");
        foreach (EFNode node in nodes)
        {
            Vector3 point = player.view.WorldToScreenPoint(node.transform.position + Vector3.up * 1.3f);
            if (point.z > 0)
                GUI.Label(new Rect(point.x - 110, Screen.height - point.y, 270, 24), node.name);
        }
    }

    void OnDestroy()
    {
        foreach (Material material in materials) if (material != null) Destroy(material);
    }
}
