using UnityEngine;

public class FpsCounter : MonoBehaviour
{
    const float Interval = 0.5f;

    GUIStyle style;
    string text = "";
    int frames;
    float elapsed;

    void Update()
    {
        frames++;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed < Interval) return;
        text = $"{Mathf.RoundToInt(frames / elapsed)} FPS";
        frames = 0;
        elapsed = 0f;
    }

    void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            style.normal.textColor = Color.white;
        }
        GUI.Label(new Rect(10, 8, 200, 28), text, style);
    }
}
