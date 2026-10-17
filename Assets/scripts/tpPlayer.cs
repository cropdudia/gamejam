using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)] // run after platform movers (default order 0), before Cinemachine
public class tpPlayer : MonoBehaviour
{
    public InputActionAsset InputActions;
    public CharacterController playerCharacterController;

    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform playerVisual;
    [SerializeField] private Image healthOverlayImage;

    public Light sunLight;
    public LayerMask shadowLayers = ~0;
    public LayerMask fireLayers;
    public float playerHealth = 100f;
    public float maxHealth = 100f;
    public float sunlightDamagePerSecond = 10f;
    public float shadowRegenerationPerSecond = 5f;


    public float playerWalkSpeed = 5.0f;
    public float gravity = -9.81f;
    public float jumpHeight = 5.0f;

    [SerializeField] private float walkBobHeight = 0.05f;
    [SerializeField] private float walkBobSpeed = 10f;
    [SerializeField] private float airTiltAngle = 20f;
    [SerializeField] private float animationSmoothing = 10f;
    [SerializeField] private float lowHealthZoom = 12f;
    [SerializeField] private float lowHealthShake = 0.15f;
    [SerializeField] private float cameraEffectSmoothing = 3f;
    [SerializeField] private float cameraShakeFrequency = 20f;

    [Header("Low Health Vignette")]
    [SerializeField, Range(0f, 100f)] private float vignetteStartPercent = 45f;
    [SerializeField, Range(0f, 1f)] private float vignetteMinBrightness = 50f / 255f;
    [SerializeField] private AudioSource heartbeatSource;
    [SerializeField, Range(0f, 1f)] private float heartbeatMaxVolume = 1f;

    [Header("Moving Platforms")]
    [SerializeField] private LayerMask platformLayers = ~0;
    [SerializeField] private float platformCheckDistance = 50.3f; // how far below the feet to look
    [SerializeField] private bool rotateCameraWithPlatform = true;
    private const string PlatformTag = "Platform";

    private InputAction playerMoveAction;
    private InputAction playerJumpAction;

    private Vector2 playerMoveAmount;
    private float playerRotateDampening = 0.1f;
    private float turnSmoothingVelocity;
    private float verticalVelocity;
    private Vector3 visualStartPosition;
    private Quaternion visualStartRotation;
    private CinemachineCamera cinemachineCamera;
    private CinemachineOrbitalFollow orbitalFollow;
    private float startingFieldOfView;
    private Vector3 startingTargetOffset;
    private Color healthOverlayBaseColor;

    private bool gameOver;

    private Transform currentPlatform;
    private Vector3 lastPlatformPosition;
    private Quaternion lastPlatformRotation;
    private Vector3 platformCarryOffset;

    private void OnEnable()
    {
        if (InputActions == null)
        {
            return;
        }

        //InputActions.bindingMask = InputBinding.MaskByGroup("Gamepad");
        InputActions.FindActionMap("Player").Enable();
    }

    private void OnDisable()
    {
        if (InputActions != null)
        {
            InputActions.FindActionMap("Player").Disable();
        }

        if (cinemachineCamera != null)
        {
            var lens = cinemachineCamera.Lens;
            lens.FieldOfView = startingFieldOfView;
            cinemachineCamera.Lens = lens;
        }

        if (orbitalFollow != null)
        {
            orbitalFollow.TargetOffset = startingTargetOffset;
        }

    }

    private void Awake()
    {
        if (playerCamera != null)
        {
            cinemachineCamera = playerCamera.GetComponent<CinemachineCamera>();
            orbitalFollow = playerCamera.GetComponent<CinemachineOrbitalFollow>();

            if (cinemachineCamera != null)
            {
                startingFieldOfView = cinemachineCamera.Lens.FieldOfView;
            }

            if (orbitalFollow != null)
            {
                startingTargetOffset = orbitalFollow.TargetOffset;
            }
        }

        if (cinemachineCamera == null)
        {
            Debug.LogWarning("Health zoom needs a Cinemachine Camera on Player Camera.", this);
        }

        if (orbitalFollow == null)
        {
            Debug.LogWarning("Health shake needs Cinemachine Orbital Follow on Player Camera.", this);
        }

        if (playerVisual == null)
        {
            MeshRenderer visualRenderer = GetComponentInChildren<MeshRenderer>();
            if (visualRenderer != null && visualRenderer.transform != transform)
            {
                playerVisual = visualRenderer.transform;
            }
        }

        if (playerVisual != null)
        {
            visualStartPosition = playerVisual.localPosition;
            visualStartRotation = playerVisual.localRotation;
        }

        if (healthOverlayImage != null)
        {
            healthOverlayBaseColor = healthOverlayImage.color;
        }

        if (heartbeatSource != null)
        {
            heartbeatSource.loop = true;
            heartbeatSource.volume = 0f;
            if (!heartbeatSource.isPlaying)
            {
                heartbeatSource.Play();
            }
        }

        if (InputActions == null)
        {
            return;
        }

        playerMoveAction = InputActions.FindAction("Player/Move");
        playerJumpAction = InputActions.FindAction("Player/Jump");
    }

    private void Update()
    {
        playerHealth = Mathf.Clamp(playerHealth, 0f, Mathf.Max(0f, maxHealth));
        UpdateCameraEffects();
        UpdateHealthOverlay();

        if (gameOver ||
            playerMoveAction == null ||
            playerJumpAction == null ||
            playerCharacterController == null)
        {
            return;
        }

        playerMoveAmount = playerMoveAction.ReadValue<Vector2>();
        playerMoveAmount = Vector2.ClampMagnitude(playerMoveAmount, 1f);

        UpdatePlatformCarry();
        Jump();
        PlayerMoveAndRotate();
        CheckForShadow();
        AnimateVisual();
    }

    private Transform FindPlatformBelow()
    {
        Bounds bounds = playerCharacterController.bounds;
        float distance = bounds.extents.y + platformCheckDistance;

        if (!Physics.Raycast(
                bounds.center,
                Vector3.down,
                out RaycastHit hit,
                distance,
                platformLayers,
                QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        // Walk up the hierarchy so child colliders of a tagged platform count too
        for (Transform t = hit.collider.transform; t != null; t = t.parent)
        {
            if (t.CompareTag(PlatformTag))
            {
                return t;
            }
        }

        return null;
    }

    // Works out how far the platform under the player moved since last frame.
    // Position is added to this frame's Move() (so isGrounded stays correct),
    // yaw is applied to the player and camera directly.
    private void UpdatePlatformCarry()
    {
        platformCarryOffset = Vector3.zero;

        Transform platform = FindPlatformBelow();

        if (platform == null)
        {
            currentPlatform = null;
            return;
        }

        // Only carry when staying on the same platform as last frame.
        // On a new platform we just start tracking it (no jump).
        if (platform == currentPlatform)
        {
            Quaternion deltaRotation = platform.rotation * Quaternion.Inverse(lastPlatformRotation);
            Vector3 playerPosition = transform.position;

            // Same as Roblox: newPlatformCF * inverse(lastPlatformCF) applied to the player
            Vector3 carriedPosition = platform.position +
                                      deltaRotation * (playerPosition - lastPlatformPosition);
            platformCarryOffset = carriedPosition - playerPosition;

            // Only the yaw turns the player/camera, so tilting platforms don't tip the character over
            Vector3 flatForward = deltaRotation * Vector3.forward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude > 0.0001f)
            {
                float yawDelta = Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg;

                transform.rotation = Quaternion.Euler(0f, yawDelta, 0f) * transform.rotation;

                if (rotateCameraWithPlatform && orbitalFollow != null)
                {
                    orbitalFollow.HorizontalAxis.Value =
                        Mathf.DeltaAngle(0f, orbitalFollow.HorizontalAxis.Value + yawDelta);
                }
            }
        }

        currentPlatform = platform;
        lastPlatformPosition = platform.position;
        lastPlatformRotation = platform.rotation;
    }

    // 0 = healthy / above threshold, 1 = dead
    private float GetVignetteIntensity()
    {
        float appearOn = Mathf.Max(0.01f, maxHealth * (vignetteStartPercent / 100f));
        float amplitude = Mathf.Min(playerHealth / appearOn, 1f); // same as the Roblox Amplitude
        return 1f - amplitude;
    }

    private void UpdateHealthOverlay()
    {
        float intensity = GetVignetteIntensity();

        if (healthOverlayImage != null)
        {
            float brightness = Mathf.Max(vignetteMinBrightness, intensity);

            Color c = healthOverlayBaseColor;
            c.r *= brightness;
            c.g *= brightness;
            c.b *= brightness;
            c.a *= intensity;
            healthOverlayImage.color = c;
        }

        if (heartbeatSource != null)
        {
            heartbeatSource.volume = intensity * heartbeatMaxVolume;
        }
    }

    private void UpdateCameraEffects()
    {
        float healthLoss = 1f - Mathf.Clamp01(playerHealth / Mathf.Max(0.01f, maxHealth));
        float blend = 1f - Mathf.Exp(-cameraEffectSmoothing * Time.unscaledDeltaTime);

        if (cinemachineCamera != null)
        {
            var lens = cinemachineCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(
                lens.FieldOfView,
                startingFieldOfView - lowHealthZoom * healthLoss,
                blend);
            cinemachineCamera.Lens = lens;
        }

        if (orbitalFollow != null)
        {
            float shakeTime = Time.unscaledTime * cameraShakeFrequency;
            Vector3 shakeOffset = new Vector3(
                (Mathf.PerlinNoise(shakeTime, 0f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0f, shakeTime) - 0.5f) * 2f,
                0f) * (lowHealthShake * healthLoss);

            orbitalFollow.TargetOffset = Vector3.Lerp(
                orbitalFollow.TargetOffset,
                startingTargetOffset + shakeOffset,
                blend);
        }

    }

    private void CheckForShadow()
    {
        if (sunLight == null)
        {
            return;
        }

        Vector3 directionToSun = -sunLight.transform.forward;
        bool nearFire = Physics.CheckSphere(
            playerCharacterController.bounds.center,
            0.1f,
            fireLayers,
            QueryTriggerInteraction.Collide);

        bool isInShadow = !nearFire && Physics.Raycast(
            playerCharacterController.bounds.center,
            directionToSun,
            Mathf.Infinity,
            shadowLayers,
            QueryTriggerInteraction.Ignore);

        if (!isInShadow)
        {
            playerHealth = Mathf.Max(
                0f,
                playerHealth - sunlightDamagePerSecond * Time.deltaTime);

            if (playerHealth <= 0f)
            {
                gameOver = true;
                Time.timeScale = 0f;
                if (heartbeatSource != null) heartbeatSource.Pause();
                Debug.Log("Game over: player ran out of health in sunlight.", this);
            }
        }
        else
        {
            playerHealth = Mathf.Min(
                maxHealth,
                playerHealth + shadowRegenerationPerSecond * Time.deltaTime);
        }
    }

    private void OnGUI()
    {
        DrawHealthBar();

        if (gameOver)
        {
            GUI.Box(
                new Rect(Screen.width / 2 - 180, Screen.height / 2 - 50, 360, 100),
                "GAME OVER\nYou ran out of health in sunlight.");
        }
    }

    private void DrawHealthBar()
    {
        float safeMaxHealth = Mathf.Max(0.01f, maxHealth);
        float healthRatio = Mathf.Clamp01(playerHealth / safeMaxHealth);
        Rect barRect = new Rect(20f, 20f, 200f, 20f);

        GUI.Box(barRect, GUIContent.none);

        Color previousColor = GUI.color;
        GUI.color = Color.green;
        GUI.DrawTexture(
            new Rect(barRect.x + 2f, barRect.y + 2f, (barRect.width - 4f) * healthRatio, barRect.height - 4f),
            Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUI.Label(
            new Rect(barRect.x, barRect.y + barRect.height + 2f, barRect.width, 20f),
            $"Health: {Mathf.Clamp(playerHealth, 0f, safeMaxHealth):0} / {safeMaxHealth:0}");
    }

    private void Jump()
    {
        if (playerCharacterController.isGrounded)
        {
            verticalVelocity = -1f;

            if (playerJumpAction.WasPressedThisFrame())
            {
                verticalVelocity = jumpHeight;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    private void PlayerMoveAndRotate()
    {
        Vector3 playerDirection = new Vector3(
            playerMoveAmount.x,
            0f,
            playerMoveAmount.y);

        float moveAmount = playerDirection.magnitude;
        // Vertical velocity plus whatever the platform under us moved this frame
        Vector3 verticalMove = new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime + platformCarryOffset;

        if (moveAmount >= 0.1f)
        {
            playerDirection.Normalize();

            float targetAngle = Mathf.Atan2(playerDirection.x, playerDirection.z) *
                                Mathf.Rad2Deg +
                                playerCamera.eulerAngles.y;

            float smoothTargetAngle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref turnSmoothingVelocity,
                playerRotateDampening);

            transform.rotation = Quaternion.Euler(0f, smoothTargetAngle, 0f);

            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) *
                                    Vector3.forward;

            playerCharacterController.Move(
                moveDirection * playerWalkSpeed * moveAmount * Time.deltaTime +
                verticalMove);
        }
        else
        {
            playerCharacterController.Move(verticalMove);
        }
    }

    private void AnimateVisual()
    {
        if (playerVisual == null)
        {
            return;
        }

        bool grounded = playerCharacterController.isGrounded && verticalVelocity <= 0f;
        bool walking = grounded && playerMoveAmount.sqrMagnitude > 0.01f;
        float bob = walking
            ? Mathf.Sin(Time.time * walkBobSpeed) * walkBobHeight
            : 0f;
        float targetTilt = grounded
            ? 0f
            : verticalVelocity > 0f ? -airTiltAngle : airTiltAngle;
        float blend = 1f - Mathf.Exp(-animationSmoothing * Time.deltaTime);

        playerVisual.localPosition = Vector3.Lerp(
            playerVisual.localPosition,
            visualStartPosition + Vector3.up * bob,
            blend);

        Quaternion targetRotation = visualStartRotation * Quaternion.Euler(targetTilt, 0f, 0f);
        playerVisual.localRotation = Quaternion.Slerp(
            playerVisual.localRotation,
            targetRotation,
            blend);
    }
}