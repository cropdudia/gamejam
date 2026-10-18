using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates a simple mana bar UI at runtime if one is not assigned on the SunController.
/// Attach this to the same GameObject as SunController or leave in scene. It will find the SunController automatically.
/// </summary>
public class HealthbarUI : MonoBehaviour
{
    [Tooltip("Reference to the Player.")]
    public GameObject Player;
    [Header("UI Variables")]
    [SerializeField] private GameObject healthFillBar;
    [SerializeField] private RectTransform healthFillBarRect;
    [SerializeField] private Vector2 maxSize = new Vector2(150f, 20f);
    [SerializeField] private Vector2 minSize = new Vector2(0f, 20f);

    void Update()
    {
        healthFillBarRect.sizeDelta = Vector2.Lerp(minSize, maxSize, Player.GetComponent<tpPlayer>().playerHealth / Player.GetComponent<tpPlayer>().maxHealth);
    }
}
