using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public class PizzaBoxHud : MonoBehaviour
{
    const int HudLayer = 29;
    const float Distance = 10f;
    const float HudFov = 30f;

    [SerializeField] GameObject boxHalf;

    [Header("Placement")]
    [Tooltip("How far the lid is swung open, in degrees. 0 is closed.")]
    [SerializeField] float openAngle = 165f;
    [Tooltip("Tilt of the whole box: X tips the inside toward you, Y turns it, Z rolls it.")]
    [SerializeField] Vector3 boxAngles = new Vector3(-30f, -45f, 8f);
    [Tooltip("Where the middle of the hinge (the fold between lid and base) sits on screen. (0,0) is bottom-left, (1,1) is top-right.")]
    [SerializeField] Vector2 screenPosition = new Vector2(0.11f, 0.17f);
    [Tooltip("Width of the box as a fraction of the screen height.")]
    [SerializeField] float size = 0.34f;

    [Header("Motion (set any to 0 to switch it off)")]
    [Tooltip("How far the box trails behind when you turn the camera.")]
    [SerializeField] float lookSway = 1f;
    [Tooltip("How much the box bobs with your footsteps.")]
    [SerializeField] float walkBob = 1f;
    [Tooltip("How much the box drops when you jump and lifts when you fall.")]
    [SerializeField] float jumpLag = 1f;
    [Tooltip("How much the box leans when you strafe.")]
    [SerializeField] float strafeLean = 1f;
    [Tooltip("How much the lid flaps on jumps and landings.")]
    [SerializeField] float lidFlap = 1f;

    PlayerController player;
    Rigidbody body;
    PizzaThrower thrower;
    Camera mainCamera, hudCamera;
    Transform box, hinge;
    GameObject[] slices = new GameObject[0];
    float lidOpen = 1f;
    float pizzaRadius;
    float modelWidth = 1f;

    Vector2 sway;
    float bobPhase, bobWeight;
    float lean;
    float dropY, dropVelocity;
    float lidAngle, lidVelocity;

    public Transform Hinge => hinge;

    void Start()
    {
        if (boxHalf == null) { Debug.LogError("PizzaBoxHud: assign the 'box half' model to the Box Half field.", this); enabled = false; return; }

        player = GetComponent<PlayerController>();
        thrower = GetComponent<PizzaThrower>();
        body = GetComponent<Rigidbody>();
        mainCamera = player != null && player.playerCamera != null ? player.playerCamera : Camera.main;

        var rig = new GameObject("Pizza Box HUD").transform;
        rig.position = new Vector3(0f, -500f, 0f);

        hudCamera = new GameObject("HUD Camera").AddComponent<Camera>();
        hudCamera.transform.SetParent(rig, false);
        hudCamera.cullingMask = 1 << HudLayer;
        hudCamera.fieldOfView = HudFov;
        hudCamera.nearClipPlane = 0.1f;
        hudCamera.farClipPlane = 50f;
        hudCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;

        mainCamera.cullingMask &= ~(1 << HudLayer);
        mainCamera.GetUniversalAdditionalCameraData().cameraStack.Add(hudCamera);

        BuildBox(rig);
    }

    void BuildBox(Transform rig)
    {
        box = new GameObject("Box").transform;
        box.SetParent(rig, false);

        GameObject bottom = Instantiate(boxHalf, box, false);
        Bounds b = MeasureLocal(bottom, box);
        modelWidth = Mathf.Max(0.001f, b.size.x);

        hinge = new GameObject("Lid Hinge").transform;
        hinge.SetParent(box, false);
        hinge.localPosition = new Vector3(b.center.x, b.max.y, b.max.z);

        var flip = new GameObject("Lid").transform;
        flip.SetParent(hinge, false);
        flip.localPosition = new Vector3(0f, 0f, b.center.z - b.max.z);
        flip.localRotation = Quaternion.Euler(180f, 0f, 0f);
        GameObject lid = Instantiate(boxHalf, flip, false);
        lid.transform.localPosition -= new Vector3(b.center.x, b.max.y, b.center.z);

        BuildSlices(b);

        MarkAsHud(box);
        Place();

        SliceVisual.ToppingCollected += AddToppingToSlices;
    }

    void OnDestroy() { SliceVisual.ToppingCollected -= AddToppingToSlices; }

    void AddToppingToSlices(GameObject template)
    {
        if (slices.Length == 0) return;
        int guaranteed = Random.Range(0, slices.Length);
        for (int i = 0; i < slices.Length; i++)
        {
            if (i != guaranteed && Random.value > SliceVisual.ToppingChance) continue;
            Transform topping = SliceVisual.AddTopping(slices[i].transform, template, pizzaRadius);
            if (topping != null) MarkAsHud(topping);
        }
    }

    static void MarkAsHud(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = HudLayer;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    void BuildSlices(Bounds b)
    {
        if (thrower == null || thrower.SliceModel == null) return;
        int count = Mathf.Max(1, thrower.magazineSize);
        pizzaRadius = b.size.x * 0.44f;
        var centre = new Vector3(b.center.x, b.min.y + b.size.x * 0.01f, b.center.z);

        slices = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            Transform slice = SliceVisual.Create(thrower.SliceModel, box, pizzaRadius, thrower.SliceTipAtMinX);
            slice.localPosition = centre;
            slice.localRotation = Quaternion.Euler(0f, i * 360f / count, 0f);
            slices[i] = slice.gameObject;
        }
    }

    static Bounds MeasureLocal(GameObject model, Transform space)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        b.center -= space.position;
        return b;
    }

    void LateUpdate()
    {
        if (box == null) return;
        UpdateMotion(Mathf.Min(Time.deltaTime, 0.033f));
        Place();
    }

    void UpdateMotion(float dt)
    {
        if (dt <= 0f) return;

        Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
        Transform view = mainCamera.transform;
        float sideSpeed = Vector3.Dot(velocity, view.right);
        float groundSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        bool grounded = player == null || player.isGrounded;
        bool sliding = player != null && player.isSliding;

        Vector2 look = Vector2.zero;
        if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            look = Mouse.current.delta.ReadValue() / dt;
        Vector2 swayTarget = Vector2.ClampMagnitude(-look * 0.00002f, 0.035f) * lookSway;
        sway = Vector2.Lerp(sway, swayTarget, 1f - Mathf.Exp(-9f * dt));

        float bobTarget = grounded && !sliding ? Mathf.Clamp01(groundSpeed / 12f) : 0f;
        bobWeight = Mathf.Lerp(bobWeight, bobTarget, 1f - Mathf.Exp(-8f * dt));
        bobPhase += groundSpeed * 0.9f * dt;

        float leanTarget = Mathf.Clamp(-sideSpeed * 0.6f, -8f, 8f) * strafeLean + (sliding ? -6f * strafeLean : 0f);
        lean = Mathf.Lerp(lean, leanTarget, 1f - Mathf.Exp(-7f * dt));

        float dropTarget = Mathf.Clamp(-velocity.y * 0.004f, -0.06f, 0.06f) * jumpLag;
        Spring(ref dropY, ref dropVelocity, dropTarget, 120f, 14f, dt);

        float lidTarget = Mathf.Clamp(velocity.y * 1.5f, -30f, 20f) * lidFlap;
        Spring(ref lidAngle, ref lidVelocity, lidTarget, 90f, 8f, dt);

        lidOpen = 1f;
        if (thrower != null && thrower.Reloading)
        {
            float p = thrower.ReloadProgress;
            lidOpen = p < 0.4f ? 1f - Mathf.SmoothStep(0f, 1f, p / 0.4f) : (p < 0.6f ? 0f : Mathf.SmoothStep(0f, 1f, (p - 0.6f) / 0.4f));
        }

        int ammo = thrower != null ? thrower.Ammo : slices.Length;
        for (int i = 0; i < slices.Length; i++)
            if (slices[i].activeSelf != (i < ammo)) slices[i].SetActive(i < ammo);
    }

    static void Spring(ref float value, ref float velocity, float target, float stiffness, float damping, float dt)
    {
        velocity += (stiffness * (target - value) - damping * velocity) * dt;
        value += velocity * dt;
    }

    void Place()
    {
        float screenHeightInWorld = 2f * Distance * Mathf.Tan(HudFov * 0.5f * Mathf.Deg2Rad);
        box.localScale = Vector3.one * (size * screenHeightInWorld / modelWidth);

        float bobX = Mathf.Cos(bobPhase) * 0.006f * bobWeight * walkBob;
        float bobY = Mathf.Sin(bobPhase * 2f) * 0.009f * bobWeight * walkBob;
        float offsetX = sway.x + bobX;
        float offsetY = sway.y + bobY + dropY - 0.03f * (1f - lidOpen);

        Quaternion motion = Quaternion.Euler(-sway.y * 150f, sway.x * 150f, lean);
        box.localRotation = motion * Quaternion.Euler(boxAngles);
        hinge.localRotation = Quaternion.Euler(Mathf.Clamp((openAngle + lidAngle) * lidOpen, 0f, 200f), 0f, 0f);

        var viewport = new Vector3(screenPosition.x + offsetX / hudCamera.aspect, screenPosition.y + offsetY, Distance);
        box.position += hudCamera.ViewportToWorldPoint(viewport) - hinge.position;
    }
}
