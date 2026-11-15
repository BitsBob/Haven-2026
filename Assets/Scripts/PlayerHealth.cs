using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    public float maxHealth = 100f;
    public float health;
    public bool IsDead { get; private set; }

    PlayerController controller;
    float hurtFlash, invulnerableUntil;
    GUIStyle labelStyle, deathStyle;

    void Awake()
    {
        Instance = this;
        health = maxHealth;
        controller = GetComponent<PlayerController>();
        Time.timeScale = 1f;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f || Time.time < invulnerableUntil) return;
        invulnerableUntil = Time.time + 0.35f;
        health = Mathf.Max(0f, health - amount);
        hurtFlash = 0.25f;
        if (health <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (!IsDead) health = Mathf.Min(maxHealth, health + amount);
    }

    void Die()
    {
        IsDead = true;
        Sfx.PlayerDeath();
        Time.timeScale = 0f;
        if (controller != null) controller.paused = true;
    }

    void Update()
    {
        hurtFlash -= Time.unscaledDeltaTime;
        if (!IsDead || Keyboard.current == null) return;
        var keys = Keyboard.current;
        if (keys.rKey.wasPressedThisFrame) Load(SceneManager.GetActiveScene().name);
    }

    static void Load(string scene)
    {
        if (!Application.CanStreamedLevelBeLoaded(scene)) return;
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }

    void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            labelStyle.normal.textColor = Color.white;
            deathStyle = new GUIStyle(labelStyle) { fontSize = 48 };
        }

        float s = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float width = Screen.width / s;

        if (hurtFlash > 0f) Fill(new Rect(0f, 0f, width, 720f), new Color(1f, 0f, 0f, 0.3f * hurtFlash / 0.25f));

        var bar = new Rect(width - 280f, 20f, 260f, 24f);
        Fill(bar, new Color(0f, 0f, 0f, 0.6f));
        Fill(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * Mathf.Clamp01(health / maxHealth), bar.height - 4f), new Color(0.85f, 0.2f, 0.2f));
        GUI.Label(bar, $"{Mathf.CeilToInt(health)} / {Mathf.CeilToInt(maxHealth)}", labelStyle);

        if (IsDead)
        {
            Fill(new Rect(0f, 0f, width, 720f), new Color(0f, 0f, 0f, 0.5f));
            GUI.Label(new Rect(0f, 290f, width, 70f), "YOU DIED", deathStyle);
            GUI.Label(new Rect(0f, 360f, width, 30f), $"You reached wave {EnemySpawner.Wave}.   Press R to restart", labelStyle);
        }
    }

    static void Fill(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
