using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] private int maxHP = 100;

    private int currentHP;

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;

    private void Start()
    {
        currentHP = maxHP;
        Debug.Log($"[{gameObject.name}] 생성됨 - HP: {currentHP}/{maxHP}");
    }

    public void TakeDamage(int damage)
    {
        currentHP -= damage;

        Debug.Log(
            $"[{gameObject.name}] 피격! 데미지: {damage} / HP: {currentHP}/{maxHP}"
        );

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"[{gameObject.name}] 사망");
        Destroy(gameObject);
    }
}