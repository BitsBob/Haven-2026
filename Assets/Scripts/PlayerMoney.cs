using UnityEngine;

public class PlayerMoney : MonoBehaviour
{
    public static PlayerMoney Instance { get; private set; }

    [SerializeField] int startingMoney;

    public int Money { get; private set; }
    public event System.Action<int> Changed;

    GUIStyle style;

    void Awake()
    {
        Instance = this;
        Money = startingMoney;
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Money += amount;
        Changed?.Invoke(Money);
    }

    public bool CanAfford(int cost) => Money >= cost;

    public bool TrySpend(int cost)
    {
        if (cost < 0 || Money < cost) return false;
        Money -= cost;
        Changed?.Invoke(Money);
        return true;
    }

    void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            style.normal.textColor = new Color(1f, 0.85f, 0.25f);
        }
        float s = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        GUI.Label(new Rect(Screen.width / s - 280f, 48f, 260f, 28f), "$ " + Money, style);
    }
}
