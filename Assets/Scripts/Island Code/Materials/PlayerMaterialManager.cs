using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMaterialManager : ResourceManager
{
    // Add any additional player-specific resources here
    public int money = 1000;

    public int material1 = 10;
    public int material2 = 10;
    public int material3 = 10;

    //public int resource1 = 100;
    //public int resource2 = 100;
    //public int resource3 = 100;



    #region Money interaction

        // Subtract the cost of a building from the player's money
        public bool SpendMoney(int cost)
        {
            if (money >= cost)
            {
                money -= cost;
                return true;
            }
            else
            {
                return false;
            }
        }
        // Subtract the cost of a building from the player's Storage 
        public bool SubtractMaterial(int cost)
        {
            if (material1 >= cost)
            {
                material1 -= cost;
                return true;
            }
            else
            {
                return false;
            }
        }

    #endregion


}