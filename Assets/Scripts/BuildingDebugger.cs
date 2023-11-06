using UnityEngine;

[RequireComponent(typeof(Building))]
[RequireComponent(typeof(BuildingCost))]
[RequireComponent(typeof(Unit))]
[RequireComponent(typeof(BuildingProperties))]
[ExecuteInEditMode] // This attribute makes the script execute in the editor.
public class BuildingDebugger : MonoBehaviour
{
    /*
    public ScriptableObject someReference;

    void OnEnable()
    {
        CheckDependencies();
    }

    void CheckDependencies()
    {
        if (someReference == null)
        {
            Debug.LogWarning("Building Debugger on " + gameObject.name + " requires a reference to some ScriptableObject!");
        }

        // Add similar checks for other dependencies
    }*/
}
