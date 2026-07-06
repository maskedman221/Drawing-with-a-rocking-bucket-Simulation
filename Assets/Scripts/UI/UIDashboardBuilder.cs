using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIDashboardBuilder : MonoBehaviour
{
    const float DashboardWidth = 380f;
    const float DashboardHeight = 860f;
    const float ContentWidth = 344f;
    const float FallbackContentHeight = 1040f;

    public SimulationController simController;
    public SPHManager sphManager;
    public BucketVolume bucketVolume;
    public PaintingCollision paintingCollision;

    [SerializeField]
    private bool buildOnStart = true;

    private bool hasBuilt;

    void Start()
    {
        if (buildOnStart && !hasBuilt)
        {
            BuildDashboard();
        }
    }

    [ContextMenu("Build Dashboard")]
    public void BuildDashboard()
    {
        hasBuilt = true;

        if (simController == null) simController = FindFirstObjectByType<SimulationController>();
        if (sphManager == null) sphManager = FindFirstObjectByType<SPHManager>();
        if (bucketVolume == null) bucketVolume = FindFirstObjectByType<BucketVolume>();
        if (paintingCollision == null) paintingCollision = FindFirstObjectByType<PaintingCollision>();

        GameObject dashboardObject = GameObject.Find("Dashboard");
        Canvas canvas = dashboardObject != null ? dashboardObject.GetComponent<Canvas>() : null;
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Dashboard");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            canvas.sortingOrder = 100;
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        ClearPreviousDashboard(canvas.transform);

        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        UIManager old = canvas.GetComponent<UIManager>();
        if (old != null)
        {
            if (Application.isPlaying)
            {
                Destroy(old);
            }
            else
            {
                DestroyImmediate(old);
            }
        }

        UIManager uiManager = canvas.gameObject.AddComponent<UIManager>();
        uiManager.simController = simController;
        uiManager.sphManager = sphManager;
        uiManager.bucketVolume = bucketVolume;
        uiManager.paintingCollision = paintingCollision;
        uiManager.paintRenderer = FindFirstObjectByType<FluidParticleRenderer>();

        GameObject scrollRectObj = CreatePanel("ScrollRect", canvas.transform);
        Image scrollBackground = scrollRectObj.AddComponent<Image>();
        scrollBackground.color = new Color(0.06f, 0.08f, 0.12f, 0.96f);
        RectTransform scrollRT = scrollRectObj.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 1f);
        scrollRT.anchorMax = new Vector2(0f, 1f);
        scrollRT.pivot = new Vector2(0f, 1f);
        scrollRT.anchoredPosition = new Vector2(18f, -18f);
        scrollRT.sizeDelta = new Vector2(DashboardWidth, DashboardHeight);

        ScrollRect scrollRect = scrollRectObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 24f;

        GameObject viewport = CreatePanel("Viewport", scrollRectObj.transform);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = Color.white;
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        RectTransform viewportRT = viewport.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = new Vector2(12f, 12f);
        viewportRT.offsetMax = new Vector2(-22f, -12f);

        GameObject content = CreatePanel("Content", viewport.transform);
        RectTransform contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        LayoutElement contentElement = content.AddComponent<LayoutElement>();
        contentElement.minWidth = ContentWidth;
        contentElement.preferredWidth = ContentWidth;

        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 10;
        contentLayout.padding = new RectOffset(0, 0, 0, 0);
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;

        ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject controlPanel = CreatePanel("ControlPanel", content.transform);
        RectTransform panelRT = controlPanel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0f, 1f);
        panelRT.anchorMax = new Vector2(0f, 1f);
        panelRT.pivot = new Vector2(0f, 1f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta = new Vector2(ContentWidth, 0f);

        LayoutElement panelElement = controlPanel.AddComponent<LayoutElement>();
        panelElement.preferredWidth = ContentWidth;
        panelElement.minWidth = ContentWidth;
        panelElement.flexibleWidth = 0f;
        panelElement.minHeight = 720f;
        Image panelImage = controlPanel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.11f, 0.16f, 0.94f);

        VerticalLayoutGroup layout = controlPanel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        ContentSizeFitter fitter = controlPanel.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;
        scrollRect.verticalScrollbar = CreateScrollbar(scrollRectObj.transform, true);
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        Text title = CreateText("Title", controlPanel.transform, "Dashboard");
        StyleTitle(title);

        BuildSection(controlPanel.transform, uiManager, "Header_A", "Section_A", "Rope & Motion", new[]
        {
            ("Length", "inputLength", "2.50", "Row_Length"),
            ("Angle", "inputAngle", "45", "Row_Angle"),
            ("Phi", "inputPhi", "0", "Row_Phi"),
            ("Damping", "inputDamping", "0.995", "Row_Damping")
        });

        GameObject sectionB = BuildSection(controlPanel.transform, uiManager, "Header_B", "Section_B", "Bucket & Paint", new[]
        {
            ("Mass", "inputMass", "3", "Row_Mass"),
            ("Radius", "inputRadius", "0.90", "Row_Radius"),
            ("Hole Diameter", "inputHole", "0.03", "Row_HoleDiameter"),
            ("Viscosity", "inputViscosity", "0.15", "Row_Viscosity")
        });
        BuildColorPicker(sectionB.transform, uiManager);

        GameObject sectionC = BuildSection(controlPanel.transform, uiManager, "Header_C", "Section_C", "Environment", new[]
        {
            ("Gravity", "inputGravity", "9.81", "Row_Gravity"),
            ("Tilt", "inputTilt", "0", "Row_Tilt")
        });
        BuildSurfaceDropdown(sectionC.transform, uiManager);

        BuildOutputs(controlPanel.transform, uiManager);
        BuildButtons(controlPanel.transform, uiManager);

        Canvas.ForceUpdateCanvases();
        RectTransform controlPanelRT = controlPanel.GetComponent<RectTransform>();
        ApplyManualControlPanelLayout(controlPanelRT);
        LayoutRebuilder.ForceRebuildLayoutImmediate(controlPanelRT);
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRT);
        LockContentHeight(controlPanelRT, contentRT, panelElement, contentElement);
        ApplyManualControlPanelLayout(controlPanelRT);
        LayoutRebuilder.ForceRebuildLayoutImmediate(controlPanelRT);
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRT);
        DisableAutoLayout(controlPanelRT);
        DisableAutoLayout(contentRT);
        ApplyManualControlPanelLayout(controlPanelRT);
        LockContentHeight(controlPanelRT, contentRT, panelElement, contentElement);
        scrollRect.verticalNormalizedPosition = 1f;
        scrollRect.horizontalNormalizedPosition = 0f;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;

        uiManager.Initialize();
        Debug.Log("Dashboard built with live simulation links.");
    }

    void LockContentHeight(RectTransform controlPanelRT, RectTransform contentRT, LayoutElement panelElement, LayoutElement contentElement)
    {
        float preferredHeight = LayoutUtility.GetPreferredHeight(controlPanelRT);
        float minHeight = LayoutUtility.GetMinHeight(controlPanelRT);
        float finalHeight = Mathf.Max(preferredHeight, minHeight, FallbackContentHeight);

        panelElement.minHeight = finalHeight;
        panelElement.preferredHeight = finalHeight;
        contentElement.minHeight = finalHeight;
        contentElement.preferredHeight = finalHeight;

        controlPanelRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ContentWidth);
        controlPanelRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalHeight);
        contentRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ContentWidth);
        contentRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalHeight);
        contentRT.anchoredPosition = Vector2.zero;
    }

    void DisableAutoLayout(RectTransform root)
    {
        LayoutGroup[] layoutGroups = root.GetComponentsInChildren<LayoutGroup>(true);
        foreach (LayoutGroup layoutGroup in layoutGroups)
        {
            layoutGroup.enabled = false;
        }

        ContentSizeFitter[] fitters = root.GetComponentsInChildren<ContentSizeFitter>(true);
        foreach (ContentSizeFitter fitter in fitters)
        {
            fitter.enabled = false;
        }
    }

    void ApplyManualControlPanelLayout(RectTransform controlPanelRT)
    {
        float y = -14f;
        float innerWidth = ContentWidth - 28f;

        for (int i = 0; i < controlPanelRT.childCount; i++)
        {
            RectTransform child = controlPanelRT.GetChild(i) as RectTransform;
            if (child == null)
            {
                continue;
            }

            float height = GetManualHeight(child);
            child.anchorMin = new Vector2(0f, 1f);
            child.anchorMax = new Vector2(0f, 1f);
            child.pivot = new Vector2(0f, 1f);
            child.anchoredPosition = new Vector2(14f, y);
            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, innerWidth);
            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            ForceFillDirectText(child);
            ApplyManualNestedLayout(child);

            y -= height + 10f;
        }

        float totalHeight = Mathf.Abs(y) + 14f;
        controlPanelRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ContentWidth);
        controlPanelRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(totalHeight, FallbackContentHeight));
    }

    float GetManualHeight(RectTransform child)
    {
        switch (child.name)
        {
            case "Title":
                return 44f;
            case "Header_A":
            case "Header_B":
            case "Header_C":
            case "Header_C_Surfaces":
            case "Header_D":
                return 30f;
            case "Section_Header_A":
            case "Section_Header_B":
                return 184f;
            case "Section_Header_C":
                return 96f;
            case "ColorPickerContainer":
                return 96f;
            case "Section_Surface":
                return 62f;
            case "Section_D":
                return 112f;
            case "ButtonPanel":
                return 52f;
            default:
                return Mathf.Max(30f, child.rect.height);
        }
    }

    void ForceFillDirectText(RectTransform root)
    {
        Text text = root.GetComponent<Text>();
        if (text != null)
        {
            StretchToParent(text.rectTransform, 0f);
        }
    }

    void ApplyManualNestedLayout(RectTransform parent)
    {
        if (parent.name.StartsWith("Section_Header_"))
        {
            float y = -10f;
            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform row = parent.GetChild(i) as RectTransform;
                if (row == null)
                {
                    continue;
                }

                SetTopLeft(row, 10f, y, parent.rect.width - 20f, 38f);
                LayoutInputRow(row);
                y -= 44f;
            }
        }
        else if (parent.name == "ColorPickerContainer")
        {
            RectTransform header = parent.Find("Header_Color") as RectTransform;
            RectTransform row = parent.Find("ColorRow") as RectTransform;
            if (header != null)
            {
                SetTopLeft(header, 10f, -10f, parent.rect.width - 20f, 30f);
                ForceFillDirectText(header);
            }

            if (row != null)
            {
                SetTopLeft(row, 10f, -48f, parent.rect.width - 20f, 38f);
                LayoutHorizontalFixed(row, 34f, 8f);
            }
        }
        else if (parent.name == "Section_Surface")
        {
            RectTransform dropdown = parent.Find("Dropdown_Surface") as RectTransform;
            if (dropdown != null)
            {
                SetTopLeft(dropdown, 10f, -14f, parent.rect.width - 20f, 34f);
                RectTransform caption = dropdown.Find("Caption") as RectTransform;
                if (caption != null)
                {
                    StretchToParent(caption, 8f);
                }
            }
        }
        else if (parent.name == "Section_D")
        {
            float y = -10f;
            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform output = parent.GetChild(i) as RectTransform;
                if (output == null)
                {
                    continue;
                }

                SetTopLeft(output, 10f, y, parent.rect.width - 20f, 22f);
                ForceFillDirectText(output);
                y -= 26f;
            }
        }
        else if (parent.name == "ButtonPanel")
        {
            LayoutHorizontalFixed(parent, 72f, 8f);
        }
    }

    void LayoutInputRow(RectTransform row)
    {
        RectTransform label = row.childCount > 0 ? row.GetChild(0) as RectTransform : null;
        RectTransform input = row.childCount > 1 ? row.GetChild(1) as RectTransform : null;

        if (label != null)
        {
            SetTopLeft(label, 8f, -6f, 145f, 26f);
            ForceFillDirectText(label);
        }

        if (input != null)
        {
            SetTopLeft(input, row.rect.width - 128f, -4f, 120f, 30f);
            RectTransform text = input.Find("Text") as RectTransform;
            RectTransform placeholder = input.Find("Placeholder") as RectTransform;
            if (text != null)
            {
                StretchToParent(text, 8f);
            }

            if (placeholder != null)
            {
                StretchToParent(placeholder, 8f);
            }
        }
    }

    void LayoutHorizontalFixed(RectTransform parent, float itemWidth, float spacing)
    {
        float x = 8f;
        float height = Mathf.Max(1f, parent.rect.height - 8f);
        for (int i = 0; i < parent.childCount; i++)
        {
            RectTransform child = parent.GetChild(i) as RectTransform;
            if (child == null)
            {
                continue;
            }

            SetTopLeft(child, x, -4f, itemWidth, height);
            ForceFillDirectText(child);
            x += itemWidth + spacing;
        }
    }

    void SetTopLeft(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1f, width));
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1f, height));
    }

    void ClearPreviousDashboard(Transform canvasTransform)
    {
        List<GameObject> toRemove = new List<GameObject>();
        foreach (Transform child in canvasTransform)
        {
            if (child.name == "Backdrop" || child.name == "ScrollRect")
            {
                toRemove.Add(child.gameObject);
            }
        }

        foreach (GameObject go in toRemove)
        {
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }
    }

    GameObject BuildSection(Transform parent, UIManager uiManager, string headerName, string sectionName, string headerLabel, (string label, string fieldName, string defaultValue, string rowName)[] rows)
    {
        Text header = CreateText(headerName, parent, headerLabel);
        StyleSectionHeader(header);

        GameObject section = CreatePanel(sectionName, parent);
        ApplySectionStyling(section, new Color(0.11f, 0.15f, 0.22f, 0.92f));
        LayoutElement sectionElement = section.AddComponent<LayoutElement>();
        sectionElement.minHeight = 20f + rows.Length * 38f + Mathf.Max(0, rows.Length - 1) * 6f;

        VerticalLayoutGroup layout = section.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        section.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (var row in rows)
        {
            GameObject rowPanel = CreatePanel(row.rowName, section.transform);
            ApplyRowStyling(rowPanel);
            LayoutElement rowElement = rowPanel.AddComponent<LayoutElement>();
            rowElement.minHeight = 38f;
            rowElement.preferredHeight = 38f;

            HorizontalLayoutGroup rowLayout = rowPanel.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10;
            rowLayout.padding = new RectOffset(8, 8, 6, 6);
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            Text label = CreateText("Label", rowPanel.transform, row.label);
            label.fontSize = 13;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(0.92f, 0.94f, 0.98f, 1f);
            label.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 26f);
            LayoutElement labelElement = label.gameObject.AddComponent<LayoutElement>();
            labelElement.minWidth = 145f;
            labelElement.preferredWidth = 145f;
            labelElement.minHeight = 26f;

            InputField inputField = CreateInputField("Input", rowPanel.transform, row.defaultValue);
            var targetField = typeof(UIManager).GetField(row.fieldName);
            if (targetField != null)
            {
                targetField.SetValue(uiManager, inputField);
            }
        }

        return section;
    }

    void BuildColorPicker(Transform parent, UIManager uiManager)
    {
        GameObject picker = CreatePanel("ColorPickerContainer", parent);
        ApplyRowStyling(picker);
        LayoutElement pickerElement = picker.AddComponent<LayoutElement>();
        pickerElement.minHeight = 112f;

        VerticalLayoutGroup pickerLayout = picker.AddComponent<VerticalLayoutGroup>();
        pickerLayout.spacing = 10;
        pickerLayout.padding = new RectOffset(8, 8, 8, 8);
        pickerLayout.childForceExpandHeight = false;
        pickerLayout.childForceExpandWidth = true;
        pickerLayout.childControlHeight = true;
        pickerLayout.childControlWidth = true;

        Text pickerHeader = CreateText("Header_ColorPicker", picker.transform, "Color Picker");
        StyleSectionHeader(pickerHeader);

        GameObject previewObj = CreatePanel("ColorPreview", picker.transform);
        Image preview = previewObj.AddComponent<Image>();
        preview.color = new Color(0.72f, 0.18f, 0.18f, 1f);
        RectTransform previewRT = previewObj.GetComponent<RectTransform>();
        previewRT.sizeDelta = new Vector2(34f, 34f);
        LayoutElement previewElement = previewObj.AddComponent<LayoutElement>();
        previewElement.minWidth = 34f;
        previewElement.preferredWidth = 34f;
        previewElement.minHeight = 34f;
        previewElement.preferredHeight = 34f;
        uiManager.colorPreview = preview;

        uiManager.colorButtons = new List<Button>();
        Color[] colors =
        {
            new Color(0.92f, 0.24f, 0.22f),
            new Color(0.17f, 0.72f, 0.31f),
            new Color(0.20f, 0.49f, 0.94f),
            new Color(0.93f, 0.77f, 0.18f),
            new Color(0.76f, 0.26f, 0.88f),
            new Color(0.20f, 0.82f, 0.82f),
            new Color(1f, 1f, 1f),
            new Color(0f, 0f, 0f),
            new Color(1f, 0.55f, 0f),
            new Color(0.55f, 0.35f, 0.15f),
            new Color(1f, 0f, 0.55f),
            new Color(0.45f, 0.8f, 1f)
        };

        foreach (Color color in colors)
        {
            GameObject buttonObj = CreatePanel("PickColorButton", picker.transform);
            Button button = buttonObj.AddComponent<Button>();
            Image image = buttonObj.AddComponent<Image>();
            image.color = color;
            button.targetGraphic = image;
            RectTransform buttonRT = buttonObj.GetComponent<RectTransform>();
            buttonRT.sizeDelta = new Vector2(34f, 34f);
            LayoutElement buttonElement = buttonObj.AddComponent<LayoutElement>();
            buttonElement.minWidth = 34f;
            buttonElement.preferredWidth = 34f;
            buttonElement.minHeight = 34f;
            buttonElement.preferredHeight = 34f;
            uiManager.colorButtons.Add(button);
        }

        GameObject hexRow = CreatePanel("ColorHexRow", picker.transform);
        HorizontalLayoutGroup hexLayout = hexRow.AddComponent<HorizontalLayoutGroup>();
        hexLayout.spacing = 8;
        hexLayout.childAlignment = TextAnchor.MiddleLeft;
        hexLayout.childControlHeight = true;
        hexLayout.childControlWidth = true;

        Text hexLabel = CreateText("ColorHexLabel", hexRow.transform, "Hex");
        hexLabel.fontSize = 13;
        hexLabel.alignment = TextAnchor.MiddleLeft;
        hexLabel.color = new Color(0.92f, 0.94f, 0.98f, 1f);
        LayoutElement hexLabelElement = hexLabel.gameObject.AddComponent<LayoutElement>();
        hexLabelElement.minWidth = 36f;
        hexLabelElement.preferredWidth = 36f;

        InputField hexInput = CreateInputField("inputColorHex", hexRow.transform, "FFFFFFFF");
        uiManager.inputColorHex = hexInput;
    }

    void BuildSurfaceDropdown(Transform parent, UIManager uiManager)
    {
        GameObject dropdownObj = CreatePanel("Dropdown_Surface", parent);
        ApplyFieldStyling(dropdownObj, new Color(0.15f, 0.19f, 0.27f, 1f));
        Dropdown dropdown = dropdownObj.AddComponent<Dropdown>();
        dropdown.options = new List<Dropdown.OptionData>
        {
            new Dropdown.OptionData("Paper"),
            new Dropdown.OptionData("Wood"),
            new Dropdown.OptionData("Metal"),
            new Dropdown.OptionData("Fabric"),
            new Dropdown.OptionData("Glass"),
            new Dropdown.OptionData("Plastic"),
            new Dropdown.OptionData("Stone"),
            new Dropdown.OptionData("Matte")
        };
        dropdown.value = 0;
        dropdown.captionText = CreateText("Caption", dropdownObj.transform, "Paper");
        dropdown.captionText.fontSize = 13;
        dropdown.captionText.alignment = TextAnchor.MiddleLeft;
        dropdown.captionText.color = Color.white;
        RectTransform captionRT = dropdown.captionText.GetComponent<RectTransform>();
        captionRT.anchorMin = Vector2.zero;
        captionRT.anchorMax = Vector2.one;
        captionRT.offsetMin = new Vector2(10f, 0f);
        captionRT.offsetMax = new Vector2(-28f, 0f);
        dropdown.template = CreateDropdownTemplate(dropdownObj.transform, out Text itemText);
        dropdown.itemText = itemText;
        dropdownObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 34f);
        LayoutElement dropdownElement = dropdownObj.AddComponent<LayoutElement>();
        dropdownElement.minHeight = 34f;
        dropdownElement.preferredHeight = 34f;
        uiManager.surfaceDropdown = dropdown;
    }

    RectTransform CreateDropdownTemplate(Transform parent, out Text itemText)
    {
        GameObject templateObj = CreatePanel("Template", parent);
        templateObj.SetActive(false);
        Image templateImage = templateObj.AddComponent<Image>();
        templateImage.color = new Color(0.10f, 0.13f, 0.19f, 0.98f);
        ScrollRect scrollRect = templateObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        RectTransform templateRT = templateObj.GetComponent<RectTransform>();
        templateRT.anchorMin = new Vector2(0f, 0f);
        templateRT.anchorMax = new Vector2(1f, 0f);
        templateRT.pivot = new Vector2(0.5f, 1f);
        templateRT.anchoredPosition = new Vector2(0f, 2f);
        templateRT.sizeDelta = new Vector2(0f, 120f);

        GameObject viewport = CreatePanel("Viewport", templateObj.transform);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = Color.white;
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        RectTransform viewportRT = viewport.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;

        GameObject content = CreatePanel("Content", viewport.transform);
        RectTransform contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject item = CreatePanel("Item", content.transform);
        Toggle toggle = item.AddComponent<Toggle>();
        Image itemBackground = item.AddComponent<Image>();
        itemBackground.color = new Color(0.15f, 0.19f, 0.27f, 1f);
        toggle.targetGraphic = itemBackground;
        RectTransform itemRT = item.GetComponent<RectTransform>();
        itemRT.sizeDelta = new Vector2(0f, 30f);

        itemText = CreateText("Item Label", item.transform, "Option");
        itemText.fontSize = 13;
        itemText.alignment = TextAnchor.MiddleLeft;
        itemText.color = Color.white;
        RectTransform itemTextRT = itemText.GetComponent<RectTransform>();
        itemTextRT.anchorMin = Vector2.zero;
        itemTextRT.anchorMax = Vector2.one;
        itemTextRT.offsetMin = new Vector2(10f, 0f);
        itemTextRT.offsetMax = new Vector2(-10f, 0f);

        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;
        return templateRT;
    }

    void BuildOutputs(Transform parent, UIManager uiManager)
    {
        Text header = CreateText("Header_D", parent, "📊 Live Output");
        StyleSectionHeader(header);

        GameObject section = CreatePanel("Section_D", parent);
        ApplySectionStyling(section, new Color(0.11f, 0.15f, 0.22f, 0.92f));
        LayoutElement sectionElement = section.AddComponent<LayoutElement>();
        sectionElement.minHeight = 112f;

        VerticalLayoutGroup layout = section.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        section.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        uiManager.outputTime = CreateOutput(section.transform, "Output_Time", "⏱ Time: 0.0s");
        uiManager.outputParticles = CreateOutput(section.transform, "Output_Particles", "🧪 Particles: 0");
        uiManager.outputCoverage = CreateOutput(section.transform, "Output_Coverage", "📐 Coverage: live");
        uiManager.outputSpeed = CreateOutput(section.transform, "Output_Speed", "🚀 Speed: 0.0 m/s");
    }

    void BuildButtons(Transform parent, UIManager uiManager)
    {
        GameObject panel = CreatePanel("ButtonPanel", parent);
        ApplySectionStyling(panel, new Color(0.09f, 0.12f, 0.18f, 0.95f));
        LayoutElement panelElement = panel.AddComponent<LayoutElement>();
        panelElement.minHeight = 52f;

        HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        uiManager.startButton = CreateButton(panel.transform, "Button_Start", "▶ Start", new Color(0.18f, 0.51f, 0.83f));
        uiManager.resetButton = CreateButton(panel.transform, "Button_Reset", "⟲ Reset", new Color(0.33f, 0.36f, 0.40f));
        uiManager.saveButton = CreateButton(panel.transform, "Button_Save", "💾 Save", new Color(0.14f, 0.57f, 0.39f));
        uiManager.compareButton = CreateButton(panel.transform, "Button_Compare", "▣ Compare", new Color(0.64f, 0.41f, 0.15f));
        uiManager.recordButton = CreateButton(panel.transform, "Button_Record", "⏺ Record", new Color(0.60f, 0.28f, 0.75f));
    }

    InputField CreateInputField(string name, Transform parent, string defaultValue)
    {
        GameObject inputObj = CreatePanel(name, parent);
        ApplyFieldStyling(inputObj, new Color(0.16f, 0.20f, 0.28f, 1f));

        RectTransform inputRT = inputObj.GetComponent<RectTransform>();
        inputRT.sizeDelta = new Vector2(120f, 30f);
        LayoutElement inputElement = inputObj.AddComponent<LayoutElement>();
        inputElement.minWidth = 120f;
        inputElement.preferredWidth = 120f;
        inputElement.minHeight = 30f;
        inputElement.preferredHeight = 30f;

        InputField inputField = inputObj.AddComponent<InputField>();
        inputField.contentType = InputField.ContentType.Standard;

        Text text = CreateText("Text", inputObj.transform, defaultValue);
        text.alignment = TextAnchor.MiddleRight;
        text.fontSize = 13;
        text.color = Color.white;
        RectTransform textRT = text.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(8f, 0f);
        textRT.offsetMax = new Vector2(-8f, 0f);

        GameObject placeholderObj = CreatePanel("Placeholder", inputObj.transform);
        Text placeholder = placeholderObj.AddComponent<Text>();
        placeholder.text = defaultValue;
        placeholder.font = text.font;
        placeholder.fontSize = 13;
        placeholder.alignment = TextAnchor.MiddleRight;
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        RectTransform placeholderRT = placeholderObj.GetComponent<RectTransform>();
        placeholderRT.anchorMin = Vector2.zero;
        placeholderRT.anchorMax = Vector2.one;
        placeholderRT.offsetMin = new Vector2(8f, 0f);
        placeholderRT.offsetMax = new Vector2(-8f, 0f);

        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        return inputField;
    }

    Text CreateOutput(Transform parent, string name, string value)
    {
        Text text = CreateText(name, parent, value);
        text.fontSize = 13;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = new Color(0.92f, 0.94f, 0.98f, 1f);
        RectTransform rt = text.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 22f);
        LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 22f;
        element.preferredHeight = 22f;
        return text;
    }

    Button CreateButton(Transform parent, string name, string label, Color color)
    {
        GameObject buttonObj = CreatePanel(name, parent);
        Image image = buttonObj.AddComponent<Image>();
        image.color = color;
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text text = CreateText("Text", buttonObj.transform, label);
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 14;
        text.color = Color.white;
        RectTransform textRT = text.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        RectTransform buttonRT = buttonObj.GetComponent<RectTransform>();
        buttonRT.sizeDelta = new Vector2(0f, 36f);
        LayoutElement element = buttonObj.AddComponent<LayoutElement>();
        element.minHeight = 36f;
        element.preferredHeight = 36f;
        return button;
    }

    Scrollbar CreateScrollbar(Transform parent, bool vertical)
    {
        GameObject sbObj = CreatePanel(vertical ? "Scrollbar Vertical" : "Scrollbar Horizontal", parent);
        Image track = sbObj.AddComponent<Image>();
        track.color = new Color(0.12f, 0.14f, 0.18f, 0.9f);
        Scrollbar scrollbar = sbObj.AddComponent<Scrollbar>();

        RectTransform rt = sbObj.GetComponent<RectTransform>();
        if (vertical)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(12f, 0f);
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 12f);
        }

        GameObject handle = CreatePanel("Handle", sbObj.transform);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.46f, 0.56f, 0.74f, 0.95f);
        RectTransform handleRT = handle.GetComponent<RectTransform>();
        handleRT.anchorMin = Vector2.zero;
        handleRT.anchorMax = Vector2.one;
        handleRT.offsetMin = new Vector2(2f, 2f);
        handleRT.offsetMax = new Vector2(-2f, -2f);
        scrollbar.handleRect = handleRT;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = vertical ? Scrollbar.Direction.BottomToTop : Scrollbar.Direction.LeftToRight;

        return scrollbar;
    }

    GameObject CreatePanel(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    void StretchToParent(RectTransform rt, float padding)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    Text CreateText(string name, Transform parent, string text)
    {
        GameObject go = CreatePanel(name, parent);
        Text txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null)
        {
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 13);
        }
        txt.color = Color.white;
        txt.raycastTarget = false;
        StretchToParent(txt.rectTransform, 0f);

        if (txt.gameObject.GetComponent<LayoutElement>() == null)
        {
            LayoutElement element = txt.gameObject.AddComponent<LayoutElement>();
            element.minHeight = Mathf.Max(22f, txt.fontSize > 0 ? txt.fontSize + 6f : 22f);
            element.preferredHeight = element.minHeight;
        }
        return txt;
    }

    void ApplySectionStyling(GameObject element, Color color)
    {
        Image image = element.AddComponent<Image>();
        image.color = color;
        Outline outline = element.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.35f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    void ApplyRowStyling(GameObject element)
    {
        Image image = element.AddComponent<Image>();
        image.color = new Color(0.15f, 0.19f, 0.27f, 1f);
    }

    void ApplyFieldStyling(GameObject element, Color color)
    {
        Image image = element.AddComponent<Image>();
        image.color = color;
    }

    void StyleTitle(Text title)
    {
        title.fontSize = 24;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = new Color(0.98f, 0.99f, 1f, 1f);
        RectTransform rt = title.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 44f);
        LayoutElement element = title.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 44f;
        element.preferredHeight = 44f;
    }

    void StyleSectionHeader(Text header)
    {
        header.fontSize = 16;
        header.fontStyle = FontStyle.Bold;
        header.alignment = TextAnchor.MiddleLeft;
        header.color = new Color(0.82f, 0.88f, 1f, 1f);
        RectTransform rt = header.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 30f);
        LayoutElement element = header.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 30f;
        element.preferredHeight = 30f;
    }
}
