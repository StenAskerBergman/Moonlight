using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
public class GetResourceData : MonoBehaviour
{
private Inventory islandInventory;
private StorageManager islandStorageManager;

public Text resourceCountText;

private void Start()
{
    // Set GameManager ref to the current instance of the game manager  
    GameManager gameManager = GameManager.instance;

    // GM - Null Ref Check
    if (gameManager != null)
    {
        // GM - Get Current Island
        Island currentIsland = gameManager.GetCurrentIsland();

        // GM - Island Null Ref Check
        if (currentIsland != null)
        {
            // GM - Get Island Item Manager
            islandInventory = gameManager.GetIslandInventory(currentIsland.id);
            if (islandInventory != null)
            {
                islandInventory.OnResourceCountChanged += OnResourceCountChanged;
            }
        }
    }
}

private void OnDestroy()
{
    if (islandItemManager != null)
    {
        islandItemManager.OnResourceCountChanged -= OnResourceCountChanged;
    }
}

private void OnResourceCountChanged(ItemEnums.ResourceType resource, int count)
{

      if (resource == ItemEnums.ResourceType.Resource1)  // Works as intended but uses variables based off the legacy methods - fix prior variables to work with new methods
      {
          resourceCountText.text = count.ToString();
      }
  }

}
*/
/*
private ResourceManager resourceManager;
private int UIwoodCount;
  private string strNR;

private void Awake()
{
  UIwoodCount = FindObjectOfType<ResourceManager>().GetResourceCount(Enums.Island.Main, Enums.Resource.Wood);
  strNR = UIwoodCount.ToString();
  GetComponent<UnityEngine.UI.Text>().text = strNR;
}

void Update()
{ 
      strNR = UIwoodCount.ToString();
      GetComponent<UnityEngine.UI.Text>().text = strNR;

}*/

