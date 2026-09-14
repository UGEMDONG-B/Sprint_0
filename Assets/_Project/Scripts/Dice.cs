using UnityEngine;
using TMPro;

public class Dice : MonoBehaviour
{
    [Header("Dice")]
    [SerializeField] private int[] faces;

    [Header("UI")]
    [SerializeField] private TMP_Text resultText;

    private int currentFace;

    public int CurrentFace => currentFace;

    private void Start()
    {
        ResetDice();
    }

    public void Roll()
    {
        if (faces == null || faces.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name}에 주사위 면이 설정되지 않았습니다.");
            return;
        }

        int index = Random.Range(0, faces.Length);
        currentFace = faces[index];

        UpdateUI();

        Debug.Log($"{gameObject.name} 결과: {currentFace}");
    }

    public void ResetDice()
    {
        currentFace = 0;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (resultText != null)
        {
            resultText.text = currentFace.ToString();
        }
    }
}