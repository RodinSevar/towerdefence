using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Enemy target;
    private float speed;
    private float damage;
    private bool initialized = false;

    public void Initialize(Enemy targetEnemy, float travelSpeed, float hitDamage)
    {
        target = targetEnemy;
        speed = travelSpeed;
        damage = hitDamage;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;

        // If target dies while bullet is in flight, self destruct
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // Aim at target's center, not its feet
        Vector3 targetPos = target.transform.position + Vector3.up * 0.4f;
        
        Vector3 direction = (targetPos - transform.position).normalized;
        float distanceThisFrame = speed * Time.deltaTime;

        if (Vector3.Distance(transform.position, targetPos) <= distanceThisFrame)
        {
            HitTarget();
            return;
        }

        transform.Translate(direction * distanceThisFrame, Space.World);
        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void HitTarget()
    {
        if (target != null)
        {
            target.TakeDamage(damage);
        }
        
        // Add impact particle effect here later if desired
        
        Destroy(gameObject);
    }
}
