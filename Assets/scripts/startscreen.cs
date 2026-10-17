using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class startscreen : MonoBehaviour
{
    [Header("Title Screen Graphics")]
    [SerializeField] private Image logoImage;
    [SerializeField] private TMP_Text startPrompt;
    [SerializeField] private TMP_Text optionsPrompt;
    [SerializeField] private TMP_Text quitPrompt;
    [SerializeField, Min(0f)] private float logoFadeDuration = 1f;
    [SerializeField, Min(0f)] private float promptFadeDuration = 0.5f;
    [SerializeField, Min(0f)] private float startPromptDelay = 2f;
    [SerializeField, Min(0f)] private float otherPromptsDelay = 0.5f;

    [Header("Scenes")]
    [SerializeField] private string nextLevelScene = "fptest";
    [SerializeField] private string optionsScene = "Options";

    private Color logoBaseColor;
    private Color startPromptBaseColor;
    private Color optionsPromptBaseColor;
    private Color quitPromptBaseColor;
    private bool transitioning;

    private void Awake()
    {
        if (logoImage != null) logoBaseColor = logoImage.color;
        if (startPrompt != null) startPromptBaseColor = startPrompt.color;
        if (optionsPrompt != null) optionsPromptBaseColor = optionsPrompt.color;
        if (quitPrompt != null) quitPromptBaseColor = quitPrompt.color;

        SetAlpha(logoImage, logoBaseColor, 0f);
        SetAlpha(startPrompt, startPromptBaseColor, 0f);
        SetAlpha(optionsPrompt, optionsPromptBaseColor, 0f);
        SetAlpha(quitPrompt, quitPromptBaseColor, 0f);
    }

    private void Start()
    {
        StartCoroutine(PlayIntro());
    }

    private void Update()
    {
        if (transitioning)
        {
            return;
        }

        bool startPressed = false;
        bool optionsPressed = false;
        bool quitPressed = false;

        foreach (InputDevice device in InputSystem.devices)
        {
            foreach (InputControl control in device.allControls)
            {
                if (!(control is ButtonControl button) || !button.wasPressedThisFrame)
                {
                    continue;
                }

                if (device is Gamepad gamepad)
                {
                    quitPressed |= button == gamepad.buttonEast;
                    optionsPressed |= button == gamepad.buttonWest;
                }
                else if (device is Keyboard keyboard)
                {
                    quitPressed |= button == keyboard.escapeKey;
                    optionsPressed |= button == keyboard.oKey;
                }

                startPressed = true;
            }
        }

        if (quitPressed)
        {
            QuitGame();
        }
        else if (optionsPressed)
        {
            LoadScene(optionsScene);
        }
        else if (startPressed)
        {
            LoadScene(nextLevelScene);
        }
    }

    private IEnumerator PlayIntro()
    {
        yield return FadeIn(logoImage, logoBaseColor, logoFadeDuration);
        yield return new WaitForSecondsRealtime(startPromptDelay);
        yield return FadeIn(startPrompt, startPromptBaseColor, promptFadeDuration);
        yield return new WaitForSecondsRealtime(otherPromptsDelay);
        yield return FadeInOtherPrompts();
    }

    private IEnumerator FadeInOtherPrompts()
    {
        float elapsed = 0f;
        while (elapsed < promptFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = promptFadeDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / promptFadeDuration);
            SetAlpha(optionsPrompt, optionsPromptBaseColor, alpha);
            SetAlpha(quitPrompt, quitPromptBaseColor, alpha);
            yield return null;
        }

        SetAlpha(optionsPrompt, optionsPromptBaseColor, 1f);
        SetAlpha(quitPrompt, quitPromptBaseColor, 1f);
    }

    private static IEnumerator FadeIn(Graphic graphic, Color baseColor, float duration)
    {
        if (graphic == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            SetAlpha(graphic, baseColor, 1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(graphic, baseColor, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SetAlpha(graphic, baseColor, 1f);
    }

    private static void SetAlpha(Graphic graphic, Color baseColor, float alpha)
    {
        if (graphic == null)
        {
            return;
        }

        Color color = baseColor;
        color.a *= alpha;
        graphic.color = color;
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) ||
            !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"Cannot load title-screen destination '{sceneName}'. Add the scene to Build Settings or correct its name.",
                this);
            return;
        }

        transitioning = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private void QuitGame()
    {
        transitioning = true;
        Application.Quit();
    }
}
