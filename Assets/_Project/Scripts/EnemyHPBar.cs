using UnityEngine;
using UnityEngine.UI;

public class EnemyHPBar : MonoBehaviour
{
    [SerializeField] private Enemy enemy;
    [SerializeField] private Image fill;

    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (enemy == null)
            return;

        fill.fillAmount =
            (float)enemy.CurrentHP / enemy.MaxHP;

        // HP바가 카메라를 바라보도록 회전
        if (mainCamera != null)
        {
            transform.LookAt(mainCamera.transform);
        }
    }
}