using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public abstract class BuildingRequirement : ScriptableObject
{
    // Abstract method that all subclasses must implement
    public abstract bool IsSatisfied();
}