
// Start - SeedSlot.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SeedSlot : MonoBehaviour
{
    public SeedDisplayManager seedDisplayManager;  // Reference to the SeedDisplayManager

    private ItemData seedData;
    [SerializeField]private Image childImage, bgImage;
    [SerializeField]private Text seedTitle, seedFactor, seedDescription; // Seed title, seed factor and seed description

    public int slotIndex; // Add this variable to keep track of this slot's index

    public Sprite defaultSprite; // The default sprite to replace the child's image with
    private bool canReplace = true; // Flag to check if the replacement is allowed
    
    private void Awake() // Tried it with, Start() and that didn't work either, same nullref iissue
    {
        seedDisplayManager = GetComponentInParent<SeedDisplayManager>();
        if (seedDisplayManager == null)
        {
            Debug.LogError("No SeedDisplayManager found in parent.");
            return;  // Return early to prevent further execution
        }

        childImage = transform.GetChild(0).GetComponent<Image>();
        if (childImage == null)
        {
            Debug.LogError("No child image found.");
            return;  // Return early to prevent further execution
        }

        // Ensure SeedManager and currentIsland are not null before calling UpdateActiveState
        if (seedDisplayManager.seedManager == null)
        {
            Debug.LogError("seedManager is null.");
        }
        
        // Ensure SeedManager and currentIsland are not null before calling UpdateActiveState
        if (seedDisplayManager.currentIsland == null)
        {
            Debug.LogWarning("currentIsland is null.");
        }
        
        UpdateActiveState();
    }



    public void SetSeedData(ItemData newSeedData)
    {
        seedData = newSeedData;
        UpdateImage();
        UpdateText();
        UpdateActiveState();
    }

    private void UpdateActiveState()
    {
        bool shouldShowDefaultImage = false;  // Assume false initially

        if (seedDisplayManager.currentIsland != null)
        {
            shouldShowDefaultImage = seedDisplayManager.ShouldShowDefaultImage(slotIndex, seedDisplayManager.seedManager.GetSeedCountOnIsland(seedDisplayManager.currentIsland));
        }

        if (childImage != null)
        {
            // Enable or disable the Image component of childImage based on the conditions
            childImage.enabled = shouldShowDefaultImage || seedData != null;
            childImage.sprite = shouldShowDefaultImage ? defaultSprite : (seedData != null ? seedData.Icon : null);
        }

        // Optional: If you still want to disable/enable the Image component on the same GameObject
        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.enabled = shouldShowDefaultImage || seedData != null;
            // Note: The following line has been commented out to prevent setting the sprite of img
            // img.sprite = shouldShowDefaultImage ? defaultSprite : (seedData != null ? seedData.Icon : null);
        }
    }



    public void ClearSeedData()
    {
        seedData = null;
        UpdateImage();  // Update the image to the default state
    }

    private void UpdateImage()
    {
        if (childImage != null)
        {
            if (seedData != null)
            {
                childImage.sprite = seedData.Icon;  // Set the sprite based on the seed data
                childImage.enabled = true;  // Ensure the image component is enabled
            }
            else
            {
                bool shouldShowDefaultImage = seedDisplayManager.ShouldShowDefaultImage(slotIndex, seedDisplayManager.seedManager.GetSeedCountOnIsland(seedDisplayManager.currentIsland));
                childImage.sprite = shouldShowDefaultImage ? defaultSprite : null;  // Set to default sprite if necessary, or hide the sprite
                childImage.enabled = shouldShowDefaultImage;  // Enable or disable the image component based on whether the default image should be shown
            }
        }
    }

    private void UpdateText()
    {
        if (seedData != null)
        {
            if (seedTitle != null)
            {
                seedTitle.text = seedData.displayName;
            }

            if (seedFactor != null)
            {
                seedFactor.text = seedData.factor.ToString();  // Assuming factor is a property of ItemData
            }

            if (seedDescription != null)
            {
                seedDescription.text = seedData.description;  // Assuming description is a property of ItemData
            }
        }
        else
        {
            if (seedTitle != null) seedTitle.text = "";
            if (seedFactor != null) seedFactor.text = "";
            if (seedDescription != null) seedDescription.text = "";
        }
    }

    // Method to replace the child's image with the default sprite
    public void ReplaceChildImageWithDefault()
    {
        if (canReplace)
        {
            Image childImage = transform.GetChild(0).GetComponent<Image>();

            if (childImage != null)
            {
                childImage.sprite = defaultSprite;
                canReplace = false; // Set the flag to false, preventing further replacements
            }
            else
            {
                Debug.LogWarning("No child image found to replace.");
            }
        }
        else
        {
            Debug.LogWarning("Image can only be replaced once.");
        }
    }
}

// End - SeedSlot.cs
