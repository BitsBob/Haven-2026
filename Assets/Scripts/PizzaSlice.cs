using System.Collections.Generic;
using UnityEngine;

public class PizzaSlice : MonoBehaviour
{
    public float speed = 30f;
    public float radius = 0.15f;
    public float lifetime = 6f;
    public int maxBounces = 3;
    public float damage = 10f;
    [Tooltip("Extra damage per bounce made before the hit, as a fraction of the base damage.")]
    public float bounceDamageBonus = 0.5f;
    [Tooltip("How many enemies this slice passes straight through before it starts bouncing off them.")]
    public int pierce;
    [Tooltip("Damage per second of the burn this slice leaves on enemies. 0 for none.")]
    public float burnDamage;
    [Tooltip("On hitting an enemy, also hits everything within this many metres for half damage. 0 for none.")]
    public float explodeRadius;

    const float BurnDuration = 3f;

    const float VisualLength = 0.6f;

    public int Bounces { get; private set; }

    Vector3 direction;
    Transform owner, visual;
    bool tentacle;
    readonly List<Enemy> pierced = new();

    static readonly RaycastHit[] hits = new RaycastHit[8];
    static Material discMaterial, trailMaterial;

    public static PizzaSlice Spawn(Vector3 position, Vector3 direction, Transform owner, GameObject model = null, bool tipAtMinX = false, bool tentacle = false)
    {
        var go = new GameObject("Pizza Slice");
        go.transform.position = position;
        var slice = go.AddComponent<PizzaSlice>();
        slice.direction = direction.normalized;
        slice.owner = owner;
        slice.tentacle = tentacle;
        slice.BuildVisual(model, tipAtMinX);
        return slice;
    }

    void BuildVisual(GameObject model, bool tipAtMinX)
    {
        if (trailMaterial == null) trailMaterial = new Material(Shader.Find("Sprites/Default"));

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        visual.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        GameObject tentacleModel = tentacle ? SliceVisual.FindTopping("Tentacle") : null;
        if (tentacleModel != null)
        {
            SliceVisual.CreateLoose(tentacleModel, visual, 0.5f);
        }
        else if (model != null)
        {
            float length = tentacle ? VisualLength * 0.6f : VisualLength;
            Transform slice = SliceVisual.Create(model, visual, length, tipAtMinX);
            slice.localPosition = new Vector3(0f, -0.03f, -length * 0.6f);
        }
        else
        {
            if (discMaterial == null) discMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(1f, 0.7f, 0.15f) };
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(visual, false);
            disc.transform.localScale = new Vector3(0.45f, 0.02f, 0.45f);
            disc.GetComponent<Renderer>().sharedMaterial = discMaterial;
        }

        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = trailMaterial;
        trail.time = 0.25f;
        trail.startWidth = 0.12f;
        trail.endWidth = 0f;
        Color trailColor = tentacle ? new Color(0.75f, 0.35f, 1f) : new Color(1f, 0.9f, 0.6f);
        trail.startColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0.8f);
        trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        lifetime -= dt;
        if (lifetime <= 0f) { Destroy(gameObject); return; }

        float remaining = speed * dt;
        for (int i = 0; i < 4 && remaining > 0f; i++)
        {
            bool wall = Sweep(remaining, out RaycastHit hit);
            Enemy enemy = EnemyHorde.Sweep(transform.position, direction, remaining, radius, pierced, out float enemyDistance, out Vector3 enemyNormal);
            if (enemy != null && wall && hit.distance < enemyDistance) enemy = null;

            if (enemy == null && !wall)
            {
                transform.position += direction * remaining;
                break;
            }

            float travelled = Mathf.Max(0f, (enemy != null ? enemyDistance : hit.distance) - 0.01f);
            transform.position += direction * travelled;
            remaining -= travelled;

            if (enemy != null)
            {
                float dealt = damage * (1f + bounceDamageBonus * Bounces);
                enemy.TakeDamage(dealt, direction);
                if (burnDamage > 0f) enemy.Ignite(burnDamage, BurnDuration);
                if (explodeRadius > 0f)
                {
                    EnemyHorde.DamageInRadius(transform.position, explodeRadius, dealt * 0.5f, enemy);
                    Puff.Spawn(transform.position, explodeRadius);
                }
                if (pierce > 0)
                {
                    pierce--;
                    pierced.Add(enemy);
                    continue;
                }
            }

            if (Bounces >= maxBounces) { Destroy(gameObject); return; }
            Bounces++;
            direction = Vector3.Reflect(direction, enemy != null ? enemyNormal : hit.normal);
        }

        visual.Rotate(tentacle ? 700f * dt : 0f, 900f * dt, 0f, Space.Self);
    }

    bool Sweep(float distance, out RaycastHit best)
    {
        int n = Physics.SphereCastNonAlloc(transform.position, radius, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
        best = default;
        float bestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.distance <= 0f && h.point == Vector3.zero) continue;
            if (owner != null && h.collider.transform.IsChildOf(owner)) continue;
            if (h.distance < bestDistance) { bestDistance = h.distance; best = h; found = true; }
        }
        return found;
    }
}
