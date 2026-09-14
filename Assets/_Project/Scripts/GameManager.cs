using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("Gauge")]
    [SerializeField] private int maxGauge = 100;
    [SerializeField] private int gaugePerHit = 20;
    [SerializeField] private Image gaugeFill;

    private int currentGauge = 0;

    public int CurrentGauge => currentGauge;
    public int MaxGauge => maxGauge;

    private void Start()
    {
        UpdateGaugeUI();
    }

    public void AddGauge()
    {
        currentGauge += gaugePerHit;
        currentGauge = Mathf.Clamp(currentGauge, 0, maxGauge);

        UpdateGaugeUI();

        Debug.Log($"게이지: {currentGauge}/{maxGauge}");
    }

    public bool UseGauge(int amount)
    {
        if (currentGauge < amount)
            return false;

        currentGauge -= amount;

        UpdateGaugeUI();

        return true;
    }

    private void UpdateGaugeUI()
    {
        if (gaugeFill == null)
            return;

        gaugeFill.fillAmount =
            (float)currentGauge / maxGauge;
    }
}