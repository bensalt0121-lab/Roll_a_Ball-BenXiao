using UnityEngine;
using TMPro;

public class MoneyManager : MonoBehaviour
{
    [Header("Money")]
    public int currentMoney = 0;

    [Header("UI")]
    public TMP_Text moneyText;

    void Start()
    {
        UpdateMoneyUI();
    }

    public void AddMoney(int amount)
    {
        currentMoney += amount;

        UpdateMoneyUI();

        Debug.Log("Money: $" + currentMoney);
    }

    public void RemoveMoney(int amount)
    {
        currentMoney -= amount;

        if (currentMoney < 0)
        {
            currentMoney = 0;
        }

        UpdateMoneyUI();
    }

    void UpdateMoneyUI()
    {
        if (moneyText != null)
        {
            moneyText.text = "$" + currentMoney;
        }
    }
}