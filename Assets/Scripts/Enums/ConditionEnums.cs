using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConditionEnums : MonoBehaviour
{
    public enum ConditionType
    {
        Resource_Exists,
        Power_Exists,

        Resource_Amount_Above,
        Power_Amount_Above,

        Ecology_Positive_Amount,
        Ecology_Negative_Amount,
        Ecology_Level,

        Buildings_Within_Range,
        Seed_Type,

        Has_Road,
        Has_CityCenter,
        Has_SecondaryUnit,
        Has_PrimaryUnit,
    }

    public class Condition
    {
        public ConditionType type;
        public object value;

        public Condition(ConditionType type, object value)
        {
            this.type = type;
            this.value = value;
        }
    }
}
