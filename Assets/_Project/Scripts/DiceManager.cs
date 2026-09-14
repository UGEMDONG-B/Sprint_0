using UnityEngine;
using UnityEngine.InputSystem;

public class DiceManager : MonoBehaviour
{
    [Header("Dice")]
    [SerializeField] private Dice[] dice;

    [Header("Game Manager")]
    [SerializeField] private GameManager gameManager;

    [Header("Player")]
    [SerializeField] private PlayerController playerController;

    [Header("Roll")]
    [SerializeField] private int rollGaugeCost = 100;

    [Header("Selection UI")]
    [SerializeField] private GameObject[] selectedUI;

    private int selectedIndex = 0;

    public Dice CurrentDice => dice[selectedIndex];

    private void Start()
    {
        if (dice == null || dice.Length != 3)
        {
            Debug.LogWarning("DiceManager에 주사위 3개를 등록해야 합니다.");
            return;
        }

        if (selectedUI == null || selectedUI.Length != 3)
        {
            Debug.LogWarning("DiceManager에 선택 UI 3개를 등록해야 합니다.");
        }

        UpdateDiceSelection();
    }

    public void OnPreviousDice(InputValue value)
    {
        if (!value.isPressed)
            return;

        selectedIndex--;

        if (selectedIndex < 0)
            selectedIndex = dice.Length - 1;

        UpdateDiceSelection();
    }

    public void OnNextDice(InputValue value)
    {
        if (!value.isPressed)
            return;

        selectedIndex++;

        if (selectedIndex >= dice.Length)
            selectedIndex = 0;

        UpdateDiceSelection();
    }

    public void OnRollDice(InputValue value)
    {
        if (!value.isPressed)
            return;

        if (gameManager == null)
        {
            Debug.LogWarning("DiceManager에 GameManager가 연결되지 않았습니다.");
            return;
        }

        if (!gameManager.UseGauge(rollGaugeCost))
        {
            Debug.Log("게이지가 부족합니다.");
            return;
        }

        CurrentDice.Roll();
    }

    public void OnUseDice(InputValue value)
    {
        if (!value.isPressed)
            return;

        int damage = CurrentDice.CurrentFace;

        if (damage <= 0)
        {
            Debug.Log("사용할 주사위 결과가 없습니다.");
            return;
        }

        Debug.Log(
            $"주사위 {selectedIndex + 1} 사용! 결과: {damage} / 데미지: {damage}"
        );

        playerController.UseDiceDamage(damage);

        // 사용한 주사위 초기화
        CurrentDice.ResetDice();
    }

    private void UpdateDiceSelection()
    {
        Debug.Log($"현재 선택된 주사위: {selectedIndex + 1}");

        if (selectedUI == null)
            return;

        for (int i = 0; i < selectedUI.Length; i++)
        {
            if (selectedUI[i] != null)
            {
                selectedUI[i].SetActive(i == selectedIndex);
            }
        }
    }
}