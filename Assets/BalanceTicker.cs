using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BalanceTicker : MonoBehaviour
{
    private Text textComponent;
    private Color originalTextColor; // Store the original text color.

    private void Start()
    {
        // Get the Text component attached to this GameObject.
        textComponent = GetComponent<Text>();

        if (textComponent == null)
        {
            Debug.LogError("No Text component found on GameObject.");
        }
        else
        {
            // Store the original text color.
            originalTextColor = textComponent.color;
        }
    }

    // Method to set the text and change color if it's negative.
    public void SetText(string newText)
    {
        if (textComponent != null)
        {
            textComponent.text = newText;

            // Check if the text represents a negative value.
            if (int.TryParse(newText, out int intValue) && intValue < 0)
            {
                // Change the text color to red.
                textComponent.color = Color.red;
            }
            else
            {
                // Reset the text color to its original color.
                textComponent.color = originalTextColor;
            }
        }
        else
        {
            Debug.LogError("No Text component found on GameObject.");
        }
    }
}