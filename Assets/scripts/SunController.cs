using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Light))]
public class SunController : MonoBehaviour
{
    public InputActionAsset inputActions;
    public float rotationSpeed = 30f;
    [SerializeField] private bool rotationEnabled;

    [Header("Mana")]
    // Total mana pool. Public so other scripts / UI can read or modify it.
    public float totalMana = 100f;
    // Current mana available. Public so external triggers can refill it.
    public float currentMana = 100f;
    // Mana drained per second while adjusting the sun (left/right).
    public float manaDrainPerSecondWhenChanging = 10f;

    private static readonly Vector2?[] CheckpointRotationOverrides =
    {
        null,
        new Vector2(55.6f, 0f),
        new Vector2(151f, -182f),
        new Vector2(128.1f, -268.4f),
        null
    };

    private static readonly Vector2 CheckpointOneRotation = new Vector2(41f, 0f);

    private float sunHeight;
    private float sunDirection;
    private InputAction sunUpAction;
    private InputAction sunDownAction;
    private InputAction sunLeftAction;
    private InputAction sunRightAction;

    public bool IsChangingDirection =>
        rotationEnabled &&
        ((sunLeftAction != null && sunLeftAction.IsPressed()) ||
         (sunRightAction != null && sunRightAction.IsPressed()));

    void Awake()
    {
        sunHeight = CheckpointOneRotation.x;
        sunDirection = CheckpointOneRotation.y;
        transform.rotation = Quaternion.Euler(sunHeight, sunDirection, 0f);
        if (inputActions == null)
        {
            Debug.LogError("SunController requires an Input Actions asset.", this);
            return;
        }

        sunUpAction = inputActions.FindAction("Player/Sun Up");
        sunDownAction = inputActions.FindAction("Player/Sun Down");
        sunLeftAction = inputActions.FindAction("Player/Sun Left");
        sunRightAction = inputActions.FindAction("Player/Sun Right");

        SunManaUI manaUI = FindAnyObjectByType<SunManaUI>();
        if (manaUI == null)
        {
            manaUI = gameObject.AddComponent<SunManaUI>();
        }

        manaUI.SetSunController(this);
    }

    void Update()
    {
        if (!rotationEnabled)
            return;

        if (sunUpAction == null || sunDownAction == null || sunLeftAction == null || sunRightAction == null)
            return;

        //float heightChange = 0f;
        //if (sunDownAction.IsPressed()) heightChange -= 1f;
        //if (sunUpAction.IsPressed()) heightChange += 1f;

        float directionChange = 0f;
        if (sunLeftAction.IsPressed()) directionChange -= 1f;
        if (sunRightAction.IsPressed()) directionChange += 1f;

        // If there's no mana, you cannot control the sun.
        if (currentMana <= 0f)
        {
            directionChange = 0f;
        }

        // Drain mana based on activity. Uses absolute direction input so holding either left or right drains mana.
        if (Mathf.Abs(directionChange) > 0f && currentMana > 0f)
        {
            float drain = manaDrainPerSecondWhenChanging * Mathf.Abs(directionChange) * Time.deltaTime;
            currentMana = Mathf.Max(0f, currentMana - drain);
        }

        //sunHeight = Mathf.Clamp(sunHeight + heightChange * rotationSpeed * Time.deltaTime, 0f, 90f);
        sunDirection += directionChange * rotationSpeed * Time.deltaTime;
        transform.rotation = Quaternion.Euler(sunHeight, sunDirection, 0f);
    }

    public void SetRotationEnabled(bool enabled)
    {
        rotationEnabled = enabled;
    }

    /// <summary>
    /// Returns true if the player currently has any mana and rotation is enabled.
    /// </summary>
    public bool CanControlSun()
    {
        return rotationEnabled && currentMana > 0f;
    }

    /// <summary>
    /// Attempts to spend the specified amount of mana. Returns true if mana was spent.
    /// External systems can call this to drain mana for other abilities.
    /// </summary>
    public bool SpendMana(float amount)
    {
        if (amount <= 0f) return true;
        if (currentMana <= 0f) return false;
        if (currentMana < amount) return false;
        currentMana = Mathf.Max(0f, currentMana - amount);
        return true;
    }

    /// <summary>
    /// Recharges (adds) mana. Public so external triggers can refill it.
    /// </summary>
    public void RechargeMana(float amount)
    {
        if (amount <= 0f) return;
        currentMana = Mathf.Min(totalMana, currentMana + amount);
    }

    public void SetCheckpoint(int checkpointIndex)
    {
        if (checkpointIndex < 0 || checkpointIndex >= CheckpointRotationOverrides.Length)
        {
            Debug.LogWarning($"Sun rotation is not configured for checkpoint index {checkpointIndex}.", this);
            return;
        }

        Vector2 rotation = CheckpointRotationOverrides[checkpointIndex] ?? CheckpointOneRotation;
        sunHeight = rotation.x;
        sunDirection = rotation.y;
        transform.rotation = Quaternion.Euler(sunHeight, sunDirection, 0f);
    }
}