using UnityEngine;

public class Coin : MonoBehaviour
{
    public int value = 1;

    const float MagnetRange = 5f, CollectRange = 1.2f;
    const int MaxCoins = 250;
    static int alive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { alive = 0; }

    void OnEnable() { alive++; }
    void OnDisable() { alive--; }

    Vector3 restPosition;
    static Material material;

    public static Coin Spawn(Vector3 position, int value)
    {
        if (alive >= MaxCoins)
        {
            if (PlayerMoney.Instance != null) PlayerMoney.Instance.Add(value);
            return null;
        }

        if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            position = hit.point;

        var go = new GameObject("Coin");
        go.transform.position = position + Vector3.up * 0.5f + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
        var coin = go.AddComponent<Coin>();
        coin.value = value;
        coin.restPosition = go.transform.position;

        float size = Mathf.Lerp(0.3f, 0.6f, Mathf.InverseLerp(1f, 15f, value));
        if (material == null) material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(1f, 0.82f, 0.2f) };
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(disc.GetComponent<Collider>());
        disc.transform.SetParent(go.transform, false);
        disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        disc.transform.localScale = new Vector3(size, 0.03f, size);
        disc.GetComponent<Renderer>().sharedMaterial = material;
        return coin;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        transform.Rotate(0f, 240f * dt, 0f, Space.World);

        PlayerHealth player = PlayerHealth.Instance;
        if (player == null || player.IsDead) return;

        Vector3 chest = player.transform.position + Vector3.up;
        float distance = Vector3.Distance(restPosition, chest);
        if (distance < CollectRange)
        {
            if (PlayerMoney.Instance != null) PlayerMoney.Instance.Add(value);
            Destroy(gameObject);
            return;
        }
        if (distance < MagnetRange) restPosition = Vector3.MoveTowards(restPosition, chest, 16f * dt);
        transform.position = restPosition;
    }
}
