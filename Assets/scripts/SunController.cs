using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Light))]
public class SunController : MonoBehaviour
{
    public InputActionAsset inputActions;
    public float rotationSpeed = 30f;
    [SerializeField] private bool rotationEnabled;

    private static readonly Vector2?[] CheckpointRotationOverrides =
    {
        null,
        new Vector2(55.6f, 0f),
        null,
        null,
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

        //sunHeight = Mathf.Clamp(sunHeight + heightChange * rotationSpeed * Time.deltaTime, 0f, 90f);
        sunDirection += directionChange * rotationSpeed * Time.deltaTime;
        transform.rotation = Quaternion.Euler(sunHeight, sunDirection, 0f);
    }

    public void SetRotationEnabled(bool enabled)
    {
        rotationEnabled = enabled;
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