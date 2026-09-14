using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Camera")]
    [SerializeField] private CameraController cameraController;

    [Header("Attack")]
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private float attackAngle = 60f;
    [SerializeField] private int attackDamage = 10;

    [Header("Target")]
    [SerializeField] private float targetRange = 10f;

    [Header("Game Manager")]
    [SerializeField] private GameManager gameManager;

    private Rigidbody rb;

    private Vector2 moveInput;
    private Vector2 lookInput;

    private Enemy currentTarget;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (cameraController != null)
        {
            cameraController.SetLookInput(lookInput);
        }
    }

    private void FixedUpdate()
    {
        if (cameraController == null)
            return;

        Vector3 forward = cameraController.GetCameraForward();
        Vector3 right = cameraController.GetCameraRight();

        Vector3 moveDirection =
            forward * moveInput.y +
            right * moveInput.x;

        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = rb.linearVelocity.y;

        rb.linearVelocity = velocity;

        // 타겟 고정 중이면 타겟을 바라봄
        if (currentTarget != null)
        {
            Vector3 targetDirection =
                currentTarget.transform.position - transform.position;

            targetDirection.y = 0f;

            if (targetDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(targetDirection);

                rb.MoveRotation(
                    Quaternion.Slerp(
                        rb.rotation,
                        targetRotation,
                        rotationSpeed * Time.fixedDeltaTime
                    )
                );
            }
        }
        // 타겟 고정이 아니면 이동 방향을 바라봄
        else if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(moveDirection);

            rb.MoveRotation(
                Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    rotationSpeed * Time.fixedDeltaTime
                )
            );
        }
    }

    // 이동
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    // 카메라
    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    // 기본 공격
    public void OnAttack(InputValue value)
    {
        if (!value.isPressed)
            return;

        // 타겟 고정 중
        if (currentTarget != null)
        {
            AttackTarget(currentTarget);
            return;
        }

        // 타겟 고정이 아니면 바라보는 방향의 적을 찾음
        Enemy target = FindAttackTarget();

        if (target != null)
        {
            currentTarget = target;
            AttackTarget(target);
        }
        else
        {
            Debug.Log("공격할 적이 없음");
        }
    }

    // 타겟 고정
    public void OnTarget(InputValue value)
    {
        if (!value.isPressed)
            return;

        // 이미 타겟이 있으면 해제
        if (currentTarget != null)
        {
            Debug.Log($"타겟 해제: {currentTarget.name}");
            currentTarget = null;
            return;
        }

        Enemy target = FindClosestEnemy();

        if (target != null)
        {
            currentTarget = target;
            Debug.Log($"타겟 고정: {currentTarget.name}");
        }
        else
        {
            Debug.Log("타겟으로 지정할 적이 없음");
        }
    }

    public void UseDiceDamage(int damage)
    {
        if (currentTarget == null)
        {
            Debug.Log("주사위를 사용할 타겟이 없습니다.");
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            currentTarget.transform.position
        );

        if (distance > attackRange)
        {
            Debug.Log("타겟이 너무 멉니다.");
            return;
        }

        Debug.Log(
            $"주사위 공격! 타겟: {currentTarget.name} / 데미지: {damage}"
        );

        currentTarget.TakeDamage(damage);
    }

    // 바라보는 방향에서 공격할 적 찾기
    private Enemy FindAttackTarget()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            attackRange
        );

        Enemy bestTarget = null;
        float bestAngle = attackAngle;

        Vector3 cameraForward = cameraController.GetCameraForward();
        cameraForward.y = 0f;
        cameraForward.Normalize();

        foreach (Collider hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();

            if (enemy == null)
                continue;

            Vector3 direction =
                enemy.transform.position - transform.position;

            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f)
                continue;

            float angle = Vector3.Angle(
                cameraForward,
                direction.normalized
            );

            if (angle < bestAngle)
            {
                bestAngle = angle;
                bestTarget = enemy;
            }
        }

        return bestTarget;
    }

    // 가장 가까운 적 찾기
    private Enemy FindClosestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            targetRange
        );

        Enemy closestEnemy = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();

            if (enemy == null)
                continue;

            float distance =
                Vector3.Distance(
                    transform.position,
                    enemy.transform.position
                );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy;
            }
        }

        return closestEnemy;
    }

    // 실제 공격
    private void AttackTarget(Enemy target)
    {
        if (target == null)
        {
            currentTarget = null;
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            target.transform.position
        );

        if (distance > attackRange)
        {
            Debug.Log($"공격 실패 - 거리가 너무 멂: {distance:F2}");
            return;
        }

        Debug.Log(
            $"공격! 타겟: {target.name} / 데미지: {attackDamage}"
        );

        target.TakeDamage(attackDamage);

        gameManager.AddGauge();
    }

    private void OnDrawGizmosSelected()
    {
        // 공격 사거리
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );

        // 타겟 탐색 거리
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            targetRange
        );
    }
}