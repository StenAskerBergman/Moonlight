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
    private Island currentIsland;

    private void Update()
    {
        UpdateEcologyUI();
    }

    public void OnCurrentIslandChanged(Island island)
    {
        if (island == null)
        {
            Debug.Log("Island = Null");
            return;
        }

        currentIsland = island;
        islandEcology = island.GetComponent<IslandEcology>();
        UpdateEcologyUI();
    }

    public void UpdateEcologyUI()
    {
        if (currentIsland != null)
        {
            if (islandEcology == null)
            {
                Debug.Log("islandEcology = Null");
                return;
            }
            else
            {
                EcoValueText.text = "" + islandEcology.GetCurrentEco();
                EcoPosText.text = "" + islandEcology.GetPositiveEco();
                EcoNegText.text = "" + islandEcology.GetNegativeEco();
            }
        }
    }
}
