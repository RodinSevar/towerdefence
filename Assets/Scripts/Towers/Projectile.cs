using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A shot flying toward a creep. Projectiles are pooled: <see cref="Spawn"/> reuses an inactive one instead of
/// building a new sphere primitive (and material) for every shot.
/// </summary>
public class Projectile : MonoBehaviour
{
    private static readonly Stack<Projectile> pool = new Stack<Projectile>();

    private Enemy target;
    private float speed;
    private float damage;
    private StatusEffect payloadEffect;
    private Transform tr;
    private Renderer rend;

    public static void Spawn(Vector3 position, float scale, Color color, Enemy targetEnemy, float travelSpeed,
        float hitDamage, StatusEffect effect)
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
        p.gameObject.SetActive(true);
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

    private void Update()
    {
        // If the target dies while the shot is in flight, recycle it
        if (target == null || !target.IsAlive)
        {
            Release();
            return;
        }

        Vector3 targetPos = target.AimPoint;
        Vector3 toTarget = targetPos - tr.position;
        float distanceThisFrame = speed * Time.deltaTime;

        if (toTarget.sqrMagnitude <= distanceThisFrame * distanceThisFrame)
        {
            HitTarget();
            return;
        }

        tr.position += toTarget.normalized * distanceThisFrame;
    }

    private void HitTarget()
    {
        target.TakeDamage(damage);
        if (payloadEffect != null)
        {
            target.ApplyStatusEffect(payloadEffect);
        }
        Release();
    }

    private void Release()
    {
        target = null;
        payloadEffect = null;
        gameObject.SetActive(false);
        pool.Push(this);
    }
}
