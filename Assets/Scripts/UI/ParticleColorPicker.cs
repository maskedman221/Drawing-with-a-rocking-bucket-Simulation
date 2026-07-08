using TMPro;
using UnityEngine;
using Xenia.ColorPicker;

public class ParticleColorPicker : MonoBehaviour
{
    public static ParticleColorPicker Instance { get; private set; }
    [SerializeField] ColorPicker colorPicker;

    private bool editColor = false;
    private Color currentColor = Color.red;

    private void Awake()
    {
        Instance = this;

        colorPicker.Opening.AddListener(() => editColor = true);
        colorPicker.Closing.AddListener(() => editColor = false);
        colorPicker.ColorPreview.AddListener((Color c) => { 
            currentColor = c; 
        });
        colorPicker.ColorChanged.AddListener((Color c) =>
        {
            currentColor = c;
        });
    }

    public Color GetCurrentColor()
    {
        return currentColor;
    }
    
    public bool IsEditingColor()
    {
        return editColor;
    }
    
    public void ResetColor(Color defaultColor)
    {
        currentColor = defaultColor;
    }
}