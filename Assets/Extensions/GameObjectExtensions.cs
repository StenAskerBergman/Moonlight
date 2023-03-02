using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Extensions
{
    public static class GameObjectExtensions
    {
        /* Don't need it for now
        // Custom event that is raised whenever SetActive is called on the GameObject
        public static event Action<bool> SetActiveEvent;

        // Custom implementation of SetActive that raises the SetActiveEvent
        public static void CustomSetActive(this GameObject gameObject, bool value)
        {
            gameObject.SetActive(value);
            SetActiveEvent?.Invoke(value);
        }*/
    }
}
