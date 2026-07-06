using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [Header("Simulation References")]
    public SimulationController simController;
    public SPHManager sphManager;
    public BucketVolume bucketVolume;
    public PaintingCollision paintingCollision;
    public MaterialPresets materialPresets;
    public FluidParticleRenderer paintRenderer;

    [Header("Input Fields")]
    public InputField inputLength;
    public InputField inputAngle;
    public InputField inputPhi;
    public InputField inputDamping;
    public InputField inputMass;
    public InputField inputRadius;
    public InputField inputHole;
    public InputField inputViscosity;
    public InputField inputGravity;
    public InputField inputTilt;
    public InputField inputColorHex;

    [Header("Other UI")]
    public Dropdown surfaceDropdown;
    public Image colorPreview;
    public List<Button> colorButtons;

    [Header("Outputs")]
    public Text outputTime;
    public Text outputParticles;
    public Text outputCoverage;
    public Text outputSpeed;

    [Header("Buttons")]
    public Button startButton;
    public Button resetButton;
    public Button saveButton;
    public Button compareButton;
    public Button recordButton;

    private bool isSimulating = true;
    private bool initialized;
    private bool isRecording;
    private int recordingFrameIndex;
    private string recordingDirectory;

    void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (initialized)
        {
            RefreshFromSimulation();
            return;
        }

        initialized = true;
        AutoResolveReferences();

        RegisterInput(inputLength, val => ApplyChange("Length", val));
        RegisterInput(inputAngle, val => ApplyChange("Angle", val));
        RegisterInput(inputPhi, val => ApplyChange("Phi", val));
        RegisterInput(inputDamping, val => ApplyChange("Damping", val));
        RegisterInput(inputMass, val => ApplyChange("Mass", val));
        RegisterInput(inputRadius, val => ApplyChange("Radius", val));
        RegisterInput(inputHole, val => ApplyChange("Hole", val));
        RegisterInput(inputViscosity, val => ApplyChange("Viscosity", val));
        RegisterInput(inputGravity, val => ApplyChange("Gravity", val));
        RegisterInput(inputTilt, val => ApplyChange("Tilt", val));
        RegisterInput(inputColorHex, ApplyChangeColorHex);

        if (surfaceDropdown != null)
        {
            surfaceDropdown.onValueChanged.AddListener(OnSurfaceChanged);
        }

        if (startButton != null) startButton.onClick.AddListener(ToggleSimulation);
        if (resetButton != null) resetButton.onClick.AddListener(ResetSimulation);
        if (saveButton != null) saveButton.onClick.AddListener(SaveImage);
        if (compareButton != null) compareButton.onClick.AddListener(CompareExperiment);
        if (recordButton != null) recordButton.onClick.AddListener(ToggleRecording);

        if (colorButtons != null)
        {
            foreach (var btn in colorButtons)
            {
                if (btn == null) continue;
                Button captured = btn;
                captured.onClick.AddListener(() => OnColorButtonClicked(captured));
            }
        }

        RefreshFromSimulation();
        RefreshSimulationButtonLabel();
    }

    void AutoResolveReferences()
    {
        if (simController == null) simController = FindFirstObjectByType<SimulationController>();
        if (sphManager == null) sphManager = FindFirstObjectByType<SPHManager>();
        if (bucketVolume == null) bucketVolume = FindFirstObjectByType<BucketVolume>();
        if (paintingCollision == null) paintingCollision = FindFirstObjectByType<PaintingCollision>();
        if (paintRenderer == null) paintRenderer = FindFirstObjectByType<FluidParticleRenderer>();
    }

    void Update()
    {
        if (simController == null)
        {
            return;
        }

        if (outputTime != null)
        {
            outputTime.text = $"⏱ Time: {Time.timeSinceLevelLoad:F1}s";
        }

        if (outputParticles != null && sphManager != null)
        {
            outputParticles.text = $"🧪 Particles: {sphManager.ParticleCount}";
        }

        if (outputSpeed != null)
        {
            outputSpeed.text = $"🚀 Speed: {simController.bucketVelocity.magnitude:F2} m/s";
        }

        if (outputCoverage != null)
        {
            outputCoverage.text = "📐 Coverage: live";
        }

        if (isRecording)
        {
            CaptureRecordingFrame();
        }
    }

    void RegisterInput(InputField field, System.Action<string> handler)
    {
        if (field != null)
        {
            field.onEndEdit.AddListener(value => handler(value));
        }
    }

    void ApplyChange(string field, string value)
    {
        if (!float.TryParse(value, out float parsed))
        {
            RefreshFromSimulation();
            return;
        }

        switch (field)
        {
            case "Length":
                simController?.SetRopeLength(parsed);
                break;
            case "Angle":
                simController?.SetInitialAngle(parsed);
                break;
            case "Phi":
                simController?.SetInitialPhi(parsed);
                break;
            case "Damping":
                simController?.SetRopeDamping(parsed);
                break;
            case "Mass":
                simController?.SetBucketMass(parsed);
                break;
            case "Radius":
                if (bucketVolume != null)
                {
                    bucketVolume.SetRadius(parsed);
                }
                break;
            case "Hole":
                if (bucketVolume != null)
                {
                    bucketVolume.SetNozzleRadius(parsed);
                }
                break;
            case "Viscosity":
                if (sphManager != null)
                {
                    sphManager.viscosityStrength = Mathf.Clamp(parsed, 0.05f, 10f);
                }
                break;
            case "Gravity":
                simController?.SetGravity(parsed);
                if (sphManager != null)
                {
                    sphManager.gravity = -Mathf.Abs(parsed);
                }
                break;
            case "Tilt":
                ApplyTilt(parsed);
                break;
        }

        RefreshFromSimulation();
    }

    void ApplyTilt(float value)
    {
        Transform plane = paintingCollision != null ? paintingCollision.plane : GameObject.Find("Plane")?.transform;
        if (plane != null)
        {
            plane.localRotation = Quaternion.Euler(Mathf.Clamp(value, -45f, 45f), 0f, 0f);
        }
    }

    void OnSurfaceChanged(int index)
    {
        if (paintingCollision == null)
        {
            return;
        }

        paintingCollision.surfaceMaterial = CreateSurfaceMaterial(index);
    }

    SurfaceMaterial CreateSurfaceMaterial(int index)
    {
        SurfaceMaterial material = ScriptableObject.CreateInstance<SurfaceMaterial>();

        switch (index)
        {
            case 1: // Wood
                material.restitution = 0.2f;
                material.friction = 0.7f;
                material.roughness = 0.3f;
                material.absorption = 0.8f;
                break;
            case 2: // Metal
                material.restitution = 0.85f;
                material.friction = 0.15f;
                material.roughness = 0.05f;
                material.absorption = 0.05f;
                break;
            case 3: // Fabric
                material.restitution = 0.05f;
                material.friction = 0.9f;
                material.roughness = 0.8f;
                material.absorption = 0.9f;
                break;
            case 4: // Glass
                material.restitution = 0.95f;
                material.friction = 0.08f;
                material.roughness = 0.02f;
                material.absorption = 0.02f;
                break;
            case 5: // Plastic
                material.restitution = 0.35f;
                material.friction = 0.25f;
                material.roughness = 0.12f;
                material.absorption = 0.15f;
                break;
            case 6: // Stone
                material.restitution = 0.12f;
                material.friction = 0.82f;
                material.roughness = 0.55f;
                material.absorption = 0.65f;
                break;
            case 7: // Matte
                material.restitution = 0.08f;
                material.friction = 0.6f;
                material.roughness = 0.35f;
                material.absorption = 0.45f;
                break;
            default: // Paper
                material.restitution = 0.1f;
                material.friction = 0.55f;
                material.roughness = 0.2f;
                material.absorption = 0.6f;
                break;
        }

        return material;
    }

    void OnColorButtonClicked(Button btn)
    {
        if (btn != null && btn.targetGraphic is Image img && colorPreview != null)
        {
            colorPreview.color = img.color;
            ApplyPaintColor(img.color);
        }
    }

    void ApplyPaintColor(Color color)
    {
        if (paintRenderer == null)
        {
            paintRenderer = FindFirstObjectByType<FluidParticleRenderer>();
        }

        if (paintRenderer != null)
        {
            paintRenderer.SetPaintColor(color);
        }

        if (inputColorHex != null)
        {
            inputColorHex.text = ColorUtility.ToHtmlStringRGBA(color);
        }
    }

    void ApplyChangeColorHex(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string normalized = value.Trim();
        if (!normalized.StartsWith("#"))
        {
            normalized = "#" + normalized;
        }

        if (ColorUtility.TryParseHtmlString(normalized, out Color parsedColor))
        {
            if (colorPreview != null)
            {
                colorPreview.color = parsedColor;
            }

            ApplyPaintColor(parsedColor);
        }
    }

    void ToggleSimulation()
    {
        isSimulating = !isSimulating;
        Time.timeScale = isSimulating ? 1f : 0f;

        if (startButton != null)
        {
            RefreshSimulationButtonLabel();
        }
    }

    void ResetSimulation()
    {
        Time.timeScale = 1f;
        isSimulating = true;

        if (simController != null)
        {
            simController.RebuildRope();
        }

        if (sphManager != null)
        {
            sphManager.ResetSimulation();
        }

        if (paintRenderer != null)
        {
            paintRenderer.ClearPaint();
        }

        if (startButton != null)
        {
            RefreshSimulationButtonLabel();
        }
    }

    void RefreshSimulationButtonLabel()
    {
        if (startButton != null)
        {
            Text label = startButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = isSimulating ? "⏸ Pause" : "▶ Start";
            }
        }
    }

    void SaveImage()
    {
        string directory = System.IO.Path.Combine(Application.persistentDataPath, "Exports");
        System.IO.Directory.CreateDirectory(directory);

        string path = System.IO.Path.Combine(directory, $"Plane_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"Saved plane screenshot to {path}");
    }

    void CompareExperiment()
    {
        string directory = System.IO.Path.Combine(Application.persistentDataPath, "Exports");
        System.IO.Directory.CreateDirectory(directory);

        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string imagePath = System.IO.Path.Combine(directory, $"Compare_{stamp}.png");
        string settingsPath = System.IO.Path.Combine(directory, $"Compare_{stamp}.txt");
        ScreenCapture.CaptureScreenshot(imagePath);
        System.IO.File.WriteAllText(settingsPath, BuildSettingsSummary());
        Debug.Log($"Saved comparison image and settings to {directory}");
    }

    void ToggleRecording()
    {
        isRecording = !isRecording;
        if (isRecording)
        {
            recordingFrameIndex = 0;
            recordingDirectory = System.IO.Path.Combine(Application.persistentDataPath, $"VideoFrames_{System.DateTime.Now:yyyyMMdd_HHmmss}");
            System.IO.Directory.CreateDirectory(recordingDirectory);
        }

        RefreshRecordingButtonLabel();
    }

    void CaptureRecordingFrame()
    {
        if (string.IsNullOrEmpty(recordingDirectory))
        {
            return;
        }

        string framePath = System.IO.Path.Combine(recordingDirectory, $"frame_{recordingFrameIndex:00000}.png");
        ScreenCapture.CaptureScreenshot(framePath);
        recordingFrameIndex++;
    }

    void RefreshRecordingButtonLabel()
    {
        if (recordButton == null)
        {
            return;
        }

        Text label = recordButton.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.text = isRecording ? "■ Stop" : "⏺ Record";
        }
    }

    string BuildSettingsSummary()
    {
        List<string> lines = new List<string>
        {
            $"Length={inputLength?.text}",
            $"Angle={inputAngle?.text}",
            $"Phi={inputPhi?.text}",
            $"Damping={inputDamping?.text}",
            $"Mass={inputMass?.text}",
            $"Radius={inputRadius?.text}",
            $"Hole={inputHole?.text}",
            $"Viscosity={inputViscosity?.text}",
            $"Gravity={inputGravity?.text}",
            $"Tilt={inputTilt?.text}",
            $"Color={inputColorHex?.text}"
        };

        return string.Join(System.Environment.NewLine, lines);
    }

    public void RefreshFromSimulation()
    {
        if (simController == null)
        {
            return;
        }

        if (inputLength != null) inputLength.text = simController.RopeLength.ToString("F2");
        if (inputAngle != null) inputAngle.text = simController.InitialAngle.ToString("F2");
        if (inputPhi != null) inputPhi.text = simController.InitialPhi.ToString("F2");
        if (inputDamping != null) inputDamping.text = simController.RopeDamping.ToString("F3");
        if (inputMass != null) inputMass.text = simController.BucketMass.ToString("F2");
        if (inputGravity != null) inputGravity.text = simController.Gravity.ToString("F2");
        if (inputTilt != null)
        {
            Transform plane = paintingCollision != null ? paintingCollision.plane : null;
            inputTilt.text = (plane != null ? NormalizeSignedAngle(plane.localEulerAngles.x) : 0f).ToString("F2");
        }

        if (bucketVolume != null)
        {
            if (inputRadius != null) inputRadius.text = bucketVolume.radius.ToString("F2");
            if (inputHole != null) inputHole.text = (bucketVolume.nozzleRadius * 2f).ToString("F3");
        }

        if (sphManager != null && inputViscosity != null)
        {
            inputViscosity.text = sphManager.viscosityStrength.ToString("F2");
        }

        if (paintRenderer != null && inputColorHex != null)
        {
            inputColorHex.text = ColorUtility.ToHtmlStringRGBA(paintRenderer.paintColor);
        }
    }

    float NormalizeSignedAngle(float angle)
    {
        angle %= 360f;
        return angle > 180f ? angle - 360f : angle;
    }
}
