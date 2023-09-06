using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Enums;

public class RequirementEnums : MonoBehaviour
{

    public RequirementType[] Requirements = new RequirementType[]
    {
            RequirementType.ReqShore,   // Requires Shoreline
            RequirementType.ReqSea,     // Requires Above at Sea
            RequirementType.ReqSub,     // Requires Above Submerged
            RequirementType.ReqLand,    // Requires Only Land
            RequirementType.ReqOther    // Requires Edge Cases
    };

    public enum RequirementType
    {
        ReqShore,
        ReqSea,
        ReqSub,
        ReqLand,
        ReqOther
    }

    public enum PlacementRequirementType
    {
        ReqShore,
        ReqSea,
        ReqSub,
        ReqLand,
        ReqOther
    }

    public enum ResourceRequirementType
    {
        ReqA,
        ReqB,
        ReqC,
        ReqD,
        ReqE
    }


}
