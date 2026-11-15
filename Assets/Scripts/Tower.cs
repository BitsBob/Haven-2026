using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Tower : MonoBehaviour
{
    [Tooltip("Enemies summoned in wave 1, and how many more for each wave after.")]
    [SerializeField] int baseEnemies = 14;
    [SerializeField] int enemiesPerWave = 6;
    [SerializeField] float interactRange = 7f;

    enum State { Idle, Fighting, Cleared }
    State state = State.Idle;
    int remaining;

    static readonly List<Tower> all = new();
    static Tower focused;
    static int focusFrame = -1;
    static GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { all.Clear(); focused = null; focusFrame = -1; }

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
        foreach (Tower t in all)
        {
            if (t.state != State.Idle) continue;
            Vector3 to = t.transform.position - player.transform.position;
            to.y = 0f;
            float d = to.magnitude;
            if (d <= t.interactRange && d < best) { best = d; focused = t; }
        }
    }

    void Update()
    {
        UpdateFocus();
        if (focused == this && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) Summon();
    }

    void Summon()
    {
        state = State.Fighting;
        Sfx.TowerOpen();
        int count = baseEnemies + enemiesPerWave * Mathf.Max(0, EnemySpawner.Wave - 1);
        remaining = count;
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 position = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(8f, 15f);

            Enemy e;
            if (i == 0) e = Enemy.Spawn(EnemyType.Heavy, true, position);
            else
            {
                EnemyType type = Random.value < 0.85f ? EnemyType.Normal : EnemyType.Flying;
                e = Enemy.Spawn(type, false, type == EnemyType.Flying ? position + Vector3.up * 3f : position);
            }
            e.Died += OnSummonDied;
        }
    }

    void OnSummonDied(Enemy e)
    {
        if (state != State.Fighting || --remaining > 0) return;
        state = State.Cleared;

        Vector3 direction = transform.forward;
        PlayerHealth player = PlayerHealth.Instance;
        if (player != null)
        {
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) direction = to.normalized;
        }
        Pickup.SpawnRandom(transform.position + direction * 4.5f + Vector3.up * 2f, Rarity.Uncommon);
    }

    void OnGUI()
    {
        string text;
        if (state == State.Idle && focused == this) text = "Tower   [E] summon a horde for an item";
        else if (state == State.Fighting) text = $"Tower horde: {remaining} left";
        else return;

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 4f);
        if (screen.z <= 0f || screen.z > 120f) return;

        if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        float s = Screen.height / 720f;
        style.fontSize = Mathf.RoundToInt(18f * s);
        style.normal.textColor = state == State.Fighting ? new Color(1f, 0.5f, 0.4f) : new Color(1f, 0.85f, 0.25f);

        float width = 420f * s, x = screen.x - width / 2f, y = Screen.height - screen.y - 30f * s;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x, y, width, 30f * s), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, width, 30f * s), text, style);
    }
}
