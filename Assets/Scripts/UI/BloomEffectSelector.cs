using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class BloomEffectSelector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI")]
    [SerializeField] private Image glowImage;
    [SerializeField] private Button button;

    [Header("Bloom")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private float normalBloomIntensity = 0.2f;
    [SerializeField] private float hoverBloomIntensity = 2.5f;
    [SerializeField] private float pressedBloomIntensity = 4f;
    [SerializeField] private float transitionSpeed = 12f;

    [Header("Image Glow")]
    [SerializeField] private Color normalColor = Color.white;
    [ColorUsage(true, true)]
    [SerializeField] private Color hoverColor = new Color(2.5f, 2.5f, 2.5f, 1f);
    [ColorUsage(true, true)]
    [SerializeField] private Color pressedColor = new Color(4f, 4f, 4f, 1f);

    [Header("Edge Glow")]
    [SerializeField] private bool createEdgeGlow = true;
    [SerializeField] private float edgeGlowScale = 1.12f;
    [SerializeField] private float edgeGlowHoverAlpha = 0.45f;
    [SerializeField] private float edgeGlowPressedAlpha = 0.7f;
    [ColorUsage(true, true)]
    [SerializeField] private Color edgeGlowColor = new Color(4f, 3.4f, 1.2f, 1f);

    private Bloom bloom;
    private Image edgeGlowImage;
    private float targetBloomIntensity;
    private Color targetImageColor;
    private float targetEdgeGlowAlpha;
    private bool pointerInside;

    private void Awake()
    {
        if (glowImage == null)
        {
            glowImage = GetComponent<Image>();
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null && glowImage != null)
        {
            button.targetGraphic = glowImage;
        }

        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out bloom);
        }

        targetBloomIntensity = normalBloomIntensity;
        targetImageColor = glowImage.color;

        if (glowImage != null)
        {
            // glowImage.color = normalColor;
            glowImage.raycastTarget = true;
        }

        if (createEdgeGlow)
        {
            CreateEdgeGlowImage();
        }

        SetBloomIntensity(normalBloomIntensity);
    }

    private void Update()
    {
        if (glowImage != null)
        {
            glowImage.color = Color.Lerp(glowImage.color, targetImageColor, Time.unscaledDeltaTime * transitionSpeed);
        }

        if (bloom != null)
        {
            bloom.intensity.value = Mathf.Lerp(bloom.intensity.value, targetBloomIntensity, Time.unscaledDeltaTime * transitionSpeed);
        }

        if (edgeGlowImage != null)
        {
            Color edgeColor = edgeGlowColor;
            edgeColor.a = targetEdgeGlowAlpha;
            edgeGlowImage.color = Color.Lerp(edgeGlowImage.color, edgeColor, Time.unscaledDeltaTime * transitionSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable())
        {
            return;
        }

        pointerInside = true;
        targetImageColor = hoverColor;
        targetBloomIntensity = hoverBloomIntensity;
        targetEdgeGlowAlpha = edgeGlowHoverAlpha;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        targetImageColor = normalColor;
        targetBloomIntensity = normalBloomIntensity;
        targetEdgeGlowAlpha = 0f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsInteractable())
        {
            return;
        }

        targetImageColor = pressedColor;
        targetBloomIntensity = pressedBloomIntensity;
        targetEdgeGlowAlpha = edgeGlowPressedAlpha;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!IsInteractable())
        {
            return;
        }

        targetImageColor = pointerInside ? hoverColor : normalColor;
        targetBloomIntensity = pointerInside ? hoverBloomIntensity : normalBloomIntensity;
        targetEdgeGlowAlpha = pointerInside ? edgeGlowHoverAlpha : 0f;
    }

    private bool IsInteractable()
    {
        return button == null || button.interactable;
    }

    private void SetBloomIntensity(float intensity)
    {
        if (bloom != null)
        {
            bloom.intensity.overrideState = true;
            bloom.intensity.value = intensity;
        }
    }

    private void CreateEdgeGlowImage()
    {
        if (glowImage == null || glowImage.sprite == null || edgeGlowImage != null)
        {
            return;
        }

        GameObject edgeGlowObject = new GameObject($"{name} Edge Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform glowRect = edgeGlowObject.GetComponent<RectTransform>();
        RectTransform sourceRect = glowImage.rectTransform;

        glowRect.SetParent(sourceRect.parent, false);
        glowRect.SetSiblingIndex(sourceRect.GetSiblingIndex());
        glowRect.anchorMin = sourceRect.anchorMin;
        glowRect.anchorMax = sourceRect.anchorMax;
        glowRect.anchoredPosition = sourceRect.anchoredPosition;
        glowRect.sizeDelta = sourceRect.sizeDelta;
        glowRect.pivot = sourceRect.pivot;
        glowRect.localRotation = sourceRect.localRotation;
        glowRect.localScale = sourceRect.localScale * edgeGlowScale;

        edgeGlowImage = edgeGlowObject.GetComponent<Image>();
        edgeGlowImage.sprite = glowImage.sprite;
        edgeGlowImage.type = glowImage.type;
        edgeGlowImage.preserveAspect = glowImage.preserveAspect;
        edgeGlowImage.raycastTarget = false;

        Color hiddenColor = edgeGlowColor;
        hiddenColor.a = 0f;
        edgeGlowImage.color = hiddenColor;
    }
}
