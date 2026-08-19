using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AudioManager : MonoBehaviour
{

    // Just a conceptual idea of how this script could work... nothing is written in stone...

    public static AudioManager Instance { get; private set; }

    public AudioClip DropIntoSlot, DropIntoSea;

    private void Awake()
    {
        if (Instance != null && Instance != this) Instance = this;
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PlaySound(AudioClip audioClip)
    {
        AudioSource.PlayClipAtPoint(audioClip, Vector3.zero);
    }

    public enum TagAttribute
    {
        ItemSlot,
        Sea,
        // Add other tags as needed
    }

    public void DroppingItem(Vector3 dropPosition, TagAttribute tag)
    {
        // Assuming DropIntoSlot and DropIntoSea are AudioClip variables defined elsewhere
        AudioClip clipToPlay = null;

        switch (tag)
        {
            case TagAttribute.ItemSlot:
                clipToPlay = DropIntoSlot;
                break;
            case TagAttribute.Sea:
                clipToPlay = DropIntoSea;
                break;
                // Add more cases as needed
        }

        if (clipToPlay != null)
        {
            AudioSource.PlayClipAtPoint(clipToPlay, dropPosition);
        }
    }


    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
