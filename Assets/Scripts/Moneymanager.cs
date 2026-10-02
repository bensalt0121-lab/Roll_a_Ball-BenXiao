/*********************************************************************************************
 * COMPONENT OF: MoneyManager
 * REQUIRED DEPENDENCIES: TextMeshPro text on the Canvas in moneyText, collectibles that call
 *                        AddMoney
 * DESCRIPTION: Keeps the player's money total and shows it on screen as "$amount".
 *              AddMoney adds to the total. RemoveMoney subtracts but never goes below 0.
 * AUTHOR: Ben Xiao
 * VERSION: 1.0
 *********************************************************************************************/
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