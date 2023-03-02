using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class GetResourceData : MonoBehaviour
{
  private IslandResourceManager islandResourceManager;
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
              islandResourceManager = gameManager.GetIslandResourceManager(currentIsland.id);
              if (islandResourceManager != null)
              {
                  islandResourceManager.OnResourceCountChanged += OnResourceCountChanged;
              }
          }
      }
  }

  private void OnDestroy()
  {
      if (islandResourceManager != null)
      {
          islandResourceManager.OnResourceCountChanged -= OnResourceCountChanged;
      }
  }

  private void OnResourceCountChanged(Enums.Resource resource, int count)
  {
      if (resource == Enums.Resource.Resource1)
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

