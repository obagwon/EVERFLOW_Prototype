using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class EFPlayer : MonoBehaviour
{
    public Camera view;
    public EFPrototype lab;
    public float moveSpeed = 5f;
    public float jumpHeight = 1.3f;
    public float castRange = 12f;
    public float gravity = -20f;
    public int selected = 1;
    public string feedback = "Select 1/2/3, then click a block";
    public EFNode hovered;
    CharacterController controller;
    Vector3 spawn;
    float verticalSpeed;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        spawn = transform.position;
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || view == null) return;
        if (keyboard.digit1Key.wasPressedThisFrame) selected = 1;
        if (keyboard.digit2Key.wasPressedThisFrame) selected = 2;
        if (keyboard.digit3Key.wasPressedThisFrame) selected = 3;
        if (keyboard.rKey.wasPressedThisFrame && lab != null) lab.ResetLab();

        Vector3 move = new Vector3(
            (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), 0,
            (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
        move = Vector3.ClampMagnitude(move, 1) * moveSpeed;
        if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
        if (controller.isGrounded && keyboard.spaceKey.wasPressedThisFrame)
            verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalSpeed += gravity * Time.deltaTime;
        move.y = verticalSpeed;
        CollisionFlags flags = controller.Move(move * Time.deltaTime);
        if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
        if (transform.position.y < -10) Respawn();

        hovered = null;
        if (mouse == null) return;
        Vector2 pointer = mouse.position.ReadValue();
        if (pointer.y > Screen.height - 155 && pointer.x < 760) return;
        Ray ray = view.ScreenPointToRay(pointer);
        // The player is on Ignore Raycast (layer 2). Walls still block this ray.
        if (Physics.Raycast(ray, out RaycastHit hit, 100, ~(1 << 2), QueryTriggerInteraction.Collide))
        {
            hovered = hit.collider.GetComponentInParent<EFNode>();
            if (mouse.leftButton.wasPressedThisFrame && hovered != null)
            {
                if (Vector3.Distance(transform.position, hit.point) > castRange)
                    feedback = "Too far: walk closer";
                else if (Physics.Linecast(transform.position + Vector3.up * .3f, hit.point,
                    out RaycastHit blocker, ~(1 << 2), QueryTriggerInteraction.Ignore)
                    && blocker.collider.GetComponentInParent<EFNode>() != hovered)
                    feedback = "Blocked: move to a clear line of sight";
                else feedback = hovered.Apply(selected);
            }
        }
#else
        feedback = "Install Input System and enable Input System Package (New) in Player settings";
#endif
    }

    void LateUpdate()
    {
        if (view == null) return;
        view.transform.position = transform.position + new Vector3(0, 15, -12);
        view.transform.LookAt(transform.position + Vector3.forward * 3);
    }

    public void Respawn()
    {
        controller.enabled = false;
        transform.position = spawn;
        verticalSpeed = 0;
        controller.enabled = true;
    }
}
