using UnityEngine;
using UnityEngine.UI;

public class TerrainAdjuster : MonoBehaviour
{
    public IslandNoise islandNoise;
    public Slider octaveSlider;
    public Slider persistenceSlider;
    public Slider lacunaritySlider;

    private void Start()
    {
        // Initialize slider values
        octaveSlider.value = islandNoise.octaves;
        persistenceSlider.value = islandNoise.persistence;
        lacunaritySlider.value = islandNoise.lacunarity;

        // Add listener to sliders
        octaveSlider.onValueChanged.AddListener(UpdateOctaves);
        persistenceSlider.onValueChanged.AddListener(UpdatePersistence);
        lacunaritySlider.onValueChanged.AddListener(UpdateLacunarity);
    }

    public void UpdateOctaves(float value)
    {
        islandNoise.octaves = (int)value;
        islandNoise.GenerateNoise();
    }

    public void UpdatePersistence(float value)
    {
        islandNoise.persistence = value;
        islandNoise.GenerateNoise();
    }

    public void UpdateLacunarity(float value)
    {
        islandNoise.lacunarity = value;
        islandNoise.GenerateNoise();
    }
}
