using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    class Entry { public string name, description; public int count; }
    readonly List<Entry> entries = new();

    PizzaThrower thrower;
    PlayerController controller;
    GUIStyle titleStyle, nameStyle, textStyle;

    void Awake()
    {
        Instance = this;
        thrower = GetComponent<PizzaThrower>();
        controller = GetComponent<PlayerController>();
    }

    public void Add(string itemName, string description)
    {
        foreach (Entry e in entries)
            if (e.name == itemName) { e.count++; return; }
        entries.Add(new Entry { name = itemName, description = description, count = 1 });
    }

    public int Count(string itemName)
    {
        foreach (Entry e in entries)
            if (e.name == itemName) return e.count;
        return 0;
    }

    void OnGUI()
    {
        if (Keyboard.current == null || !Keyboard.current.tabKey.isPressed) return;

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = Color.white;
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            nameStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            textStyle.normal.textColor = Color.white;
        }

        float s = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float screenWidth = Screen.width / s;

        const float width = 620f, row = 30f;
        int rows = Mathf.Max(1, entries.Count);
        float height = 70f + rows * row + (thrower != null ? 120f : 0f);
        float x = (screenWidth - width) / 2f, y = Mathf.Max(20f, (720f - height) / 2f);

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(x, y + 10f, width, 36f), "ITEMS", titleStyle);
        y += 56f;
        if (entries.Count == 0) GUI.Label(new Rect(x + 24f, y, width - 48f, row), "Nothing yet.", textStyle);
        foreach (Entry e in entries)
        {
            GUI.Label(new Rect(x + 24f, y, 60f, row), "x" + e.count, nameStyle);
            GUI.Label(new Rect(x + 84f, y, 170f, row), e.name, nameStyle);
            GUI.Label(new Rect(x + 260f, y, width - 284f, row), e.description, textStyle);
            y += row;
        }
        y += Mathf.Max(0, 1 - entries.Count) * row;

        if (thrower == null) return;
        y += 14f;
        float speed = controller != null ? controller.movementFactor : 1f;
        GUI.Label(new Rect(x + 24f, y, width - 48f, row),
            $"Throws per second {1f / Mathf.Max(0.01f, thrower.cooldown):0.0}     Damage x{thrower.damageMultiplier:0.00}     Move speed x{speed:0.00}", textStyle);
        GUI.Label(new Rect(x + 24f, y + row, width - 48f, row),
            $"Tentacle shots {thrower.extraShots}     Extra ricochets {thrower.extraBounces}     Box holds {thrower.magazineSize}", textStyle);
        GUI.Label(new Rect(x + 24f, y + row * 2f, width - 48f, row),
            $"Pierce {thrower.pierce}     Burn {thrower.burnDamage:0} per second", textStyle);
    }
}
