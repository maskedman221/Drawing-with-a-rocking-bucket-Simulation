using TMPro;
using UnityEngine;
using Xenia.ColorPicker;

public class ParticleColorPicker : MonoBehaviour
{
    public static ParticleColorPicker Instance { get; private set; }
    [SerializeField] ColorPicker colorPicker;
    [SerializeField] private Renderer targetRenderer;

    private bool editColor = false;
    private Color currentColor = Color.red;

    private void Awake()
    {
        Instance = this;
        if (targetRenderer != null)
            currentColor = targetRenderer.material.color;

        colorPicker.Opening.AddListener(() => editColor = true);
        colorPicker.Closing.AddListener(() => editColor = false);
        colorPicker.ColorPreview.AddListener((Color c) => { 
            currentColor = c; 
        });
        colorPicker.ColorChanged.AddListener((Color c) => {
            currentColor = c;
            if (targetRenderer != null)
                targetRenderer.material.color = c;
        });
    }

    public void ChangeColor(Color c)
    {
        currentColor = c;
        if (targetRenderer != null)
            targetRenderer.material.color = c;
    }

    public Color GetCurrentColor()
    {
        return currentColor;
    }
}