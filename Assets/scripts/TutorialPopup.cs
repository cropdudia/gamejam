using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialPopup : MonoBehaviour
{
    [SerializeField, Min(0f)] private float displayDuration = 4f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.25f;
    [SerializeField] private Vector2 panelSize = new Vector2(520f, 150f);
    [SerializeField] private Vector2 screenPadding = new Vector2(36f, 36f);
    [SerializeField] private TMP_FontAsset fontAsset;

    private static TutorialPopup instance;

    private CanvasGroup canvasGroup;
    private TMP_Text messageText;
    private Coroutine currentMessage;
    private GameObject popupCanvasObject;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("Multiple TutorialPopup services are active. Only one is supported.", this);
            enabled = false;
            return;
        }

        instance = this;
        CreatePopupUI();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (popupCanvasObject != null)
        {
            Destroy(popupCanvasObject);
        }
    }

    public static void ShowMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            Debug.LogWarning("Tutorial popup message was empty.");
            return;
        }

        if (instance == null)
        {
            Debug.LogWarning("Cannot show tutorial popup: no active TutorialPopup service exists.");
            return;
        }

        instance.DisplayMessage(message);
    }

    private void CreatePopupUI()
    {
        popupCanvasObject = new GameObject("Tutorial Popup Canvas");
        GameObject canvasObject = popupCanvasObject;

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("Tutorial Popup");
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelTransform = panelObject.AddComponent<RectTransform>();
        panelTransform.anchorMin = new Vector2(0f, 1f);
        panelTransform.anchorMax = new Vector2(0f, 1f);
        panelTransform.pivot = new Vector2(0f, 1f);
        panelTransform.anchoredPosition = new Vector2(screenPadding.x, -screenPadding.y);
        panelTransform.sizeDelta = panelSize;

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.75f);
        panelImage.raycastTarget = false;

        canvasGroup = panelObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject textObject = new GameObject("Message");
        textObject.transform.SetParent(panelObject.transform, false);

        RectTransform textTransform = textObject.AddComponent<RectTransform>();
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
        textTransform.offsetMin = new Vector2(24f, 18f);
        textTransform.offsetMax = new Vector2(-24f, -18f);

        messageText = textObject.AddComponent<TextMeshProUGUI>();
        messageText.font = fontAsset;
        messageText.color = Color.white;
        messageText.fontSize = 30f;
        messageText.alignment = TextAlignmentOptions.TopLeft;
        messageText.textWrappingMode = TextWrappingModes.Normal;
        messageText.raycastTarget = false;
    }

    private void DisplayMessage(string message)
    {
        messageText.text = message;
        if (currentMessage != null)
        {
            StopCoroutine(currentMessage);
        }

        currentMessage = StartCoroutine(ShowAndHideMessage());
    }

    private IEnumerator ShowAndHideMessage()
    {
        yield return FadeCanvasGroup(1f);

        if (displayDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(displayDuration);
        }

        yield return FadeCanvasGroup(0f);
        messageText.text = string.Empty;
        currentMessage = null;
    }

    private IEnumerator FadeCanvasGroup(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        if (fadeDuration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }
}
