using UnityEngine;
using UnityEngine.UI;

public class PowerUIManager : MonoBehaviour
{
    [Header("Power Related")]
    public Text CurrentPowerText;
    public Text MadePowerText;
    public Text SpentPowerText;
    public Text TotalPowerText;

    [Header("UI References")]
    [SerializeField] private GameObject powerDisplayRoot;
    [SerializeField] private Text balanceText;
    [SerializeField] private Text supplyText;
    [SerializeField] private Text demandText;

    public IslandPower islandPower;
    public int IslandSettlement;

    private void OnDestroy()
    {
        UnsubscribeFromCurrentIsland();
    }

    public void OnCurrentIslandChanged(Island island)
    {
        UnsubscribeFromCurrentIsland();

        islandPower = island != null ? island.GetComponent<IslandPower>() : null;
        if (islandPower != null)
        {
            islandPower.OnPowerChanged += UpdatePowerUI;
        }

        UpdatePowerUI();
    }

    private void UnsubscribeFromCurrentIsland()
    {
        if (islandPower != null)
        {
            islandPower.OnPowerChanged -= UpdatePowerUI;
        }
    }

    public void UpdatePowerUI()
    {
        bool canDisplayPower = islandPower != null && islandPower.Settled;
        IslandSettlement = canDisplayPower ? 1 : 0;

        if (powerDisplayRoot != null)
        {
            powerDisplayRoot.SetActive(canDisplayPower);
        }

        if (!canDisplayPower)
        {
            ClearPowerText();
            return;
        }

        SetText(CurrentPowerText, islandPower.GetCurrentPower());
        SetText(SpentPowerText, islandPower.GetPowerSpent());
        SetText(TotalPowerText, islandPower.GetTotalPower());
        SetText(MadePowerText, islandPower.GetMadePower());

        SetText(balanceText, islandPower.GetCurrentPower());
        SetText(supplyText, islandPower.GetMadePower());
        SetText(demandText, islandPower.GetPowerSpent());
    }

    private void ClearPowerText()
    {
        SetText(CurrentPowerText, string.Empty);
        SetText(SpentPowerText, string.Empty);
        SetText(TotalPowerText, string.Empty);
        SetText(MadePowerText, string.Empty);
        SetText(balanceText, string.Empty);
        SetText(supplyText, string.Empty);
        SetText(demandText, string.Empty);
    }

    private static void SetText(Text target, int value) => SetText(target, value.ToString());

    private static void SetText(Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
