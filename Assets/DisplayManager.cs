using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class DisplayManager : MonoBehaviour
{
    // This script is used to manage what HUD elements to display

    // Has two lists, one for elements to show and one for elements to hide

    // When an element is added to the show list, it is shown and removed from the hide list
    // When an element is added to the hide list, it is hidden and removed from the show list

    // Then it loops the lists to manage what HUD elements to enable or disable based on the lists

    public List<GameObject> FullList = new List<GameObject>(); // Full List of all HUD elements - Might not be needed
    public List<GameObject> ShowList = new List<GameObject>(); // List of HUD elements to show
    public List<GameObject> HideList = new List<GameObject>(); // List of HUD elements to hide

    
    #region Focus Methods

    // A Stack to track for what object the player has previously selected to focus on
    // This is used to return to the previous focus when the player deselects the current focus
    // And provides us with a way to track what the player is currently and previous focusing on

    // Common Scenario:
    // 1. Player Selects Object - Gets Menu, Object is destroyed, Now What?
    // 2. Player Selects Object - Gets Menu, Player selects Another Object, but the previous Object gets destroyed, Now What?

    /*  Stacks Info ...
     
        Here's a little more details On Stacks:

        Push: When you push an item, it gets added to the top of the stack.
        Pop: When you pop an item, it removes the item from the top of the stack.
        Peek (or Top): Views the object on the top of the stack without removing it.

    */

    public Stack<GameObject> FocusStack = new Stack<GameObject>(); // Last In, First Out (LIFO) 

    // When a player selects an object to focus on, we add its ui to the stack if we can

    public void FocusOn(GameObject element)
    {
        if (element != null)
        {
            FocusStack.Push(element);
            Show(element);
        }
    }

    public void Focus()
    {
        if (FocusStack.Count > 0)
        {
            GameObject topElement = FocusStack.Peek();
            if (topElement != null)
            {
                FocusStack.Peek();

                // Logic to focus on the topElement.
            }
            else
            {
                // Logic for what to do if the top element is null.
            }
        }
        else
        {
            // Logic if the stack is empty.
        }
    }

    // When a player deselects an object to focus on, we remove it from the stack
    public void Unfocus(bool ReturnFocusAfter)
    {
        // Optionally: Logic to "unfocus" from the current object.
        if (FocusStack.Count > 0)
        {
            GameObject unfocusedElement = FocusStack.Pop(); // Yes, it does Both
            Hide(unfocusedElement);
            
            if (ReturnFocusAfter) { ReturnFocus(); } else { return;}
        }
    }

    // When a player deselects an object to focus on, we return the focus to the previous element - When asked too!
    public void ReturnFocus()
    {
        if (FocusStack.Count > 0)
        {
            FocusStack.Pop(); // Remove current (or null) focus.

            // Check again if there's something to focus on.
            if (FocusStack.Count > 0)
            {
                GameObject previousFocus = FocusStack.Peek();
                if (previousFocus != null)
                {
                    // Logic to focus on the previous object.
                    Focus();
                }
                else
                {
                    // Handle the case where the previous focus is null. 

                    // Remove the previous focus from the stack - removes null references
                    FocusStack.Pop();
                }
            }
            else
            {
                // Logic for when there's nothing to return focus to.
                
                // Do nothing. :)
            }
        }
    }


    #endregion

    #region Show/Hide Methods

    private enum ListOperation { add, remove };

        public void Show(GameObject element)
        {
            if (FullList.Contains(element))
            {
                // If the element is on the full list, remove it
                ModifyList(ShowList, element, ListOperation.add);
                ModifyList(HideList, element, ListOperation.remove);
            }

            UpdateElements();
        }

        public void Hide(GameObject element)
        {
            if (FullList.Contains(element))
            {
                // If the element is on the full list, remove it
                ModifyList(ShowList, element, ListOperation.remove);
                ModifyList(HideList, element, ListOperation.add);
            }

            UpdateElements();
        }
    
        public void ShowOverAll(GameObject _element)
        {
            foreach (GameObject element in ShowList)
            {
                // Hide all elements in the show list
                ModifyList(ShowList, element, ListOperation.remove);    // Remove all elements in the show list
                ModifyList(HideList, element, ListOperation.add);       // Adds all elements from the show list
            }

            ModifyList(ShowList, _element, ListOperation.add); // Adds the element to the show list - This is the element that should be shown over all other elements since its alone in the show list
            ModifyList(HideList, _element, ListOperation.remove);
            UpdateElements();
        }

        public void HideAll()
        {
            foreach (GameObject element in ShowList)
            {
                // Hide all elements in the show list
                ModifyList(ShowList, element, ListOperation.remove);    // Remove all elements in the show list
                ModifyList(HideList, element, ListOperation.add);       // Adds all elements from the show list
            }

            UpdateElements();
        }

    #endregion


    private void UpdateElements()
    {
        foreach (GameObject element in ShowList)
        {
            element.SetActive(true);
        }
        foreach (GameObject element in HideList)
        {
            element.SetActive(false);
        }
    }

    private void ModifyList(List<GameObject> list, GameObject element, ListOperation action)
    {
        switch(action)
        {
            case ListOperation.add:
                list.Add(element);
                break;
            case ListOperation.remove:
                list.Remove(element);
                break;
        }
    }

    public void UpdateAdoption()
    {
        // Add all children to the full list        
        foreach (Transform child in transform)
        {
            // Check - If not the full list
            if (!FullList.Contains(child.gameObject))
            {
                // If not, add it
                FullList.Add(child.gameObject);
            } 
            else
            {
                // If it is, remove it
                FullList.Remove(child.gameObject);

                // Check if its still on the full list
                if (!FullList.Contains(child.gameObject))
                {
                    // If not, add it
                    FullList.Add(child.gameObject);
                }
            }
        }

        // Remove objects from the full list that are no longer children
        FullList.RemoveAll(item => item.transform.parent != transform);
    }


    public enum ListType { FullList, HideList, ShowList };

    [Header("Pick a List to Clear")]
    public ListType listType; // This is the list we want to clear - we set it from the inspector

    public void Clear()
    {
        switch (listType)
        {
            case ListType.FullList:
                FullList.Clear();
                break;
            case ListType.HideList:
                HideList.Clear();
                break;
            case ListType.ShowList:
                ShowList.Clear();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(listType), listType, null);
        }
    }
    public void Clear(List<GameObject> list)
    {
        list.Clear();
    }

    // Start is called before the first frame update 
    void Start()
    {
        FullList.Clear();
        UpdateAdoption();
        UpdateElements();
    }
}
