using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StepSoundCycle : MonoBehaviour
{
    [SerializeField] AudioSource source;

    [SerializeField] AudioClip[] stepClips;
     
    int current;

    void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    public void Cycle()
    {
        current++;

        if (current >= stepClips.Length)
        {
            current = 0;
        }

        source.PlayOneShot(stepClips[current]);
    }

    public void CycleRandom()
    {
        current = Random.Range(0, stepClips.Length);
        source.clip = stepClips[current];
        source.Play();
    }
}