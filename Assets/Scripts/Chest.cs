using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Chest : MonoBehaviour
{
    public static int Opened { get; private set; }

    [Header("Price")]
    [SerializeField] int baseCost = 20;
    [Tooltip("Each chest opened multiplies the price of the next by this.")]
    [SerializeField] float costGrowth = 1.25f;

    [Header("Lid")]
    [SerializeField] Transform lidHinge;
    [SerializeField] Vector3 hingeAxis = Vector3.forward;
    [SerializeField] float openAngle = 100f;
    [SerializeField] float openTime = 0.45f;

    [SerializeField] float interactRange = 3.5f;

    bool opened;
    float openProgress, deniedUntil;
    Quaternion closedRotation;

    static readonly List<Chest> all = new();
    static Chest focused;
    static int focusFrame = -1;
    static GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Opened = 0; all.Clear(); focused = null; focusFrame = -1; }

    public int Cost => Mathf.RoundToInt(baseCost * Mathf.Pow(costGrowth, Opened));

    void Awake() { if (lidHinge != null) closedRotation = lidHinge.localRotation; }
    void OnEnable() { all.Add(this); }
    void OnDisable() { all.Remove(this); if (focused == this) focused = null; }

    static void UpdateFocus()
    {
        if (focusFrame == Time.frameCount) return;
        focusFrame = Time.frameCount;
        focused = null;
        PlayerHealth player = PlayerHealth.Instance;
        if (player == null || player.IsDead) return;
        float best = float.MaxValue;
        foreach (Chest c in all)
        {
            if (c.opened) continue;
            float d = Vector3.Distance(c.transform.position, player.transform.position);
            if (d <= c.interactRange && d < best) { best = d; focused = c; }
        }
    }

    void Update()
    {
        UpdateFocus();

        if (opened && openProgress < 1f && lidHinge != null)
        {
            openProgress = Mathf.Min(1f, openProgress + Time.deltaTime / openTime);
            float t = openProgress, eased = 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f);
            lidHinge.localRotation = closedRotation * Quaternion.AngleAxis(openAngle * eased, hingeAxis);
        }

        if (focused != this || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        if (PlayerMoney.Instance == null || !PlayerMoney.Instance.TrySpend(Cost)) { deniedUntil = Time.time + 1f; return; }

        opened = true;
        Opened++;
        Invoke(nameof(DropItem), openTime * 0.6f);
    }

    void DropItem()
    {
        Vector3 spot = transform.position + transform.forward * 1.8f;
        PlayerHealth player = PlayerHealth.Instance;
        if (player != null)
        {
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            Vector3 direction = distance > 0.01f ? to / distance : transform.forward;
            spot = distance > 2.6f
                ? transform.position + direction * Mathf.Min(2f, distance - 1.7f)
                : transform.position + Vector3.Cross(Vector3.up, direction) * 1.8f;
        }
        Pickup.SpawnRandom(spot + Vector3.up);
    }

    void OnGUI()
    {
        if (focused != this) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.6f);
        if (screen.z <= 0f) return;

        if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        float s = Screen.height / 720f;
        style.fontSize = Mathf.RoundToInt(18f * s);

        bool denied = Time.time < deniedUntil;
        bool affordable = PlayerMoney.Instance != null && PlayerMoney.Instance.CanAfford(Cost);
        style.normal.textColor = denied || !affordable ? new Color(1f, 0.4f, 0.35f) : new Color(1f, 0.85f, 0.25f);
        string text = denied ? $"Not enough money (${Cost})" : $"Chest  ${Cost}   [E] open";

        float width = 320f * s, x = screen.x - width / 2f, y = Screen.height - screen.y - 30f * s;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x, y, width, 30f * s), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, width, 30f * s), text, style);
    }
}
