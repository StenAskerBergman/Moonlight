using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    #region currentIsland Section 

        public bool logMouse = false;
        public bool logIsland = false;

        private Island currentIsland;
        
        private void Start()
        {
            IslandManager.instance.OnPlayerEnterIsland += OnPlayerEnterIsland;
        }

        private void OnDestroy()
        {
            IslandManager.instance.OnPlayerEnterIsland -= OnPlayerEnterIsland;
        }

        private void OnPlayerEnterIsland(Island island)
        {
            currentIsland = island;
            if (logIsland==true){Debug.Log("Player is looking at island: " + currentIsland.id);}
        }

    #endregion

    #region Raycast Section 

        RaycastHit hit; //Creates a hit varible
        public GameObject Model;
        [SerializeField] private LayerMask mask; //On the top of you code. SerializeField make your variable visible in the inspector


        void Update()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); // Ray

            if (Physics.Raycast(ray, out hit, mask))
            {   
                if (logMouse==true){Debug.Log("Mouse Position: " + Input.mousePosition);}
            }
        }

    #endregion
}
