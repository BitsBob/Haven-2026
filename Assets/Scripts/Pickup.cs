using UnityEngine;

public enum Rarity { Common, Uncommon, Rare }

public enum PickupEffect { ExtraShot, AttackSpeed, Damage, MoveSpeed, MaxHealth, Heal, ExtraBounce, Pierce, Burn, Explode }

public class Pickup : MonoBehaviour
{
    public static Pickup[] DropTable;
    public static float DropChance = 0.02f, EliteDropChance = 0.25f;

    const float CommonWeight = 65f, UncommonWeight = 27f, RareWeight = 8f, RareWeightPerWave = 1f;

    public PickupEffect effect;
    public Rarity rarity;
    [Tooltip("Name shown on the label and in the Tab item list. Leave empty to use the object's name.")]
    [SerializeField] string displayName;
    [SerializeField] float collectRange = 1.2f;
    [Tooltip("How close you have to be to see what the item does.")]
    [SerializeField] float labelRange = 10f;

    Vector3 restPosition;
    float phase;
    static GUIStyle nameStyle, descriptionStyle;

    static readonly System.Collections.Generic.List<Pickup> all = new();
    static Pickup focused;
    static int focusFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { DropTable = null; DropChance = 0.02f; EliteDropChance = 0.25f; all.Clear(); focused = null; focusFrame = -1; }

    void OnEnable() { all.Add(this); }
    void OnDisable() { all.Remove(this); if (focused == this) focused = null; }

    static void UpdateFocus()
    {
        if (focusFrame == Time.frameCount) return;
        focusFrame = Time.frameCount;
        focused = null;
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 eye = cam.transform.position, forward = cam.transform.forward;
        float best = float.MaxValue;
        foreach (Pickup p in all)
        {
            Vector3 to = p.restPosition - eye;
            float distance = to.magnitude;
            if (distance > p.labelRange || distance < 0.01f) continue;
            float angle = Vector3.Angle(forward, to);
            float allowed = Mathf.Max(8f, Mathf.Atan2(0.7f, distance) * Mathf.Rad2Deg);
            if (angle <= allowed && angle < best) { best = angle; focused = p; }
        }
    }

    public string Name
    {
        get
        {
            if (!string.IsNullOrEmpty(displayName)) return displayName;
            return name.Replace("(Clone)", "").Replace(" Pickup", "").Replace('_', ' ').Trim();
        }
    }

    public static string Describe(PickupEffect effect)
    {
        switch (effect)
        {
            case PickupEffect.ExtraShot: return "+1 tentacle shot per throw (half damage)";
            case PickupEffect.AttackSpeed: return "Throw 15% faster";
            case PickupEffect.Damage: return "+25% slice damage";
            case PickupEffect.ExtraBounce: return "+1 ricochet per slice";
            case PickupEffect.MoveSpeed: return "+8% move speed";
            case PickupEffect.MaxHealth: return "+20 max health";
            case PickupEffect.Pierce: return "Slices pierce +1 enemy";
            case PickupEffect.Burn: return "Slices burn enemies: +4 damage a second for 3s";
            case PickupEffect.Explode: return "Slices burst on hit: half damage to everything within 3.5 m (+1 m each extra)";
            default: return "Restore 30 health";
        }
    }

    public static void TryDrop(Vector3 position, float chance)
    {
        if (Random.value <= chance) SpawnRandom(position);
    }

    public static Pickup SpawnRandom(Vector3 position, Rarity minimum = Rarity.Common)
    {
        Pickup prefab = RollPrefab(minimum);
        if (prefab == null) return null;
        if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            position = hit.point;
        return Instantiate(prefab, position + Vector3.up * 0.7f, Quaternion.identity);
    }

    static Pickup RollPrefab(Rarity minimum)
    {
        if (DropTable == null || DropTable.Length == 0) return null;
        float rare = RareWeight + RareWeightPerWave * Mathf.Max(0, EnemySpawner.Wave - 1);
        float roll = Random.value * (CommonWeight + UncommonWeight + rare);
        Rarity wanted = roll < CommonWeight ? Rarity.Common : (roll < CommonWeight + UncommonWeight ? Rarity.Uncommon : Rarity.Rare);
        if (wanted < minimum) wanted = minimum;

        Pickup chosen = null;
        int seen = 0;
        foreach (Pickup p in DropTable)
            if (p != null && p.rarity == wanted && Random.Range(0, ++seen) == 0) chosen = p;
        return chosen != null ? chosen : DropTable[Random.Range(0, DropTable.Length)];
    }

    static Color RarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Rare: return new Color(0.85f, 0.55f, 1f);
            case Rarity.Uncommon: return new Color(0.45f, 1f, 0.55f);
            default: return Color.white;
        }
    }

    void Start()
    {
        restPosition = transform.position;
        phase = Random.Range(0f, 6f);
    }

    void Update()
    {
        UpdateFocus();
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        transform.position = restPosition + Vector3.up * (Mathf.Sin(Time.time * 3f + phase) * 0.15f);

        PlayerHealth player = PlayerHealth.Instance;
        if (player == null || player.IsDead) return;
        if (Vector3.Distance(restPosition, player.transform.position + Vector3.up) < collectRange)
        {
            Apply(player);
            Destroy(gameObject);
        }
    }

    void Apply(PlayerHealth health)
    {
        var thrower = health.GetComponent<PizzaThrower>();
        var controller = health.GetComponent<PlayerController>();
        switch (effect)
        {
            case PickupEffect.ExtraShot: if (thrower != null) thrower.extraShots++; break;
            case PickupEffect.AttackSpeed: if (thrower != null) thrower.cooldown *= 0.85f; break;
            case PickupEffect.Damage: if (thrower != null) thrower.damageMultiplier += 0.25f; break;
            case PickupEffect.ExtraBounce: if (thrower != null) thrower.extraBounces++; break;
            case PickupEffect.MoveSpeed: if (controller != null) controller.movementFactor += 0.08f; break;
            case PickupEffect.MaxHealth: health.maxHealth += 20f; health.Heal(20f); break;
            case PickupEffect.Pierce: if (thrower != null) thrower.pierce++; break;
            case PickupEffect.Burn: if (thrower != null) thrower.burnDamage += 4f; break;
            case PickupEffect.Explode: if (thrower != null) thrower.explodeRadius = thrower.explodeRadius <= 0f ? 3.5f : thrower.explodeRadius + 1f; break;
            default: health.Heal(30f); break;
        }

        string description = Describe(effect);
        if (thrower != null) thrower.ShowMessage(Name + ": " + description);

        if (effect != PickupEffect.Heal && PlayerInventory.Instance != null) PlayerInventory.Instance.Add(Name, description);

        if (transform.childCount > 0) SliceVisual.CollectTopping(transform.GetChild(0).gameObject);
    }

    void OnGUI()
    {
        if (focused != this) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 screen = cam.WorldToScreenPoint(restPosition + Vector3.up * 0.8f);
        if (screen.z <= 0f) return;

        if (nameStyle == null)
        {
            nameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            nameStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);
            descriptionStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            descriptionStyle.normal.textColor = Color.white;
        }
        float s = Screen.height / 720f;
        nameStyle.fontSize = Mathf.RoundToInt(18f * s);
        nameStyle.normal.textColor = RarityColor(rarity);
        descriptionStyle.fontSize = Mathf.RoundToInt(14f * s);

        float width = 300f * s, x = screen.x - width / 2f, y = Screen.height - screen.y - 52f * s;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x, y, width, 50f * s), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y + 2f * s, width, 24f * s), $"{Name}  ({rarity})", nameStyle);
        GUI.Label(new Rect(x, y + 25f * s, width, 22f * s), Describe(effect), descriptionStyle);
    }
}
