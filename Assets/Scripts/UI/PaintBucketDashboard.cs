using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class PaintBucketDashboard : MonoBehaviour
{
    [SerializeField] private  List<TMP_InputField> PaintSettings;
    [SerializeField] private  List<TMP_InputField> BucketSettings;
    [SerializeField] private  List<TMP_InputField> RopeSettings;
    [SerializeField] private  List<TMP_InputField> PaintColorSettings;
    [SerializeField] private  List<TMP_InputField> PlaneSettings;
    [SerializeField] private List<Toggle> toggles;
    [SerializeField] private List<Slider> sliders;
    [SerializeField] private List<Slider> planeSliders;
    [SerializeField] private List<Button> surfaceButton;
    [SerializeField] private  Button applyButton;
    [SerializeField] private  Button applyColorButton;
    [SerializeField] private  SPHManager sph;
    [SerializeField] private  BucketVolume bucket;
    [SerializeField] private  SimulationControllerGPU simulationControllerGPU;
    [SerializeField] private  FluidParticleRenderer fluidParticleRenderer;
    [SerializeField] private  PaintingCollision paintCollision;
    [SerializeField] private  List<SurfaceMaterial> surfaceMaterials;
    [SerializeField] private  Renderer surfaceRenderer;
    [SerializeField] private  List<Material> materials;


    private void Start()
    {
        applyButton.onClick.AddListener(() => Apply());
        applyColorButton.onClick.AddListener(() => ApplyColor());
        for(int i = 0 ; i<surfaceButton.Count ; i++)
        {
            int surfaceIndex = i;
            surfaceButton[i].onClick.AddListener(() => ApplySurfaceMaterial(surfaceIndex));
        }
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

    private void ApplyColor()
    {
        fluidParticleRenderer.applyColorUIChange = toggles[1].isOn;
        if(!fluidParticleRenderer.applyColorUIChange)
        sph.AddParticles(ParticleColorPicker.Instance.GetCurrentColor() , int.Parse(PaintColorSettings[0].text));
        if(paintCollision.surfaceMaterial == surfaceMaterials[4])
        {
            paintCollision.surfaceMaterial.restitution = sliders[2].value;
            paintCollision.surfaceMaterial.absorption = sliders[3].value;
            paintCollision.surfaceMaterial.staticFriction = sliders[4].value;
            paintCollision.surfaceMaterial.dynamicFriction = sliders[5].value;
            paintCollision.surfaceMaterial.spread = sliders[6].value;
            paintCollision.surfaceMaterial.wetness = sliders[7].value;
            paintCollision.surfaceMaterial.wetnessSlideFactor = sliders[8].value;
        }
    }

    private void ApplySurfaceMaterial(int index)
    {
        if(surfaceMaterials == null || index < 0 || index >= surfaceMaterials.Count)
        {
            Debug.LogWarning("Invalid surface material index.");
            return;
        }
        paintCollision.surfaceMaterial = surfaceMaterials[index];

        if(surfaceRenderer == null && paintCollision != null)
        {
            if(paintCollision.plane != null)
            {
                surfaceRenderer = paintCollision.plane.GetComponent<Renderer>();
            }

            if(surfaceRenderer == null)
            {
                surfaceRenderer = paintCollision.GetComponentInChildren<Renderer>(true);
            }
        }

        if(surfaceRenderer == null)
        {
            Debug.LogWarning("No child Renderer found under PaintingCollision.");
            return;
        }

        if(materials == null || index >= materials.Count || materials[index] == null)
        {
            Debug.LogWarning("No visual material assigned for this surface index.");
            return;
        }

        surfaceRenderer.sharedMaterial = materials[index];
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
