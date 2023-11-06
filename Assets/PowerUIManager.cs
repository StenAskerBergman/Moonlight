using UnityEngine;
using UnityEngine.UI;

public class PowerUIManager : MonoBehaviour
{
    [Header("Power Related")]
    public Text CurrentPowerText;
    public Text MadePowerText;
    public Text SpentPowerText;
    public Text TotalPowerText;

    public IslandPower islandPower;
    private Island currentIsland;
    public bool IslandSettled = false;

    public void OnCurrentIslandChanged(Island island)
    {
        if (island == null)
        {
            Debug.Log("Island = Null");
            IslandSettled = false;
            return;
        }
        currentIsland = island;
        islandPower = island.GetComponent<IslandPower>();
        UpdatePowerUI();
    }

    public void UpdatePowerUI()
    {
        if (islandPower == null)
        {
            Debug.Log("islandPower = Null");
            IslandSettled = false;
            return;
        }
        else
        {
            if (islandPower.Settled == true)
            {
                IslandSettled = islandPower.Settled;
                // Display the amount of Power in the UI
                CurrentPowerText.text = "" + islandPower.GetCurrentPower();   // Current Power
                SpentPowerText.text = "" + islandPower.GetPowerSpent();       // Spent Power
                TotalPowerText.text = "" + islandPower.GetTotalPower();       // Total Power
                MadePowerText.text = "" + islandPower.GetMadePower();         // Total Power
            }
        }
    }
}
