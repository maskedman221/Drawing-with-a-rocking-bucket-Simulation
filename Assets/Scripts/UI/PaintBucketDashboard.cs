using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class PaintBucketDashboard : MonoBehaviour
{
    [SerializeField] private  List<TMP_InputField> PaintSettings;
    [SerializeField] private  List<TMP_InputField> BucketSettings;
    [SerializeField] private  List<TMP_InputField> RopeSettings;
    [SerializeField] private List<Toggle> toggles;
    [SerializeField] private List<Slider> sliders;
    [SerializeField] private  Button applyButton;
    [SerializeField] private  SPHManager sph;
    [SerializeField] private  BucketVolume bucket;
    [SerializeField] private  SimulationControllerGPU simulationControllerGPU;


    private void Start()
    {
        applyButton.onClick.AddListener(() => Apply());
    }

    private void Apply()
    {   
        SetSPHValues();
        SetBucketValues();
        SetSimulationControllerGPUValues();
        
        if (!sph.gameObject.activeInHierarchy)
        {
            sph.gameObject.SetActive(true);
        }
        if (!simulationControllerGPU.gameObject.activeInHierarchy)
        {
            simulationControllerGPU.gameObject.SetActive(true);
        }

        
    }
    private void SetSPHValues()
    {
        if (PaintSettings == null || PaintSettings.Count < 10)
        {
            Debug.LogWarning("PaintSettings needs 10 input fields: Grid, particle space, gravity, density, pressure, near pressure, viscosity, max speed, wall bounce, simulation speed.");
            return;
        }

        sph.ApplySimulationSettings(
            int.Parse(PaintSettings[0].text),
            float.Parse(PaintSettings[1].text),
            float.Parse(PaintSettings[2].text),
            float.Parse(PaintSettings[3].text),
            float.Parse(PaintSettings[4].text),
            float.Parse(PaintSettings[5].text),
            float.Parse(PaintSettings[6].text),
            float.Parse(PaintSettings[7].text),
            float.Parse(PaintSettings[8].text),
            float.Parse(PaintSettings[9].text)
        );
    }
    private void SetBucketValues()
    {
        if (BucketSettings == null || BucketSettings.Count < 4)
        {
            Debug.LogWarning("BucketSettings needs 4 input fields: NozzleRadius, bounce, Friction, nozzleCaptureDepth.");
            return;
        }

        bucket.nozzleRadius = float.Parse(BucketSettings[0].text);
        bucket.bounce = float.Parse(BucketSettings[1].text);
        bucket.friction = float.Parse(BucketSettings[2].text);
        bucket.nozzleCaptureDepth = float.Parse(BucketSettings[3].text);
        bucket.useMeteredNozzleFlow = toggles[0].isOn;
        bucket.nozzleFeedRadiusFraction = sliders[0].value;
    }
    private void SetSimulationControllerGPUValues()
    {
        if (RopeSettings == null || RopeSettings.Count < 4)
        {
            Debug.LogWarning("RopeSettings needs 4 input fields: Point count, rope length, max stretch, rope damping.");
            return;
        }

        simulationControllerGPU.ApplyRopeSettings(
            int.Parse(RopeSettings[0].text),
            float.Parse(RopeSettings[1].text),
            sliders[1].value,
            float.Parse(RopeSettings[2].text),
            float.Parse(RopeSettings[3].text)
        );
    }
}
