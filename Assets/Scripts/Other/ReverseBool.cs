using UnityEngine;
using Extensions;

public class ReverseBool : MonoBehaviour
{
    [SerializeField]
    private bool ActiveOp;

    [SerializeField]
    private GameObject Target;

    private void Awake()
    {
        ActiveOp = false;
        if (Target != null)
        {
            Target.SetActive(false);
        }
    }

    public void InvertBool_Method()
    {
        InvertBool();
    }

    public bool InvertBool()
    {
        Target.SetActive(!Target.activeSelf);
        ActiveOp = Target.activeSelf;
        //Debug.Log("Inversion");
        return ActiveOp;
    }

}