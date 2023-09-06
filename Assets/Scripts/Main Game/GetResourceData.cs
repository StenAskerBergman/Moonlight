using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class GetResourceData : MonoBehaviour
{
  private IslandItemManager islandItemManager;
  private ResourceManager resourceManager;
  private IslandStorage islandStorage;
  public Text resourceCountText;
  
  private void Start()
  {
    
      GameManager gameManager = GameManager.instance;
      if (gameManager != null)
      {
          Island currentIsland = gameManager.GetCurrentIsland();
          if (currentIsland != null)
          {
              islandItemManager = gameManager.GetIslandItemManager(currentIsland.id);
              if (islandItemManager != null)
              {
                  islandItemManager.OnResourceCountChanged += OnResourceCountChanged;
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
      if (resource == ItemEnums.ResourceType.Resource1)
      {
          resourceCountText.text = count.ToString();
      }
  }
}


  /*
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

