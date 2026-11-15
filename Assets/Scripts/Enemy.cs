using System.Collections.Generic;
using UnityEngine;

public enum EnemyType { Normal, Heavy, Flying, Swarm }

public class Enemy
{
    public static readonly List<Enemy> All = new();
    public static Transform Target;

    public static float HealthScale = 1f, DamageScale = 1f, SpeedScale = 1f;

    public EnemyType type;
    public bool elite, boss;
    public float maxHealth, health, speed, attackDamage;
    public float scale = 1f, radius, height, hitRadius;
    public Vector3 position;
    public float yaw;

    public System.Action<Enemy> Died;

    public bool IsDead => health <= 0f;
    public Vector3 Centre => type == EnemyType.Flying ? position : position + Vector3.up * (height * 0.5f);

    internal Vector3 knock;
    internal float knockResistance, attackTimer, flashTimer, burnDamage, burnTimer, phase, groundY, groundTimer, throwTimer;

    const float EliteScale = 1.35f, EliteHealth = 3f, EliteSpeed = 1.15f, EliteDamage = 1.5f;
    const float BossScale = 1.7f, BossHealth = 8f;

    public static float BaseHeight(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Heavy: return 3.2f;
            case EnemyType.Flying: return 1.1f;
            case EnemyType.Swarm: return 0.7f;
            default: return 1.5f;
        }
    }

    public static Enemy Spawn(EnemyType type, bool elite, Vector3 position, bool boss = false)
    {
        EnemyHorde.Ensure();
        var e = new Enemy { type = type, elite = elite, boss = boss, position = position, groundY = position.y };
        float s = (elite ? EliteScale : 1f) * (boss ? BossScale : 1f);
        if (type == EnemyType.Normal) s *= Random.Range(0.75f, 1.3f);
        e.scale = s;
        e.height = BaseHeight(type) * s;

        switch (type)
        {
            case EnemyType.Heavy:
                e.maxHealth = 80f; e.speed = 2.6f; e.attackDamage = 25f; e.knockResistance = 0.8f;
                e.radius = 0.8f * s; e.hitRadius = 1.3f * s;
                break;
            case EnemyType.Flying:
                e.maxHealth = 10f; e.speed = 6.5f; e.attackDamage = 6f;
                e.radius = 0.5f * s; e.hitRadius = 0.65f * s;
                break;
            case EnemyType.Swarm:
                e.maxHealth = 6f; e.speed = 8.5f; e.attackDamage = 4f;
                e.radius = 0.3f * s; e.hitRadius = 0.55f * s;
                break;
            default:
                e.maxHealth = 20f; e.speed = 5f; e.attackDamage = 10f;
                e.radius = 0.5f * s; e.hitRadius = 0.85f * s;
                break;
        }

        if (elite) { e.maxHealth *= EliteHealth; e.speed *= EliteSpeed; e.attackDamage *= EliteDamage; }
        if (boss) { e.maxHealth *= BossHealth; e.attackDamage *= 1.5f; e.knockResistance = 1f; }
        e.maxHealth *= HealthScale;
        e.attackDamage *= DamageScale;
        e.speed *= SpeedScale * Random.Range(0.9f, 1.1f);
        e.health = e.maxHealth;
        e.phase = Random.Range(0f, 6f);
        e.groundTimer = Random.Range(0f, 0.3f);
        e.throwTimer = Random.Range(1f, 3f);

        All.Add(e);
        return e;
    }

    public void TakeDamage(float amount, Vector3 direction)
    {
        if (IsDead) return;
        health -= amount;
        direction.y = 0f;
        knock = direction.normalized * 9f * (1f - knockResistance);
        flashTimer = 0.08f;
        if (IsDead) Die();
        else Sfx.EnemyHit(type, position);
    }

    public void Ignite(float damagePerSecond, float duration)
    {
        if (IsDead || damagePerSecond <= 0f) return;
        burnDamage = Mathf.Max(burnDamage, damagePerSecond);
        burnTimer = Mathf.Max(burnTimer, duration);
    }

    internal void Die()
    {
        health = 0f;
        Sfx.EnemyDeath(type, position);
        int coins = type == EnemyType.Heavy ? 5 : (type == EnemyType.Normal ? 2 : 1);
        Coin.Spawn(position, coins * (elite ? 3 : 1) * (boss ? 5 : 1));
        Pickup.TryDrop(position, boss ? 1f : (elite ? Pickup.EliteDropChance : Pickup.DropChance));
        Died?.Invoke(this);
    }
}
