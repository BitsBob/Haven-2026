using UnityEngine;
using UnityEngine.InputSystem;

public class PizzaThrower : MonoBehaviour
{
    [Header("Stats (changed by pickups)")]
    public float cooldown = 0.4f;
    public float damageMultiplier = 1f;
    [Tooltip("Extra slices thrown at the same moment as the main one. They don't use extra ammo.")]
    public int extraShots;
    public int extraBounces;
    [Tooltip("Damage of each tentacle shot, as a fraction of the main slice's damage.")]
    public float extraShotDamage = 0.5f;
    [Tooltip("Enemies each slice passes straight through. One per pineapple.")]
    public int pierce;
    [Tooltip("Burn damage per second slices leave on enemies. Four per chilli.")]
    public float burnDamage;
    [Tooltip("Radius of the burst when a slice hits an enemy. From the mushroom; 0 for none.")]
    public float explodeRadius;

    [Tooltip("Degrees between the main slice and each extra one.")]
    [SerializeField] float spreadAngle = 7f;

    [Header("Ammo")]
    [Tooltip("Slices in a full box.")]
    public int magazineSize = 8;
    [Tooltip("Seconds for the box to shut and open again.")]
    [SerializeField] float reloadTime = 1.1f;

    [Header("Slice model")]
    [SerializeField] GameObject sliceModel;
    [Tooltip("Tick this if slices show up crust-first (in flight or in the box).")]
    [SerializeField] bool sliceTipAtMinX;
    [Tooltip("Scattered one or two to a slice. Leave empty for plain slices.")]
    [SerializeField] GameObject pepperoniModel;

    public int Ammo { get; private set; }
    public bool Reloading => reloadTimer > 0f;
    public float ReloadProgress => Reloading ? 1f - reloadTimer / reloadTime : 1f;
    public GameObject SliceModel => sliceModel;
    public bool SliceTipAtMinX => sliceTipAtMinX;

    PlayerController player;
    Camera cam;
    float nextThrow, reloadTimer;
    bool refilled;
    string message;
    float messageUntil;
    GUIStyle messageStyle;

    void Awake()
    {
        Ammo = magazineSize;
        SliceVisual.Reset(pepperoniModel);
    }

    void Start()
    {
        player = GetComponent<PlayerController>();
        cam = player != null ? player.playerCamera : Camera.main;
    }

    public void ShowMessage(string text)
    {
        message = text;
        messageUntil = Time.unscaledTime + 2f;
    }

    void Update()
    {
        if (Reloading)
        {
            reloadTimer -= Time.deltaTime;
            if (!refilled && ReloadProgress >= 0.5f) { Ammo = magazineSize; refilled = true; }
            if (reloadTimer <= 0f) reloadTimer = 0f;
            return;
        }

        if (player != null && (player.paused || !player.canMove)) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        var keyboard = Keyboard.current;
        bool reloadPressed = keyboard != null && keyboard.rKey.wasPressedThisFrame && Ammo < magazineSize;
        if (reloadPressed || Ammo <= 0)
        {
            reloadTimer = reloadTime;
            refilled = false;
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || Time.time < nextThrow) return;
        nextThrow = Time.time + cooldown;
        Ammo--;
        Throw();
    }

    void Throw()
    {
        Sfx.Throw();
        Transform c = cam.transform;

        Vector3 origin = c.position + c.forward * 0.5f - c.up * 0.15f;
        if (Physics.Linecast(c.position, origin, ~0, QueryTriggerInteraction.Ignore)) origin = c.position;

        Vector3 target = c.position + c.forward * 100f;
        if (Physics.Raycast(c.position, c.forward, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore) && hit.distance > 1.5f)
            target = hit.point;
        Vector3 aim = (target - origin).normalized;

        for (int i = 0; i <= extraShots; i++)
        {
            float step = (i + 1) / 2;
            float angle = (i % 2 == 1 ? step : -step) * spreadAngle;
            bool tentacleShot = i > 0;
            PizzaSlice slice = PizzaSlice.Spawn(origin, Quaternion.AngleAxis(angle, c.up) * aim, transform, sliceModel, sliceTipAtMinX, tentacleShot);
            slice.damage *= damageMultiplier * (tentacleShot ? extraShotDamage : 1f);
            slice.maxBounces += extraBounces;
            slice.pierce = pierce;
            slice.burnDamage = burnDamage;
            slice.explodeRadius = explodeRadius;
        }
    }

    void OnGUI()
    {
        const float size = 4f;
        GUI.DrawTexture(new Rect((Screen.width - size) / 2f, (Screen.height - size) / 2f, size, size), Texture2D.whiteTexture);

        if (message == null || Time.unscaledTime > messageUntil) return;
        if (messageStyle == null)
        {
            messageStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            messageStyle.normal.textColor = Color.white;
        }
        messageStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.035f);
        GUI.Label(new Rect(0f, Screen.height * 0.68f, Screen.width, Screen.height * 0.06f), message, messageStyle);
    }
}
