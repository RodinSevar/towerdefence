using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A shot flying toward a creep. Projectiles are pooled: <see cref="Spawn"/> reuses an inactive one instead of
/// building a new sphere primitive (and material) for every shot.
/// </summary>
public class Projectile : MonoBehaviour
{
    private static readonly Stack<Projectile> pool = new Stack<Projectile>();
    private static readonly List<Projectile> flying = new List<Projectile>(); // in flight (deterministic order: swap-removal on hit)

    private Enemy target;
    private float speed;
    private float damage;
    private StatusEffect payloadEffect;
    private int owner;
    private Transform tr;
    private Renderer rend;
    private int flyingIndex;

    public static void Spawn(Vector3 position, float scale, Color color, Enemy targetEnemy, float travelSpeed,
        float hitDamage, StatusEffect effect, int ownerId)
    {
        Projectile p = null;
        while (pool.Count > 0 && p == null) p = pool.Pop(); // skip entries destroyed by a scene reload
        if (p == null) p = Create();

        p.tr.position = position;
        p.tr.localScale = Vector3.one * scale;
        p.rend.material.color = color;
        p.target = targetEnemy;
        p.speed = travelSpeed;
        p.damage = hitDamage;
        p.payloadEffect = effect;
        p.owner = ownerId;
        p.gameObject.SetActive(true);
        p.flyingIndex = flying.Count;
        flying.Add(p);
    }

    private static Projectile Create()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Projectile";
        Destroy(go.GetComponent<Collider>()); // no collider: it must not block rays
        Projectile p = go.AddComponent<Projectile>();
        p.tr = go.transform;
        p.rend = go.GetComponent<Renderer>();
        return p;
    }

    /// <summary>Moves every projectile one tick (called by <see cref="Simulation"/>).</summary>
    public static void SimTickAll(float dt)
    {
        for (int i = flying.Count - 1; i >= 0; i--)
        {
            Projectile p = flying[i];
            if (p == null) { RemoveFlying(i); continue; } // destroyed by a scene reload
            p.Step(dt);
        }
    }

    private static void RemoveFlying(int i)
    {
        int last = flying.Count - 1;
        if (i != last) { flying[i] = flying[last]; if (flying[i] != null) flying[i].flyingIndex = i; }
        flying.RemoveAt(last);
    }

    private void Step(float dt)
    {
        // If the target dies while the shot is in flight, recycle it
        if (target == null || !target.IsAlive)
        {
            Release();
            return;
        }

        Vector3 targetPos = target.AimPoint;
        Vector3 toTarget = targetPos - tr.position;
        float distanceThisTick = speed * dt;

        if (toTarget.sqrMagnitude <= distanceThisTick * distanceThisTick)
        {
            HitTarget();
            return;
        }

        tr.position += toTarget.normalized * distanceThisTick;
    }

    private void HitTarget()
    {
        target.TakeDamage(damage, owner);
        if (payloadEffect != null)
        {
            target.ApplyStatusEffect(payloadEffect);
        }
        Release();
    }

    private void Release()
    {
        RemoveFlying(flyingIndex);
        target = null;
        payloadEffect = null;
        gameObject.SetActive(false);
        pool.Push(this);
    }
}
