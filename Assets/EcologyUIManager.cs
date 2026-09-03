using UnityEngine;
using UnityEngine.UI;

public class EcologyUIManager : MonoBehaviour
{
    [Header("Eco Related")]
    [Space(8)]
    public Text EcoValueText;
    public Text EcoPosText;
    public Text EcoNegText;

    public IslandEcology islandEcology;

    private void OnDestroy()
    {
        UnsubscribeFromCurrentIsland();
    }

    public void OnCurrentIslandChanged(Island island)
    {
        UnsubscribeFromCurrentIsland();

        islandEcology = island != null ? island.GetComponent<IslandEcology>() : null;
        if (islandEcology != null)
        {
            islandEcology.EcologyChanged += UpdateEcologyUI;
        }

        UpdateEcologyUI();
    }

    private void UnsubscribeFromCurrentIsland()
    {
        if (islandEcology != null)
        {
            islandEcology.EcologyChanged -= UpdateEcologyUI;
        }
    }

    public void UpdateEcologyUI()
    {
        if (islandEcology == null)
        {
            SetText(EcoValueText, string.Empty);
            SetText(EcoPosText, string.Empty);
            SetText(EcoNegText, string.Empty);
            return;
        }

        SetText(EcoValueText, islandEcology.GetCurrentEco());
        SetText(EcoPosText, islandEcology.GetPositiveEco());
        SetText(EcoNegText, islandEcology.GetNegativeEco());
    }

    private static void SetText(Text target, int value) => SetText(target, value.ToString());

    private static void SetText(Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
