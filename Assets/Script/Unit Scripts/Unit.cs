using UnityEngine;

public class Unit : MonoBehaviour
{
    public enum UnitType{Character, House};
    public UnitType type;

    void Start()
    {
        UnitSelections.Instance.unitList.Add(this);
    }

    void OnDestroy()
    {
        UnitSelections.Instance.unitList.Remove(this);
    }
}
