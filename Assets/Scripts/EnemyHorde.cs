using System.Collections.Generic;
using UnityEngine;

public class EnemyHorde : MonoBehaviour
{
    const float CellSize = 4f;
    const float MaxHitRadius = 4f;
    const int Frames = 8;
    const float DrawDistance = 220f, BarDistance = 55f;

    static EnemyHorde instance;

    public static void Ensure()
    {
        if (instance == null) instance = new GameObject("Enemy Horde").AddComponent<EnemyHorde>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        Enemy.All.Clear();
        Enemy.Target = null;
        Enemy.HealthScale = Enemy.DamageScale = Enemy.SpeedScale = 1f;
    }

    void OnDestroy() { Enemy.All.Clear(); }

    readonly Dictionary<int, List<Enemy>> cells = new();
    readonly List<List<Enemy>> usedCells = new();

    static int Key(int cx, int cz) => (cx * 73856093) ^ (cz * 19349663);
    static int CellOf(float v) => Mathf.FloorToInt(v / CellSize);

    void RebuildGrid()
    {
        foreach (List<Enemy> list in usedCells) list.Clear();
        usedCells.Clear();
        foreach (Enemy e in Enemy.All)
        {
            int key = Key(CellOf(e.position.x), CellOf(e.position.z));
            if (!cells.TryGetValue(key, out List<Enemy> list)) cells[key] = list = new List<Enemy>(8);
            if (list.Count == 0) usedCells.Add(list);
            list.Add(e);
        }
    }

    public static Enemy Sweep(Vector3 origin, Vector3 direction, float distance, float sphereRadius, List<Enemy> ignore, out float hitDistance, out Vector3 normal)
    {
        hitDistance = 0f;
        normal = -direction;
        if (instance == null) return null;

        Vector3 end = origin + direction * distance;
        float pad = sphereRadius + MaxHitRadius;
        int x0 = CellOf(Mathf.Min(origin.x, end.x) - pad), x1 = CellOf(Mathf.Max(origin.x, end.x) + pad);
        int z0 = CellOf(Mathf.Min(origin.z, end.z) - pad), z1 = CellOf(Mathf.Max(origin.z, end.z) + pad);

        Enemy best = null;
        float bestT = distance;
        for (int cx = x0; cx <= x1; cx++)
        {
            for (int cz = z0; cz <= z1; cz++)
            {
                if (!instance.cells.TryGetValue(Key(cx, cz), out List<Enemy> list)) continue;
                foreach (Enemy e in list)
                {
                    if (e.IsDead || (ignore != null && ignore.Contains(e))) continue;
                    Vector3 centre = e.Centre;
                    Vector3 oc = origin - centre;
                    float r = e.hitRadius + sphereRadius;
                    float b = Vector3.Dot(oc, direction);
                    float c = oc.sqrMagnitude - r * r;
                    float t;
                    if (c <= 0f)
                    {
                        if (b >= 0f) continue;
                        t = 0f;
                    }
                    else
                    {
                        if (b > 0f) continue;
                        float disc = b * b - c;
                        if (disc < 0f) continue;
                        t = -b - Mathf.Sqrt(disc);
                    }
                    if (t > bestT) continue;
                    bestT = t;
                    best = e;
                    Vector3 n = origin + direction * t - centre;
                    normal = n.sqrMagnitude > 0.0001f ? n.normalized : -direction;
                }
            }
        }
        hitDistance = bestT;
        return best;
    }

    public static int DamageInRadius(Vector3 point, float radius, float damage, Enemy except = null)
    {
        if (instance == null) return 0;
        float pad = radius + MaxHitRadius;
        int x0 = CellOf(point.x - pad), x1 = CellOf(point.x + pad), z0 = CellOf(point.z - pad), z1 = CellOf(point.z + pad);
        int hits = 0;
        for (int cx = x0; cx <= x1; cx++)
        {
            for (int cz = z0; cz <= z1; cz++)
            {
                if (!instance.cells.TryGetValue(Key(cx, cz), out List<Enemy> list)) continue;
                foreach (Enemy e in list)
                {
                    if (e == except || e.IsDead) continue;
                    Vector3 away = e.Centre - point;
                    float reach = radius + e.hitRadius * 0.5f;
                    if (away.sqrMagnitude > reach * reach) continue;
                    e.TakeDamage(damage, away);
                    hits++;
                }
            }
        }
        return hits;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        List<Enemy> all = Enemy.All;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            if (!all[i].IsDead) continue;
            all[i] = all[all.Count - 1];
            all.RemoveAt(all.Count - 1);
        }
        RebuildGrid();

        Transform target = Enemy.Target;
        if (target == null) return;
        Vector3 targetPosition = target.position;
        PlayerHealth player = PlayerHealth.Instance;
        for (int i = 0; i < all.Count; i++) Simulate(all[i], dt, target, targetPosition, player);
        UpdateBones(dt, target, targetPosition, player);
    }

    void Simulate(Enemy e, float dt, Transform target, Vector3 tp, PlayerHealth player)
    {
        e.attackTimer -= dt;
        if (e.flashTimer > 0f) e.flashTimer -= dt;
        if (e.burnTimer > 0f)
        {
            e.burnTimer -= dt;
            e.health -= e.burnDamage * dt;
            if (e.health <= 0f) { e.Die(); return; }
            if (e.burnTimer <= 0f) e.burnDamage = 0f;
        }
        e.knock = Vector3.MoveTowards(e.knock, Vector3.zero, 25f * dt);

        if (e.type == EnemyType.Flying) { Fly(e, dt, tp, player); return; }

        Vector3 to = tp - e.position;
        float heightGap = to.y;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance > 130f) { Relocate(e, tp); return; }

        Vector3 move = e.knock;
        if (distance > 0.01f)
        {
            Vector3 direction = to / distance;
            e.yaw = Mathf.MoveTowardsAngle(e.yaw, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 480f * dt);
            if (distance > e.radius + 0.8f) move += direction * e.speed;
            else if (Mathf.Abs(heightGap) < e.height + 0.6f) Attack(e, player);
        }

        move.y = 0f;
        e.position += move * dt;
        Separate(e);

        e.groundTimer -= dt;
        if (e.groundTimer <= 0f)
        {
            e.groundTimer = 0.22f + (e.phase % 1f) * 0.1f;
            if (Physics.Raycast(e.position + Vector3.up * 11f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)
                && !(hit.collider is CapsuleCollider) && !hit.collider.transform.IsChildOf(target))
                e.groundY = hit.point.y;
        }
        e.position.y = Mathf.MoveTowards(e.position.y, e.groundY, (e.groundY > e.position.y ? 12f : 25f) * dt);

        if (e.type == EnemyType.Heavy)
        {
            e.throwTimer -= dt;
            if (e.throwTimer <= 0f && distance > BoneMinRange && distance < BoneMaxRange && bones.Count < MaxBones)
            {
                e.throwTimer = Random.Range(3f, 4.5f) * (e.boss ? 0.35f : 1f);
                Vector3 from = e.position + Vector3.up * (e.height * 0.75f);
                Vector3 aim = tp + Vector3.up - from;
                bones.Add(new Bone { position = from, velocity = aim.normalized * BoneSpeed, life = 3.5f, damage = e.attackDamage * 0.4f, spin = e.phase * 60f });
            }
        }
    }

    struct Bone { public Vector3 position, velocity; public float life, damage, spin; }

    const float BoneSpeed = 16f, BoneMinRange = 6f, BoneMaxRange = 34f;
    const int MaxBones = 300;
    readonly List<Bone> bones = new();
    readonly InstanceList boneInstances = new();
    Mesh boneMesh;
    Material boneMaterial;

    void UpdateBones(float dt, Transform target, Vector3 tp, PlayerHealth player)
    {
        Vector3 chest = tp + Vector3.up;
        for (int i = bones.Count - 1; i >= 0; i--)
        {
            Bone b = bones[i];
            Vector3 step = b.velocity * dt;
            b.life -= dt;

            bool gone = b.life <= 0f;
            if (!gone && (b.position + step - chest).sqrMagnitude < 0.9f * 0.9f)
            {
                if (player != null) player.TakeDamage(b.damage);
                gone = true;
            }
            if (!gone && Physics.Raycast(b.position, step.normalized, out RaycastHit hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(target))
                gone = true;

            if (gone)
            {
                bones[i] = bones[bones.Count - 1];
                bones.RemoveAt(bones.Count - 1);
                continue;
            }
            b.position += step;
            bones[i] = b;
        }
    }

    void Fly(Enemy e, float dt, Vector3 tp, PlayerHealth player)
    {
        Vector3 offset = Quaternion.Euler(0f, e.phase * 60f, 0f) * Vector3.forward * (1.3f + e.phase % 1f);
        Vector3 goal = tp + offset + Vector3.up * (1.5f + e.phase % 1f);
        if ((goal - e.position).sqrMagnitude > 130f * 130f) { Relocate(e, tp); return; }

        e.position = Vector3.MoveTowards(e.position, goal, e.speed * dt) + e.knock * dt;
        Vector3 look = tp - e.position;
        if (look.x * look.x + look.z * look.z > 0.01f)
            e.yaw = Mathf.MoveTowardsAngle(e.yaw, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, 480f * dt);

        Separate(e);
        if ((tp + Vector3.up - e.position).sqrMagnitude < 2.6f * 2.6f) Attack(e, player);
    }

    void Separate(Enemy e)
    {
        bool flying = e.type == EnemyType.Flying;
        int cx = CellOf(e.position.x), cz = CellOf(e.position.z), checks = 0;
        float pushX = 0f, pushZ = 0f;

        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oz = -1; oz <= 1; oz++)
            {
                if (!cells.TryGetValue(Key(cx + ox, cz + oz), out List<Enemy> list)) continue;
                for (int j = 0; j < list.Count && checks < 48; j++)
                {
                    Enemy o = list[j];
                    if (o == e || o.IsDead || (o.type == EnemyType.Flying) != flying) continue;
                    checks++;
                    float dx = e.position.x - o.position.x, dz = e.position.z - o.position.z;
                    float d2 = dx * dx + dz * dz, min = e.radius + o.radius;
                    if (d2 >= min * min) continue;
                    if (d2 < 0.0001f)
                    {
                        pushX += Mathf.Cos(e.phase) * 0.1f;
                        pushZ += Mathf.Sin(e.phase) * 0.1f;
                        continue;
                    }
                    float d = Mathf.Sqrt(d2);
                    float share = o.radius / min;
                    pushX += dx / d * (min - d) * share;
                    pushZ += dz / d * (min - d) * share;
                }
            }
        }

        float length = Mathf.Sqrt(pushX * pushX + pushZ * pushZ);
        if (length < 0.0001f) return;
        float limit = Mathf.Min(length, 0.6f) / length;
        e.position.x += pushX * limit;
        e.position.z += pushZ * limit;
    }

    static void Attack(Enemy e, PlayerHealth player)
    {
        if (e.attackTimer > 0f || player == null) return;
        e.attackTimer = 1f;
        player.TakeDamage(e.attackDamage);
    }

    static void Relocate(Enemy e, Vector3 tp)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        e.position = tp + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(45f, 60f);
        if (e.type == EnemyType.Flying) e.position.y += 2f;
        e.groundY = e.position.y;
        e.groundTimer = 0f;
    }

    class InstanceList
    {
        public Matrix4x4[] data = new Matrix4x4[128];
        public int count;
        public void Add(in Matrix4x4 m)
        {
            if (count == data.Length) System.Array.Resize(ref data, data.Length * 2);
            data[count++] = m;
        }
    }

    class Part { public Mesh[] frames; public Matrix4x4 matrix = Matrix4x4.identity; public bool identity = true; public Material[] materials, eliteMaterials; }

    class Visual
    {
        public readonly List<Part> parts = new();
        public Matrix4x4 fit = Matrix4x4.identity;
        public int frameCount = 1;
        public float animationSpeed = 1f;
        public InstanceList[,] lists;
    }

    readonly Visual[] visuals = new Visual[4];
    Material flashMaterial, barBack, barRed, barGold, barOrange;
    Mesh quad;
    readonly InstanceList backBars = new(), redBars = new(), goldBars = new(), orangeBars = new();
    Matrix4x4[] scratch = new Matrix4x4[128];
    static readonly Bounds everywhere = new Bounds(Vector3.zero, Vector3.one * 100000f);
    static readonly Color gold = new Color(1f, 0.78f, 0.15f);

    void Awake()
    {
        flashMaterial = Unlit(Color.white);
        barBack = Unlit(new Color(0.05f, 0.05f, 0.05f));
        barRed = Unlit(new Color(0.9f, 0.2f, 0.2f));
        barGold = Unlit(new Color(1f, 0.8f, 0.2f));
        barOrange = Unlit(new Color(1f, 0.5f, 0.1f));
        quad = PrimitiveMesh(PrimitiveType.Quad);
        boneMesh = PrimitiveMesh(PrimitiveType.Cube);
        boneMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.94f, 0.91f, 0.82f), enableInstancing = true };

        var set = Resources.Load<EnemyVisualSet>("EnemyVisuals");
        if (set != null && set.entries != null)
            foreach (EnemyVisualSet.Entry entry in set.entries)
                if (entry != null && entry.model != null && (int)entry.type < visuals.Length)
                    visuals[(int)entry.type] = Bake(entry);

        for (int i = 0; i < visuals.Length; i++)
            if (visuals[i] == null) visuals[i] = Fallback((EnemyType)i);
    }

    static Material Unlit(Color color)
    {
        var template = Resources.Load<Material>("HordeUnlit");
        Material m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.color = color;
        m.enableInstancing = true;
        return m;
    }

    static Mesh PrimitiveMesh(PrimitiveType type)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
        Destroy(go);
        return mesh;
    }

    Visual Bake(EnemyVisualSet.Entry entry)
    {
        var visual = new Visual { animationSpeed = entry.animationSpeed, frameCount = entry.clip != null ? Frames : 1 };
        GameObject go = Instantiate(entry.model, Vector3.zero, Quaternion.identity);
        foreach (Animator a in go.GetComponentsInChildren<Animator>()) a.enabled = false;
        var skinned = go.GetComponentsInChildren<SkinnedMeshRenderer>();
        var solid = go.GetComponentsInChildren<MeshFilter>();

        foreach (SkinnedMeshRenderer r in skinned) visual.parts.Add(NewPart(r.sharedMaterials, visual.frameCount));
        foreach (MeshFilter f in solid)
        {
            var renderer = f.GetComponent<Renderer>();
            Part part = NewPart(renderer != null ? renderer.sharedMaterials : new Material[0], visual.frameCount);
            for (int i = 0; i < visual.frameCount; i++) part.frames[i] = f.sharedMesh;
            part.matrix = f.transform.localToWorldMatrix;
            visual.parts.Add(part);
        }

        for (int frame = 0; frame < visual.frameCount; frame++)
        {
            if (entry.clip != null) entry.clip.SampleAnimation(go, entry.clip.length * frame / visual.frameCount);
            for (int i = 0; i < skinned.Length; i++)
            {
                var mesh = new Mesh();
                skinned[i].BakeMesh(mesh);
                mesh.RecalculateBounds();
                visual.parts[i].frames[frame] = mesh;
                if (frame == 0) visual.parts[i].matrix = Matrix4x4.TRS(skinned[i].transform.position, skinned[i].transform.rotation, Vector3.one);
            }
        }
        Destroy(go);

        bool any = false;
        Bounds b = default;
        foreach (Part part in visual.parts)
        {
            if (part.frames[0] == null) continue;
            Bounds mb = part.frames[0].bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 p = mb.center + Vector3.Scale(mb.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                p = part.matrix.MultiplyPoint3x4(p);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        if (any && b.size.y > 0.0001f)
        {
            float s = Enemy.BaseHeight(entry.type) / b.size.y;
            float baseY = entry.type == EnemyType.Flying ? b.center.y : b.min.y;
            visual.fit = Matrix4x4.Rotate(Quaternion.Euler(0f, entry.yawOffset, 0f)) * Matrix4x4.Scale(Vector3.one * s)
                       * Matrix4x4.Translate(-new Vector3(b.center.x, baseY, b.center.z));
        }
        Finish(visual);
        return visual;
    }

    static Part NewPart(Material[] source, int frames)
    {
        var part = new Part { frames = new Mesh[frames], materials = new Material[source.Length], eliteMaterials = new Material[source.Length] };
        for (int i = 0; i < source.Length; i++)
        {
            Material original = source[i] != null ? source[i] : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            part.materials[i] = new Material(original) { enableInstancing = true };
            part.eliteMaterials[i] = new Material(original) { enableInstancing = true };
            if (original.HasProperty("_BaseColor") || original.HasProperty("_Color"))
                part.eliteMaterials[i].color = Color.Lerp(original.color, gold, 0.7f);
        }
        return part;
    }

    Visual Fallback(EnemyType type)
    {
        Color color = type == EnemyType.Heavy ? new Color(0.45f, 0.3f, 0.55f) : type == EnemyType.Flying ? new Color(0.25f, 0.25f, 0.35f)
                    : type == EnemyType.Swarm ? new Color(0.5f, 0.42f, 0.35f) : new Color(0.9f, 0.35f, 0.25f);
        var lit = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        Part part = NewPart(new[] { lit }, 1);
        part.frames[0] = PrimitiveMesh(type == EnemyType.Flying ? PrimitiveType.Sphere : PrimitiveType.Capsule);

        var visual = new Visual();
        visual.parts.Add(part);
        float h = Enemy.BaseHeight(type), width = type == EnemyType.Heavy ? 1.6f : h * 0.5f;
        visual.fit = type == EnemyType.Flying
            ? Matrix4x4.Scale(Vector3.one * h)
            : Matrix4x4.Translate(Vector3.up * (h * 0.5f)) * Matrix4x4.Scale(new Vector3(width, h * 0.5f, width));
        Finish(visual);
        return visual;
    }

    static void Finish(Visual visual)
    {
        if (visual.parts.Count == 1)
        {
            visual.fit = visual.fit * visual.parts[0].matrix;
            visual.parts[0].matrix = Matrix4x4.identity;
        }
        foreach (Part part in visual.parts) part.identity = part.matrix == Matrix4x4.identity;

        visual.lists = new InstanceList[3, visual.frameCount];
        for (int v = 0; v < 3; v++)
            for (int f = 0; f < visual.frameCount; f++) visual.lists[v, f] = new InstanceList();
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || (Enemy.All.Count == 0 && bones.Count == 0)) return;
        Vector3 eye = cam.transform.position, forward = cam.transform.forward, right = cam.transform.right;
        Quaternion facing = cam.transform.rotation;
        float time = Time.time;

        foreach (Visual visual in visuals)
            foreach (InstanceList list in visual.lists) list.count = 0;
        backBars.count = redBars.count = goldBars.count = orangeBars.count = 0;

        foreach (Enemy e in Enemy.All)
        {
            if (e.IsDead) continue;
            Vector3 to = e.position - eye;
            float d2 = to.sqrMagnitude;
            if (d2 > DrawDistance * DrawDistance || (d2 > 100f && Vector3.Dot(to, forward) < 0f)) continue;

            Visual visual = visuals[(int)e.type];
            Vector3 position = e.position;
            if (e.type == EnemyType.Flying) position.y += Mathf.Sin(time * 9f + e.phase) * 0.22f * e.scale;
            Matrix4x4 root = Matrix4x4.TRS(position, Quaternion.Euler(0f, e.yaw, 0f), Vector3.one * e.scale);

            int variant = e.flashTimer > 0f ? 2 : (e.elite ? 1 : 0);
            int frame = visual.frameCount > 1 ? (int)(time * 12f * visual.animationSpeed + e.phase * 4f) % visual.frameCount : 0;
            visual.lists[variant, frame].Add(root * visual.fit);

            if (d2 > BarDistance * BarDistance || !(e.elite || e.boss || e.health < e.maxHealth)) continue;
            float width = e.boss ? 3f : (e.elite ? 1.6f : 1.1f), fill = Mathf.Clamp01(e.health / e.maxHealth);
            Vector3 barPosition = position + Vector3.up * ((e.type == EnemyType.Flying ? e.height * 0.5f : e.height) + 0.35f);
            backBars.Add(Matrix4x4.TRS(barPosition, facing, new Vector3(width + 0.06f, 0.2f, 1f)));
            InstanceList fills = e.burnTimer > 0f ? orangeBars : (e.elite ? goldBars : redBars);
            fills.Add(Matrix4x4.TRS(barPosition - right * (width * (1f - fill) * 0.5f) - forward * 0.02f, facing, new Vector3(width * fill, 0.14f, 1f)));
        }

        foreach (Visual visual in visuals)
        {
            for (int variant = 0; variant < 3; variant++)
            {
                for (int frame = 0; frame < visual.frameCount; frame++)
                {
                    InstanceList list = visual.lists[variant, frame];
                    if (list.count == 0) continue;
                    foreach (Part part in visual.parts)
                    {
                        Mesh mesh = part.frames[frame];
                        if (mesh == null) continue;
                        Matrix4x4[] data = list.data;
                        if (!part.identity)
                        {
                            if (scratch.Length < list.count) scratch = new Matrix4x4[list.data.Length];
                            for (int i = 0; i < list.count; i++) scratch[i] = list.data[i] * part.matrix;
                            data = scratch;
                        }
                        Material[] materials = variant == 1 ? part.eliteMaterials : part.materials;
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        {
                            Material m = variant == 2 ? flashMaterial : (materials.Length > 0 ? materials[Mathf.Min(sub, materials.Length - 1)] : flashMaterial);
                            Draw(mesh, sub, m, data, list.count);
                        }
                    }
                }
            }
        }

        boneInstances.count = 0;
        foreach (Bone b in bones)
            boneInstances.Add(Matrix4x4.TRS(b.position, Quaternion.Euler(time * 700f + b.spin, b.spin, 0f), new Vector3(0.16f, 0.16f, 0.8f)));
        Draw(boneMesh, 0, boneMaterial, boneInstances.data, boneInstances.count);

        Draw(quad, 0, barBack, backBars.data, backBars.count);
        Draw(quad, 0, barRed, redBars.data, redBars.count);
        Draw(quad, 0, barGold, goldBars.data, goldBars.count);
        Draw(quad, 0, barOrange, orangeBars.data, orangeBars.count);
    }

    static void Draw(Mesh mesh, int subMesh, Material material, Matrix4x4[] data, int count)
    {
        if (count == 0) return;
        var rp = new RenderParams(material)
        {
            worldBounds = everywhere,
            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows = true,
        };
        for (int start = 0; start < count; start += 1023)
            Graphics.RenderMeshInstanced(rp, mesh, subMesh, data, Mathf.Min(1023, count - start), start);
    }
}
