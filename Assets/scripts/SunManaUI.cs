using UnityEngine;
using UnityEngine.UI;

public class SunManaUI : MonoBehaviour
{
    [SerializeField] private SunController sunController;
    [SerializeField] private GameObject manaFillBar;
    [SerializeField] private RectTransform manaFillBarRect;
    [SerializeField] private Vector2 barSize = new Vector2(180f, 8f);
    [SerializeField, Min(0f)] private float bottomPadding = 28f;
    [SerializeField] private Color backgroundColor = new Color(0.04f, 0.06f, 0.07f, 0.55f);
    [SerializeField] private Color fillColor = new Color(0.35f, 0.78f, 0.58f, 0.9f);

    private RectTransform barRect;
    private RectTransform fillRect;
    private Image backgroundImage;
    private Image fillImage;

    private void Awake()
    {
        barRect = GetComponent<RectTransform>();
        fillRect = manaFillBarRect;
        if (manaFillBar != null)
        {
            fillImage = manaFillBar.GetComponent<Image>();
        }
        if (barRect != null)
        {
            backgroundImage = barRect.GetComponent<Image>();
        }

        if (sunController == null)
        {
            sunController = FindAnyObjectByType<SunController>();
        }

        if (barRect == null || fillRect == null)
        {
            CreateRuntimeBar();
        }

        ConfigureBar();
    }

    private void Update()
    {
        if (sunController == null)
        {
            sunController = FindAnyObjectByType<SunController>();
        }

        if (sunController == null || fillRect == null)
        {
            return;
        }

        float ratio = sunController.totalMana > 0f
            ? Mathf.Clamp01(sunController.currentMana / sunController.totalMana)
            : 0f;
        fillRect.sizeDelta = new Vector2(barSize.x * ratio, 0f);
    }

    public void SetSunController(SunController controller)
    {
        sunController = controller;
    }

    private void CreateRuntimeBar()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Mana HUD Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject barObject = new GameObject("Mana Bar");
        barObject.transform.SetParent(canvas.transform, false);
        barRect = barObject.AddComponent<RectTransform>();
        backgroundImage = barObject.AddComponent<Image>();
        backgroundImage.raycastTarget = false;

        GameObject fillObject = new GameObject("Mana Fill");
        fillObject.transform.SetParent(barRect, false);
        fillRect = fillObject.AddComponent<RectTransform>();
        fillImage = fillObject.AddComponent<Image>();
        fillImage.raycastTarget = false;
        manaFillBar = fillObject;
        manaFillBarRect = fillRect;
    }

    private void ConfigureBar()
    {
        if (barRect == null || fillRect == null)
        {
            Debug.LogError("Mana bar UI could not be created.", this);
            return;
        }

        barRect.anchorMin = new Vector2(0.5f, 0f);
        barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, bottomPadding);
        barRect.sizeDelta = barSize;

        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(barSize.x, 0f);

        for (int i = 0; i < barRect.childCount; i++)
        {
            Transform child = barRect.GetChild(i);
            if (child != fillRect)
            {
                child.gameObject.SetActive(false);
            }
        }

        if (backgroundImage == null)
        {
            backgroundImage = barRect.GetComponent<Image>();
        }

        if (fillImage == null)
        {
            fillImage = fillRect.GetComponent<Image>();
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
            backgroundImage.raycastTarget = false;
        }

        if (fillImage != null)
        {
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;
        }
    }
}
