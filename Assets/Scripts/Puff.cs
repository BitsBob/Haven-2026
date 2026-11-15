using UnityEngine;

public class Puff : MonoBehaviour
{
    const float Life = 0.22f;
    static Material material;
    float age, diameter;

    public static void Spawn(Vector3 position, float radius)
    {
        if (material == null) material = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.78f, 0.45f, 1f, 0.35f) };
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.name = "Puff";
        go.transform.position = position;
        go.transform.localScale = Vector3.zero;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<Puff>().diameter = radius * 2f;
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Life);
        transform.localScale = Vector3.one * (diameter * (1f - (1f - t) * (1f - t)));
        if (age >= Life) Destroy(gameObject);
    }
}
