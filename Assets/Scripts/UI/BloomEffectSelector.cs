using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(Button))]
public class BloomEffectSelector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ICanvasRaycastFilter
{
    [Header("Glow Settings")]
    public Image bucketImage;
    public Color glowColor = new Color(10f, 8f, 2f, 1f);
    public float glowScale = 1.05f;
    public float glowImageScale = 1.18f;
    [Range(0f, 1f)]
    public float glowAlpha = 0.45f;
    public bool fullImageRectClickable = true;
    
    private Color originalColor;
    private Vector3 originalScale;
    private Button button;
    private Coroutine clickFlashRoutine;
    private Image generatedGlowImage;
    private RectTransform generatedGlowRect;
    
    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.transition = Selectable.Transition.None;

        if (bucketImage == null)
            bucketImage = GetComponent<Image>();

        originalScale = transform.localScale;
        if (bucketImage == null)
        {
            Debug.LogWarning($"{nameof(BloomEffectSelector)} needs an Image assigned.", this);
            return;
        }

        originalColor = bucketImage.color;
        ConfigureRaycastTarget();
        EnsureGeneratedGlowImage();
        SetGlowVisible(false);

        if (button != null)
            button.targetGraphic = bucketImage;
    }

    void OnValidate()
    {
        if (bucketImage == null)
            bucketImage = GetComponent<Image>();

        if (bucketImage != null)
            ConfigureRaycastTarget();

        Button currentButton = GetComponent<Button>();
        if (currentButton != null && bucketImage != null)
        {
            currentButton.transition = Selectable.Transition.None;
            currentButton.targetGraphic = bucketImage;
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanInteract())
            return;
        
        bucketImage.color = GetDisplayGlowColor(originalColor.a);
        transform.localScale = originalScale * glowScale;
        SetGlowVisible(true);
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (bucketImage == null)
            return;

        bucketImage.color = originalColor;
        transform.localScale = originalScale;
        SetGlowVisible(false);
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!CanInteract())
            return;
        
        // Your scene loading logic here
        // SceneManager.LoadScene("YourSceneName");
        Debug.Log("Bucket clicked!");
        
        // Optional: Click feedback
        if (clickFlashRoutine != null)
            StopCoroutine(clickFlashRoutine);

        clickFlashRoutine = StartCoroutine(ClickFlash());
    }
    
    System.Collections.IEnumerator ClickFlash()
    {
        if (bucketImage == null)
            yield break;

        bucketImage.color = Color.white;
        transform.localScale = originalScale * 1.15f;
        yield return new WaitForSeconds(0.1f);
        
        if (button != null && button.interactable)
        {
            bucketImage.color = originalColor;
            transform.localScale = originalScale;
            SetGlowVisible(false);
        }

        clickFlashRoutine = null;
    }

    bool CanInteract()
    {
        return bucketImage != null && (button == null || button.interactable);
    }

    void EnsureGeneratedGlowImage()
    {
        if (generatedGlowImage != null || bucketImage == null)
            return;

        RectTransform sourceRect = bucketImage.rectTransform;
        Transform parent = sourceRect.parent;
        if (parent == null)
            return;

        GameObject glowObject = new GameObject($"{name} Hover Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        generatedGlowRect = glowObject.GetComponent<RectTransform>();
        generatedGlowRect.SetParent(parent, false);
        generatedGlowRect.SetSiblingIndex(sourceRect.GetSiblingIndex());

        generatedGlowImage = glowObject.GetComponent<Image>();
        generatedGlowImage.sprite = bucketImage.sprite;
        generatedGlowImage.type = bucketImage.type;
        generatedGlowImage.preserveAspect = bucketImage.preserveAspect;
        generatedGlowImage.raycastTarget = false;
        generatedGlowImage.useSpriteMesh = false;

        SyncGlowRect();
    }

    void SyncGlowRect()
    {
        if (generatedGlowRect == null || bucketImage == null)
            return;

        RectTransform sourceRect = bucketImage.rectTransform;
        generatedGlowRect.anchorMin = sourceRect.anchorMin;
        generatedGlowRect.anchorMax = sourceRect.anchorMax;
        generatedGlowRect.anchoredPosition = sourceRect.anchoredPosition;
        generatedGlowRect.sizeDelta = sourceRect.sizeDelta;
        generatedGlowRect.pivot = sourceRect.pivot;
        generatedGlowRect.localRotation = sourceRect.localRotation;
        generatedGlowRect.localScale = originalScale * glowScale * glowImageScale;
    }

    void SetGlowVisible(bool visible)
    {
        if (generatedGlowImage == null)
            return;

        SyncGlowRect();
        generatedGlowImage.enabled = visible;
        generatedGlowImage.color = GetDisplayGlowColor(glowAlpha);
    }

    Color GetDisplayGlowColor(float alpha)
    {
        float maxChannel = Mathf.Max(1f, glowColor.r, glowColor.g, glowColor.b);
        return new Color(
            glowColor.r / maxChannel,
            glowColor.g / maxChannel,
            glowColor.b / maxChannel,
            alpha
        );
    }

    void ConfigureRaycastTarget()
    {
        bucketImage.raycastTarget = true;
        bucketImage.useSpriteMesh = false;

        Image buttonImage = GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.raycastTarget = true;
            buttonImage.useSpriteMesh = false;
        }
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!fullImageRectClickable || bucketImage == null)
            return true;

        return RectTransformUtility.RectangleContainsScreenPoint(
            bucketImage.rectTransform,
            screenPoint,
            eventCamera
        );
    }
}
