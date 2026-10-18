using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates a simple mana bar UI at runtime if one is not assigned on the SunController.
/// Attach this to the same GameObject as SunController or leave in scene. It will find the SunController automatically.
/// </summary>
public class SunManaUI : MonoBehaviour
{
    [Tooltip("Reference to the SunController. If not set, will try to find one in the scene.")]
    public SunController sunController;
    [Header("UI Variables")]
    [SerializeField] private GameObject manaFillBar;
    [SerializeField] private RectTransform manaFillBarRect;
    [SerializeField] private Vector2 maxSize = new Vector2(150f, 20f);
    [SerializeField] private Vector2 minSize = new Vector2(0f, 20f);

    void Update()
    {
        manaFillBarRect.sizeDelta = Vector2.Lerp(minSize, maxSize, sunController.currentMana / sunController.totalMana);
    }
}
