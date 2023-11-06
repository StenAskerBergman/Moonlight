using UnityEngine;

public interface IPreviewable
{
    void GeneratePreview();
    Texture2D PreviewTexture { get; }
}
