using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingInventoryData", menuName = "Building Inventory Data")]
public class BuildingInventoryData : ScriptableObject
{
    // Note.
    // Almost all these values are dynamic
    // so might aswell treat all as dynamic

    //#region Nav & Id

    [Header("For Navigation & Identification")]
    // Navigation Related
    // For Navigation & Identification
    [SerializeField] private int inventoryID;
    [SerializeField] private int inventoryType;
    [Space(10)]
    //#endregion

    //#region Amounts & Caps
    [Header("Amounts & Caps")]
    // Basic Usecases
    public int inventoryCapacity; 

    // Adv. Usecases
    public int nowInventoryCapacity;        // Capacity Now 
    public int maxInventoryCapacity;        // Capacity Maximum
    public int minInventoryCapacity;        // Capacity Minimum
    [Space(10)]

    //#endregion


    //#region Speed Related
    [Header("Speed Related")]

    // Transfer Speeds for loading
    public int InventoryRate = 1;                   // Default Speed of Transfer
    
    // Notice:
    // Return and Introduce complexity later, when you feel ready for it!

    /* Read The Notice Above
    public int TotalInventoryRate = 1;              // Total Speed of Transfer

    // For Loading Input & Output
    public int InternalInventoryRate = 1;           // Internal Speed of Transfer
    public int ExternalInventoryRate = 1;           // External Speed of Transfer
    
    //#endregion
    */

    
}