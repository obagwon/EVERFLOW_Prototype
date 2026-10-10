using UnityEngine;

// An object's properties and state. Abilities request changes; the network
// derives power from the CURRENT connections every frame.
public class EFNode : MonoBehaviour
{
    public bool acceptsLife;
    public bool acceptsFreeze;
    public bool isPowerSource;
    public bool needsGrowthToConduct;
    public bool isRotatingMachine;
    public bool isWater;
    public bool isDoor;
    public EFNode[] outputs = new EFNode[0];
    public GameObject grownShape;
    public float rotationSpeed = 35f;

    public bool Grown { get; private set; }
    public bool Frozen { get; private set; }
    public bool SourceOn { get; private set; }
    public bool Powered { get; set; }
    public bool Filled { get; private set; }
    public bool Conducts => !needsGrowthToConduct || Grown;

    Vector3 startPosition;
    Quaternion startRotation;
    Renderer visual;
    Collider body;
    Rigidbody rigidBody;

    void Awake()
    {
        startPosition = transform.localPosition;
        startRotation = transform.localRotation;
        visual = GetComponent<Renderer>();
        body = GetComponent<Collider>();
        rigidBody = GetComponent<Rigidbody>();
    }

    public string Apply(int ability)
    {
        if (ability == 1 && acceptsLife)
        {
            Grown = true; // Persistent growth in this experiment.
            if (grownShape != null) grownShape.SetActive(true);
            return name + ": grown";
        }
        if (ability == 2 && acceptsFreeze)
        {
            if (isWater && !Filled) return "No water yet: activate WaterSource first";
            Frozen = !Frozen;
            return name + (Frozen ? ": frozen" : ": released");
        }
        if (ability == 3 && isPowerSource)
        {
            SourceOn = !SourceOn;
            return name + (SourceOn ? ": source ON" : ": source OFF");
        }
        return name + ": this ability does not apply";
    }

    public void RefreshAppearance()
    {
        // A filled basin retains water when the inlet source is turned off.
        if (isWater && Powered) Filled = true;
        if (isWater && body != null) body.isTrigger = !(Filled && Frozen);
        if (isDoor)
            transform.localPosition = startPosition + (Powered ? Vector3.up * 5 : Vector3.zero);
        if (visual != null)
        {
            Color color = new Color(.42f, .42f, .42f);
            if (isWater && Filled) color = new Color(.1f, .35f, .8f);
            if (Grown) color = new Color(.15f, .8f, .3f);
            if (Powered) color = new Color(1f, .8f, .1f);
            if (Frozen) color = new Color(.25f, .95f, 1f);
            // Each primitive is given its own material by EFPrototype.
            visual.sharedMaterial.color = color;
        }
    }

    void FixedUpdate()
    {
        if (!isRotatingMachine || Frozen) return;
        Quaternion rest = transform.parent != null ? transform.parent.rotation * startRotation : startRotation;
        // Power-off returns to rest, so ACTIVATE cannot replace FREEZE as a brake.
        Quaternion next = Powered
            ? transform.rotation * Quaternion.Euler(0, rotationSpeed * Time.fixedDeltaTime, 0)
            : Quaternion.RotateTowards(transform.rotation, rest, rotationSpeed * Time.fixedDeltaTime);
        if (rigidBody != null) rigidBody.MoveRotation(next);
    }

    public void ResetState()
    {
        Grown = Frozen = SourceOn = Powered = Filled = false;
        transform.localPosition = startPosition;
        transform.localRotation = startRotation;
        if (rigidBody != null)
        {
            rigidBody.position = transform.position;
            rigidBody.rotation = transform.rotation;
        }
        if (grownShape != null) grownShape.SetActive(false);
        RefreshAppearance();
    }

    public string StateText => $"Grown:{Grown} Frozen:{Frozen} Power:{Powered}";
}
