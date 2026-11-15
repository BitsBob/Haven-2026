using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public static int Wave { get; private set; }

    [Header("Waves")]
    [Tooltip("Points to spend on enemies in wave 1. A slime costs 2, so 40 is 20 enemies.")]
    [SerializeField] int waveOnePoints = 40;
    [Tooltip("Each wave's points are the last wave's times this. 1.35 reaches about 600 by wave 10 and 2,700 by wave 15.")]
    [SerializeField] float growthPerWave = 1.35f;
    [Tooltip("Roughly how long a wave takes to finish arriving.")]
    [SerializeField] float arrivalSeconds = 22f;
    [Tooltip("Hard cap on enemies alive at once. Spawning pauses at the cap and resumes as they die.")]
    [SerializeField] int maxAlive = 3000;
    [Tooltip("Seconds before the first wave.")]
    [SerializeField] float firstWaveDelay = 3f;
    [Tooltip("Seconds of rest between waves, for spending money.")]
    [SerializeField] float breakTime = 12f;
    [Tooltip("A boss arrives every this many waves. 0 for never.")]
    [SerializeField] int bossEvery = 5;
    [Tooltip("Money for clearing a wave: this much, plus the same again times the wave number.")]
    [SerializeField] int clearBonus = 5;

    [Header("Packs")]
    [Tooltip("Enemies arrive in packs of one type. Smallest and largest pack.")]
    [SerializeField] int packMin = 4;
    [SerializeField] int packMax = 10;
    [Tooltip("Skeletons always arrive as a horde of this many, whatever the wave budget has left.")]
    [SerializeField] int skeletonHordeMin = 10;
    [SerializeField] int skeletonHordeMax = 18;
    [Tooltip("How far from the player packs appear.")]
    [SerializeField] float spawnNear = 26f;
    [SerializeField] float spawnFar = 48f;

    [Header("Scaling per wave")]
    [Tooltip("Enemy health grows by this fraction of its base every wave. Kept low because the numbers grow so fast.")]
    [SerializeField] float healthPerWave = 0.06f;
    [SerializeField] float damagePerWave = 0.05f;
    [Tooltip("Speed growth per wave, capped at +40%.")]
    [SerializeField] float speedGrowth = 0.02f;
    [Range(0f, 1f)] [SerializeField] float eliteChanceStart = 0.04f;
    [Range(0f, 1f)] [SerializeField] float eliteChancePerWave = 0.02f;
    [Range(0f, 1f)] [SerializeField] float eliteChanceMax = 0.35f;

    [Header("Drops")]
    [Tooltip("Pickups an enemy can leave behind when it dies. Bosses always drop one.")]
    [SerializeField] Pickup[] pickupPrefabs;
    [Tooltip("Chance an ordinary enemy drops an item. Small, because there are so many of them.")]
    [Range(0f, 1f)] [SerializeField] float dropChancePerKill = 0.03f;
    [Tooltip("Chance an elite drops an item.")]
    [Range(0f, 1f)] [SerializeField] float dropChancePerElite = 0.35f;

    [Header("Chests")]
    [Tooltip("New chests placed around the player each time a wave is cleared.")]
    [SerializeField] Chest chestPrefab;
    [SerializeField] int newChestsPerWave = 3;
    [SerializeField] float chestRange = 70f;

    enum Phase { Break, Spawning, Clearing }
    Phase phase = Phase.Break;
    float timer, bannerUntil, owed, pointsPerSecond;
    int budget;
    bool bossPending;
    GUIStyle bigStyle, smallStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Wave = 0; }

    void Start()
    {
        Wave = 0;
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) Enemy.Target = player.transform;
        Pickup.DropTable = pickupPrefabs;
        Pickup.DropChance = dropChancePerKill;
        Pickup.EliteDropChance = dropChancePerElite;
        timer = firstWaveDelay;
    }

    void Update()
    {
        if (Enemy.Target == null) return;
        timer -= Time.deltaTime;

        switch (phase)
        {
            case Phase.Break:
                if (timer <= 0f) StartWave();
                break;

            case Phase.Spawning:
                if (budget <= 0 && !bossPending) { phase = Phase.Clearing; break; }
                owed = Mathf.Min(owed + pointsPerSecond * Time.deltaTime, 400f);
                for (int packs = 0; packs < 6 && (owed >= 1f || bossPending) && Enemy.All.Count < maxAlive; packs++)
                {
                    int spent = SpawnPack();
                    if (spent <= 0) break;
                    owed -= spent;
                    budget -= spent;
                    if (budget <= 0) break;
                }
                break;

            case Phase.Clearing:
                if (Enemy.All.Count > 0) break;
                if (PlayerMoney.Instance != null) PlayerMoney.Instance.Add(clearBonus + clearBonus * Wave);
                Sfx.WaveVictory();
                for (int i = 0; i < newChestsPerWave; i++) SpawnChest();
                phase = Phase.Break;
                timer = breakTime;
                break;
        }
    }

    void StartWave()
    {
        Wave++;
        phase = Phase.Spawning;
        bannerUntil = Time.time + 2.5f;

        bossPending = bossEvery > 0 && Wave % bossEvery == 0;
        budget = Mathf.RoundToInt(waveOnePoints * Mathf.Pow(growthPerWave, Wave - 1));
        pointsPerSecond = budget / Mathf.Max(1f, arrivalSeconds);
        owed = Mathf.Min(budget, 12f);

        int n = Wave - 1;
        Enemy.HealthScale = 1f + healthPerWave * n;
        Enemy.DamageScale = 1f + damagePerWave * n;
        Enemy.SpeedScale = Mathf.Min(1.4f, 1f + speedGrowth * n);
    }

    int SpawnPack()
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 spot = Enemy.Target.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(spawnNear, spawnFar);

            if (!Physics.Raycast(spot + Vector3.up * 40f, Vector3.down, out RaycastHit hit, 90f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (hit.normal.y < 0.7f || hit.point.y > Enemy.Target.position.y + 14f) continue;
            if (hit.collider.transform.IsChildOf(Enemy.Target)) continue;

            if (bossPending)
            {
                bossPending = false;
                Enemy.Spawn(EnemyType.Heavy, true, hit.point, true);
                return 1;
            }

            EnemyType type = PickType();
            int cost = Cost(type);
            int count = Mathf.Clamp(Mathf.Max(1, budget) / cost, 1, Random.Range(packMin, packMax + 1));
            if (type == EnemyType.Heavy) count = Random.Range(skeletonHordeMin, skeletonHordeMax + 1);
            bool eliteLeader = Random.value < Mathf.Min(eliteChanceMax, eliteChanceStart + eliteChancePerWave * (Wave - 1));

            int spent = 0;
            for (int i = 0; i < count; i++)
            {
                Vector2 jitter = Random.insideUnitCircle * (type == EnemyType.Heavy ? 9f : 4f);
                Vector3 position = hit.point + new Vector3(jitter.x, type == EnemyType.Flying ? 4f : 0f, jitter.y);
                bool elite = eliteLeader && i == 0;
                Enemy.Spawn(type, elite, position);
                spent += cost * (elite ? 3 : 1);
            }
            return spent;
        }
        return 0;
    }

    static int Cost(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Heavy: return 4;
            case EnemyType.Swarm: return 1;
            default: return 2;
        }
    }

    EnemyType PickType()
    {
        float flying = Wave >= 2 ? 18f : 0f, heavy = Wave >= 2 ? 14f : 0f;
        float roll = Random.value * (72f + flying + heavy);
        if (roll < 72f) return EnemyType.Normal;
        return roll < 72f + flying ? EnemyType.Flying : EnemyType.Heavy;
    }

    void SpawnChest()
    {
        if (chestPrefab == null) return;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 spot = Enemy.Target.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(15f, chestRange);
            if (!Physics.Raycast(spot + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (hit.normal.y < 0.9f || hit.collider.GetComponentInParent<Chest>() != null) continue;
            Instantiate(chestPrefab, hit.point, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            return;
        }
    }

    void OnGUI()
    {
        if (bigStyle == null)
        {
            bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bigStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(bigStyle) { fontSize = 20 };
        }
        float s = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float width = Screen.width / s;

        string status;
        if (phase == Phase.Break)
            status = Wave == 0 ? $"First wave in {Mathf.CeilToInt(Mathf.Max(0f, timer))}" : $"Wave {Wave} cleared.  Next wave in {Mathf.CeilToInt(Mathf.Max(0f, timer))}";
        else
            status = $"Wave {Wave}     Enemies: {Enemy.All.Count}{(phase == Phase.Spawning ? "+" : "")}";
        GUI.Label(new Rect(0f, 14f, width, 30f), status, smallStyle);

        if (Time.time < bannerUntil)
            GUI.Label(new Rect(0f, 150f, width, 60f), bossEvery > 0 && Wave % bossEvery == 0 ? $"WAVE {Wave}: BOSS" : $"WAVE {Wave}", bigStyle);
    }
}
