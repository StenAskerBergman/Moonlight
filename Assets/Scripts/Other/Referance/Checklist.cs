using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Checklist : MonoBehaviour
{
    [Space(5)] // 5px Space
    [Header("Building System")] // List Header
    public bool BuildingCost = false;
    public bool BuildingFunction = false;
    public bool BuildingPassive = false;
    public bool BuildingCondition = false;
    public bool BuildingProperties = false;
    public bool BuildingHealth = false;
    public bool BuildingUpgrades = false;
    public bool BuildingInteractable = false;
    public bool BuildingDescription = false;
    public bool BuildingToolTip = false;
    public bool BuildingActionMenu = false;
    public bool BuildingMarket = false;
    public bool BuildingStates = false;
    [Space(10)] // 10 pixels of spacing here.
    [Header("Graphical User Interface", order = 1)]
    public bool Modes = false;

    public bool ImageChangerScript = false;
    /* 
     * Below is extra / above is more
     * 
        [Space(10)] // 10 pixels of spacing here.

        [Space(10, order = 0)]
        [Header("Header with some space around it", order = 1)]
        [Space(40, order = 2)]

        public string playerName = "Unnamed";

        [Space(10)]
        [Tooltip("Health value between 0 and 100.")]
        int health = 0;
        [Space(10)]

        [TextArea]
        public string MyTextArea;
     */
}