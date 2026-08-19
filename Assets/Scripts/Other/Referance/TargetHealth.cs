using UnityEngine;

public class TargetHealth : MonoBehaviour
{
    public float health = 50f;

    public void TakeDamage (float amount)
    {
        health -= amount;
        if (health <= 0f)
        {
            Die();
        }
    }
    void Die ()
    {
        Destroy(gameObject);
    }
}
