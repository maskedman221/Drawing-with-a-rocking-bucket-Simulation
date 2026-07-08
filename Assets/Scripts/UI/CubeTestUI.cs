using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
public class CubeTestUI : MonoBehaviour
{
    [SerializeField] private  List<TMP_InputField> PaintSettings;
    [SerializeField] private  Button applyButton;
    [SerializeField] private  SPHManager sph;

    private void Start()
    {
        applyButton.onClick.AddListener(() => Apply());

    }

    private void Apply()
    {
        SetSPHValues();

        if (!sph.gameObject.activeInHierarchy)
        {
            sph.gameObject.SetActive(true);
        }
    }

    private void SetSPHValues()
    {
        if (PaintSettings == null || PaintSettings.Count < 10)
        {
            Debug.LogWarning("PaintSettings needs 10 input fields: Grid, particle space, gravity, density, pressure, near pressure, viscosity, max speed, wall bounce, simulation speed.");
            return;
        }

        sph.ApplySimulationSettings2(
            int.Parse(PaintSettings[0].text),
            float.Parse(PaintSettings[1].text),
            float.Parse(PaintSettings[2].text),
            float.Parse(PaintSettings[3].text),
            float.Parse(PaintSettings[4].text),
            float.Parse(PaintSettings[5].text),
            float.Parse(PaintSettings[6].text),
            float.Parse(PaintSettings[7].text),
            float.Parse(PaintSettings[8].text),
            float.Parse(PaintSettings[9].text),
            float.Parse(PaintSettings[10].text),
            float.Parse(PaintSettings[11].text),
            float.Parse(PaintSettings[12].text),
            float.Parse(PaintSettings[13].text),
            float.Parse(PaintSettings[14].text),
            float.Parse(PaintSettings[15].text)
        );
    }
}
