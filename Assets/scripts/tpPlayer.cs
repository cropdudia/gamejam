using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private SunController sunController;
    [SerializeField] private Transform respawnCheckpoint;
    [SerializeField] private AudioSource sizzling;
    [SerializeField] private AudioSource jumpClip;
    [SerializeField] private AudioSource walkClip;
    [SerializeField, Min(0f)] private float respawnDelay = 1f;
    [SerializeField] private Texture2D[] checkpointTitleCards;
    [SerializeField, Min(0f)] private float checkpointCardDisplayDuration = 2f;
    [SerializeField, Min(0f)] private float checkpointCardFadeOutDuration = 1f;
    private const string RespawnCheckpointName = "CHECKPOINT 1";
    private const string CheckpointNamePrefix = "CHECKPOINT ";

    public Light sunLight;
    public LayerMask shadowLayers = ~0;
    public LayerMask fireLayers;
    [SerializeField] private LayerMask instantDeathLayers;
    [SerializeField] private LayerMask checkpointLayers;
    public float playerHealth = 100f;
    public float maxHealth = 100f;
    public float sunlightDamagePerSecond = 10f;
    public float shadowRegenerationPerSecond = 5f;


    public float playerWalkSpeed = 5.0f;
    [SerializeField, Min(0f)] private float groundAcceleration = 25f;
    [SerializeField, Min(0f)] private float groundDeceleration = 35f;
    [SerializeField, Range(0f, 1f)] private float airControl = 0.2f;
    public float gravity = -9.81f;
    [Tooltip("Initial upward speed when a jump starts.")]
    public float jumpHeight = 5.0f;
    [SerializeField, Min(0f)] private float maxJumpHeight = 5f;

    [SerializeField] private float walkBobHeight = 0.05f;
    [SerializeField] private float walkBobSpeed = 10f;
    [SerializeField] private float airTiltAngle = 20f;
    [SerializeField] private float animationSmoothing = 10f;
    [SerializeField] private float lowHealthZoom = 12f;
    [SerializeField] private float lowHealthShake = 0.15f;
    [SerializeField] private float cameraEffectSmoothing = 3f;
    [SerializeField] private float cameraShakeFrequency = 20f;

    [Header("Damage Visual")]
    [SerializeField] private Color damageTint = new Color(1f, 0.2f, 0f);
    [SerializeField, Range(0f, 100f)] private float fullDamageTintHealthPercent = 30f;

    [Header("Low Health Vignette")]
    [SerializeField, Range(0f, 100f)] private float vignetteStartPercent = 45f;
    [SerializeField, Range(0f, 1f)] private float vignetteMinBrightness = 50f / 255f;
    [SerializeField] private AudioSource heartbeatSource;
    [SerializeField, Range(0f, 1f)] private float heartbeatMaxVolume = 1f;

    [Header("Character Effects")]
    [SerializeField] private Color sunAbilityEffectColor = new Color(0.2f, 1f, 0.25f, 1f);
    [SerializeField, Min(0f)] private float sunAbilityLightRange = 5f;
    [SerializeField, Min(0f)] private float sunAbilityLightIntensity = 2f;
    [SerializeField, Min(0f)] private float smokeParticlesPerDamage = 0.5f;

    [Header("Moving Platforms")]
    [SerializeField] private LayerMask platformLayers = ~0;
    [SerializeField] private float platformCheckDistance = 50.3f; // how far below the feet to look
    [SerializeField] private bool rotateCameraWithPlatform = true;
    private const string PlatformTag = "Platform";

    private InputAction playerMoveAction;
    private InputAction playerJumpAction;
    private InputActionMap playerActionMap;

    private Vector2 playerMoveAmount;
    private float playerRotateDampening = 0.1f;
    private float turnSmoothingVelocity;
    private float verticalVelocity;
    private Vector3 horizontalVelocity;
    private float jumpStartHeight;
    private bool jumpCanContinueRising;
    private Vector3 visualStartPosition;
    private Quaternion visualStartRotation;
    private CinemachineCamera cinemachineCamera;
    private CinemachineOrbitalFollow orbitalFollow;
    private float startingFieldOfView;
    private Vector3 startingTargetOffset;
    private Color healthOverlayBaseColor;
    private readonly List<VisualMaterialState> visualMaterialStates = new List<VisualMaterialState>();
    private ParticleSystem sunAbilityParticles;
    private ParticleSystem damageSmokeParticles;
    private Light sunAbilityLight;
    private Material sunAbilityParticleMaterial;
    private Material damageSmokeParticleMaterial;
    private float lastObservedHealth;
    private float pendingSmokeParticles;
    private bool sunAbilityEffectActive;

    private bool isRespawning;
    private bool checkpointCardActive;
    private readonly bool[] activatedEnemyCheckpoints = new bool[5];
    private CanvasGroup checkpointCardCanvasGroup;
    private RawImage checkpointCardImage;

    private sealed class VisualMaterialState
    {
        public Renderer renderer;
        public int materialIndex;
        public int colorPropertyId;
        public int texturePropertyId;
        public Color baseColor;
        public MaterialPropertyBlock originalPropertyBlock;
        public MaterialPropertyBlock workingPropertyBlock;
    }

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
        playerActionMap = InputActions.FindActionMap("Player");
        playerActionMap.Enable();
    }

    private void OnDisable()
    {
        if (InputActions != null)
        {
            InputActions.FindActionMap("Player").Disable();
        }

        RestoreVisualMaterials();

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

        SetSunAbilityEffect(false);
        SetWalkSound(false);
    }

    private void Awake()
    {
        if (respawnCheckpoint == null)
        {
            GameObject checkpointObject = GameObject.Find(RespawnCheckpointName);
            if (checkpointObject != null)
            {
                respawnCheckpoint = checkpointObject.transform;
            }
        }

        CreateCheckpointCardOverlay();
        lastObservedHealth = playerHealth;

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
            CacheVisualMaterials();
        }

        CreateCharacterEffects();

        if (sunController == null)
        {
            sunController = FindAnyObjectByType<SunController>();
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

        if (walkClip != null)
        {
            walkClip.loop = true;
        }

        if (InputActions == null)
        {
            return;
        }

        playerActionMap = InputActions.FindActionMap("Player");
        playerMoveAction = InputActions.FindAction("Player/Move");
        playerJumpAction = InputActions.FindAction("Player/Jump");
    }

    private void Start()
    {
        int startingCheckpointIndex = TryGetCheckpointIndex(respawnCheckpoint, out int checkpointIndex)
            ? checkpointIndex
            : 0;
        InitializeCheckpointEnemies(startingCheckpointIndex);
        StartCoroutine(ShowCheckpointCard(0));
    }

    private void Update()
    {
        playerHealth = Mathf.Clamp(playerHealth, 0f, Mathf.Max(0f, maxHealth));
        UpdateDamageSmoke();
        UpdateCameraEffects();
        UpdateHealthOverlay();
        UpdateDamageVisual();
        UpdateSunAbilityEffect();

        if (isRespawning ||
            checkpointCardActive ||
            playerMoveAction == null ||
            playerJumpAction == null ||
            playerCharacterController == null)
        {
            SetWalkSound(false);
            return;
        }

        playerMoveAmount = playerMoveAction.ReadValue<Vector2>();
        playerMoveAmount = Vector2.ClampMagnitude(playerMoveAmount, 1f);

        UpdatePlatformCarry();
        Jump();
        PlayerMoveAndRotate();
        CheckForShadow();
        AnimateVisual();
        UpdateWalkSound();
    }

    private void UpdateWalkSound()
    {
        bool walkingOnGround = playerCharacterController.isGrounded &&
                               playerMoveAmount.sqrMagnitude > 0.01f &&
                               horizontalVelocity.sqrMagnitude > 0.25f;

        SetWalkSound(walkingOnGround);
    }

    private void SetWalkSound(bool shouldPlay)
    {
        if (walkClip == null)
        {
            return;
        }

        if (shouldPlay && !walkClip.isPlaying)
        {
            walkClip.Play();
        }
        else if (!shouldPlay && walkClip.isPlaying)
        {
            walkClip.Stop();
        }
    }

    private void CreateCharacterEffects()
    {
        sunAbilityParticles = CreateParticleSystem(
            "Sun Ability Green Particles",
            sunAbilityEffectColor,
            0.05f,
            0.12f,
            0.6f,
            1.2f,
            0.15f,
            0.6f,
            0.45f,
            out sunAbilityParticleMaterial);
        ParticleSystem.EmissionModule sunEmission = sunAbilityParticles.emission;
        sunEmission.rateOverTime = 0f;

        GameObject lightObject = new GameObject("Sun Ability Green Light");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 1f, 0f);
        sunAbilityLight = lightObject.AddComponent<Light>();
        sunAbilityLight.type = LightType.Point;
        sunAbilityLight.color = sunAbilityEffectColor;
        sunAbilityLight.range = sunAbilityLightRange;
        sunAbilityLight.intensity = sunAbilityLightIntensity;
        sunAbilityLight.shadows = LightShadows.None;
        sunAbilityLight.enabled = false;

        damageSmokeParticles = CreateParticleSystem(
            "Damage Smoke",
            new Color(0.55f, 0.55f, 0.55f, 0.7f),
            0.3f,
            0.65f,
            0.8f,
            1.5f,
            0.2f,
            0.5f,
            0.25f,
            out damageSmokeParticleMaterial);
        ParticleSystem.EmissionModule smokeEmission = damageSmokeParticles.emission;
        smokeEmission.rateOverTime = 0f;
        damageSmokeParticles.Play();
    }

    private ParticleSystem CreateParticleSystem(
        string effectName,
        Color color,
        float minSize,
        float maxSize,
        float minLifetime,
        float maxLifetime,
        float minSpeed,
        float maxSpeed,
        float radius,
        out Material effectMaterial)
    {
        GameObject effectObject = new GameObject(effectName);
        effectObject.transform.SetParent(transform, false);
        effectObject.transform.localPosition = new Vector3(0f, 1f, 0f);

        ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = Color.white;
        main.maxParticles = 200;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);

        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(color.a, 0f),
                new GradientAlphaKey(color.a, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeGradient);

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null)
        {
            particleShader = Shader.Find("Particles/Standard Unlit");
        }

        effectMaterial = particleShader != null ? new Material(particleShader) : null;
        if (effectMaterial != null)
        {
            effectMaterial.name = effectName + " Material";
            particleRenderer.sharedMaterial = effectMaterial;
        }
        else
        {
            Debug.LogWarning(
                $"No supported particle shader was found for '{effectName}'; using Unity's default particle material.",
                this);
        }

        return particles;
    }

    private void UpdateSunAbilityEffect()
    {
        SetSunAbilityEffect(sunController != null && sunController.IsChangingDirection);
    }

    private void SetSunAbilityEffect(bool active)
    {
        sunAbilityEffectActive = active;
        if (sunAbilityParticles != null)
        {
            if (active)
            {
                ParticleSystem.EmissionModule emission = sunAbilityParticles.emission;
                emission.rateOverTime = 14f;
                sunAbilityParticles.Play();
            }
            else
            {
                ParticleSystem.EmissionModule emission = sunAbilityParticles.emission;
                emission.rateOverTime = 0f;
                sunAbilityParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (sunAbilityLight != null)
        {
            sunAbilityLight.enabled = active;
        }
    }

    private void UpdateDamageSmoke()
    {
        float healthLost = lastObservedHealth - playerHealth;
        lastObservedHealth = playerHealth;
        if (healthLost <= 0f || damageSmokeParticles == null || smokeParticlesPerDamage <= 0f)
        {
            if (sizzling.isPlaying)
            {
                sizzling.Stop();
            }
            return;
        }
        if (!sizzling.isPlaying)
        {
            sizzling.Play();
        }
        pendingSmokeParticles += healthLost * smokeParticlesPerDamage;
        int particleCount = Mathf.FloorToInt(pendingSmokeParticles);
        if (particleCount > 0)
        {
            damageSmokeParticles.Emit(particleCount);
            pendingSmokeParticles -= particleCount;
        }
    }

    private void OnDestroy()
    {
        if (sunAbilityParticleMaterial != null)
        {
            Destroy(sunAbilityParticleMaterial);
        }

        if (damageSmokeParticleMaterial != null)
        {
            Destroy(damageSmokeParticleMaterial);
        }
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

        if (!isInShadow && playerCharacterController.isGrounded)
        {
            playerHealth = Mathf.Max(
                0f,
                playerHealth - sunlightDamagePerSecond * Time.deltaTime);

            if (playerHealth <= 0f)
            {
                Kill();
            }
        }
        else
        {
            playerHealth = Mathf.Min(
                maxHealth,
                playerHealth + shadowRegenerationPerSecond * Time.deltaTime);
        }
    }

    public void Kill()
    {
        if (isRespawning)
        {
            return;
        }

        playerHealth = 0f;
        isRespawning = true;
        SetWalkSound(false);

        if (heartbeatSource != null)
        {
            heartbeatSource.Pause();
        }

        if (respawnCheckpoint == null)
        {
            GameObject checkpointObject = GameObject.Find(RespawnCheckpointName);
            if (checkpointObject != null)
            {
                respawnCheckpoint = checkpointObject.transform;
            }
        }

        if (respawnCheckpoint == null)
        {
            Debug.LogError(
                $"Cannot respawn: no active GameObject named '{RespawnCheckpointName}' was found, and no checkpoint is assigned.",
                this);
            return;
        }

        StartCoroutine(Respawn());
    }

    private IEnumerator Respawn()
    {
        yield return new WaitForSecondsRealtime(respawnDelay);

        if (respawnCheckpoint == null)
        {
            Debug.LogError("Cannot respawn: the checkpoint was destroyed before respawn completed.", this);
            yield break;
        }

        bool controllerWasEnabled = playerCharacterController != null &&
                                    playerCharacterController.enabled;
        if (controllerWasEnabled)
        {
            playerCharacterController.enabled = false;
        }
        transform.SetPositionAndRotation(
            respawnCheckpoint.position,
            respawnCheckpoint.rotation);

        if (TryGetCheckpointIndex(respawnCheckpoint, out int checkpointIndex))
        {
            if (sunController == null)
            {
                sunController = FindAnyObjectByType<SunController>();
            }

            if (sunController != null)
            {
                sunController.SetCheckpoint(checkpointIndex);
                sunController.RechargeMana(1e9f);
            }
            else
            {
                Debug.LogWarning("Cannot reset sun rotation on respawn: no SunController exists in the scene.", this);
            }
        }
        else
        {
            Debug.LogWarning(
                $"Cannot reset sun rotation on respawn: checkpoint '{respawnCheckpoint.name}' is not named CHECKPOINT 1 through CHECKPOINT 5.",
                this);
        }

        ResetManaPickups();

        if (controllerWasEnabled)
        {
            playerCharacterController.enabled = true;
        }

        playerHealth = maxHealth;
        verticalVelocity = 0f;
        horizontalVelocity = Vector3.zero;
        jumpCanContinueRising = false;
        currentPlatform = null;
        platformCarryOffset = Vector3.zero;
        isRespawning = false;

        if (heartbeatSource != null)
        {
            heartbeatSource.UnPause();
        }
    }

    private void CreateCheckpointCardOverlay()
    {
        GameObject canvasObject = new GameObject("Checkpoint Title Card Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new GameObject("Checkpoint Title Card");
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform imageTransform = imageObject.AddComponent<RectTransform>();
        imageTransform.anchorMin = Vector2.zero;
        imageTransform.anchorMax = Vector2.one;
        imageTransform.offsetMin = Vector2.zero;
        imageTransform.offsetMax = Vector2.zero;

        checkpointCardImage = imageObject.AddComponent<RawImage>();
        checkpointCardImage.raycastTarget = false;
        checkpointCardCanvasGroup = imageObject.AddComponent<CanvasGroup>();
        checkpointCardCanvasGroup.alpha = 0f;
        checkpointCardCanvasGroup.blocksRaycasts = false;
        checkpointCardCanvasGroup.interactable = false;
    }

    private IEnumerator ShowCheckpointCard(int checkpointIndex)
    {
        if (checkpointTitleCards == null ||
            checkpointIndex < 0 ||
            checkpointIndex >= checkpointTitleCards.Length ||
            checkpointTitleCards[checkpointIndex] == null)
        {
            Debug.LogWarning(
                $"No title card texture is assigned for checkpoint {checkpointIndex + 1}.",
                this);
            ShowCheckpointTutorial(checkpointIndex);
            yield break;
        }

        checkpointCardActive = true;
        if (playerActionMap != null)
        {
            playerActionMap.Disable();
        }

        checkpointCardImage.texture = checkpointTitleCards[checkpointIndex];
        checkpointCardCanvasGroup.alpha = 1f;

        if (checkpointCardDisplayDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(checkpointCardDisplayDuration);
        }

        float elapsed = 0f;
        while (elapsed < checkpointCardFadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            checkpointCardCanvasGroup.alpha = 1f -
                Mathf.Clamp01(elapsed / checkpointCardFadeOutDuration);
            yield return null;
        }

        checkpointCardCanvasGroup.alpha = 0f;
        checkpointCardImage.texture = null;
        checkpointCardActive = false;
        if (playerActionMap != null)
        {
            playerActionMap.Enable();
        }

        ShowCheckpointTutorial(checkpointIndex);
    }

    private static void ShowCheckpointTutorial(int checkpointIndex)
    {
        if (checkpointIndex == 0)
        {
            TutorialPopup.ShowMessage("Left stick = Move \nRight Stick = Camera\nX = Jump");
        }
        else if (checkpointIndex == 1)
        {
            TutorialPopup.ShowMessage("R1 L1 = Change Time \nChronomansy.");
        }
    }

    private static bool TryGetCheckpointIndex(Transform checkpoint, out int checkpointIndex)
    {
        checkpointIndex = -1;
        if (checkpoint == null ||
            !checkpoint.name.StartsWith(CheckpointNamePrefix, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string number = checkpoint.name.Substring(CheckpointNamePrefix.Length);
        if (!int.TryParse(number, out int checkpointNumber) ||
            checkpointNumber < 1 ||
            checkpointNumber > 5)
        {
            return false;
        }

        checkpointIndex = checkpointNumber - 1;
        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isRespawning || checkpointCardActive)
        {
            return;
        }

        if ((instantDeathLayers.value & (1 << other.gameObject.layer)) != 0)
        {
            Kill();
            return;
        }

        if ((checkpointLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        Transform newCheckpoint = other.transform;
        bool foundNamedCheckpoint = false;
        for (Transform candidate = other.transform; candidate != null; candidate = candidate.parent)
        {
            if (TryGetCheckpointIndex(candidate, out _))
            {
                newCheckpoint = candidate;
                foundNamedCheckpoint = true;
                break;
            }
        }

        if (!foundNamedCheckpoint)
        {
            while (newCheckpoint.parent != null &&
                   (checkpointLayers.value & (1 << newCheckpoint.parent.gameObject.layer)) != 0)
            {
                newCheckpoint = newCheckpoint.parent;
            }
        }

        if (newCheckpoint == respawnCheckpoint)
        {
            return;
        }

        if (!TryGetCheckpointIndex(newCheckpoint, out int checkpointIndex))
        {
            Debug.LogWarning(
                $"Checkpoint '{newCheckpoint.name}' must be named CHECKPOINT 1 through CHECKPOINT 5 to show its title card.",
                this);
            return;
        }

        Transform previousCheckpoint = respawnCheckpoint;
        respawnCheckpoint = newCheckpoint;
        ResetManaPickups();
        ActivateEnemiesForCheckpoint(checkpointIndex);

        if (sunController == null)
        {
            sunController = FindAnyObjectByType<SunController>();
        }

        if (sunController != null)
        {
            sunController.SetCheckpoint(checkpointIndex);
        }
        else
        {
            Debug.LogWarning("Cannot set checkpoint sun rotation: no SunController exists in the scene.", this);
        }

        if (checkpointIndex == 1)
        {
            if (sunController != null)
            {
                sunController.SetRotationEnabled(true);
            }
            else
            {
                Debug.LogWarning("Cannot enable sun rotation at checkpoint 2: no SunController exists in the scene.", this);
            }
        }

        if (previousCheckpoint != null)
        {
            previousCheckpoint.gameObject.SetActive(false);
        }

        Debug.Log($"Checkpoint updated: {respawnCheckpoint.name}.", this);
        StartCoroutine(ShowCheckpointCard(checkpointIndex));
    }

    private static void ResetManaPickups()
    {
        ManaPickup[] pickups = FindObjectsByType<ManaPickup>(
            FindObjectsInactive.Include);
        foreach (ManaPickup pickup in pickups)
        {
            pickup.ResetPickup();
        }
    }

    private void InitializeCheckpointEnemies(int checkpointIndex)
    {
        tpAI[] enemies = FindObjectsByType<tpAI>(
            FindObjectsInactive.Include);
        foreach (tpAI enemy in enemies)
        {
            enemy.gameObject.SetActive(enemy.ActivationCheckpoint == checkpointIndex + 1);
        }

        if (checkpointIndex >= 0 && checkpointIndex < activatedEnemyCheckpoints.Length)
        {
            activatedEnemyCheckpoints[checkpointIndex] = true;
        }
    }

    private void ActivateEnemiesForCheckpoint(int checkpointIndex)
    {
        if (checkpointIndex < 0 ||
            checkpointIndex >= activatedEnemyCheckpoints.Length ||
            activatedEnemyCheckpoints[checkpointIndex])
        {
            return;
        }

        activatedEnemyCheckpoints[checkpointIndex] = true;
        tpAI[] enemies = FindObjectsByType<tpAI>(
            FindObjectsInactive.Include);
        foreach (tpAI enemy in enemies)
        {
            if (enemy.ActivationCheckpoint == checkpointIndex + 1)
            {
                enemy.gameObject.SetActive(true);
            }
        }
    }

    private void Jump()
    {
        if (playerCharacterController.isGrounded)
        {
            verticalVelocity = -1f;
            jumpCanContinueRising = false;

            if (playerJumpAction.WasPressedThisFrame())
            {
                jumpClip.Play();
                jumpStartHeight = transform.position.y;
                verticalVelocity = Mathf.Max(0f, jumpHeight);
                jumpCanContinueRising = maxJumpHeight > 0f && verticalVelocity > 0f;

                if (jumpCanContinueRising &&
                    verticalVelocity * Time.deltaTime >= maxJumpHeight)
                {
                    verticalVelocity = maxJumpHeight / Time.deltaTime;
                    jumpCanContinueRising = false;
                }
            }

            return;
        }

        if (!jumpCanContinueRising || !playerJumpAction.IsPressed())
        {
            jumpCanContinueRising = false;
            verticalVelocity += gravity * Time.deltaTime;
            return;
        }

        float heightGained = transform.position.y - jumpStartHeight;
        float remainingJumpHeight = maxJumpHeight - heightGained;
        if (remainingJumpHeight <= 0f || verticalVelocity <= 0f)
        {
            jumpCanContinueRising = false;
            verticalVelocity = 0f;
            return;
        }

        float frameRise = verticalVelocity * Time.deltaTime;
        if (frameRise >= remainingJumpHeight)
        {
            verticalVelocity = remainingJumpHeight / Time.deltaTime;
            jumpCanContinueRising = false;
        }
    }

    private void PlayerMoveAndRotate()
    {
        Vector3 playerDirection = new Vector3(
            playerMoveAmount.x,
            0f,
            playerMoveAmount.y);

        float moveAmount = playerDirection.magnitude;
        Vector3 desiredHorizontalVelocity = Vector3.zero;
        if (moveAmount >= 0.1f)
        {
            playerDirection.Normalize();

            float targetAngle = Mathf.Atan2(playerDirection.x, playerDirection.z) *
                                Mathf.Rad2Deg +
                                playerCamera.eulerAngles.y;
            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            desiredHorizontalVelocity = moveDirection * playerWalkSpeed * moveAmount;

            float smoothTargetAngle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref turnSmoothingVelocity,
                playerRotateDampening);
            transform.rotation = Quaternion.Euler(0f, smoothTargetAngle, 0f);
        }

        if (playerCharacterController.isGrounded)
        {
            float groundAccelerationRate = moveAmount >= 0.1f
                ? groundAcceleration
                : groundDeceleration;
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                desiredHorizontalVelocity,
                groundAccelerationRate * Time.deltaTime);
        }
        else if (moveAmount >= 0.1f)
        {
            float airAcceleration = playerWalkSpeed * airControl * Time.deltaTime;
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                desiredHorizontalVelocity,
                airAcceleration);
        }

        // Vertical velocity plus whatever the platform under us moved this frame
        Vector3 frameMove = horizontalVelocity * Time.deltaTime +
                            new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime +
                            platformCarryOffset;
        playerCharacterController.Move(frameMove);
    }

    private void CacheVisualMaterials()
    {
        visualMaterialStates.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer visualRenderer in renderers)
        {
            Material[] materials = visualRenderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                int colorPropertyId = material.HasProperty("_BaseColor")
                    ? Shader.PropertyToID("_BaseColor")
                    : material.HasProperty("_Color")
                        ? Shader.PropertyToID("_Color")
                        : 0;
                if (colorPropertyId == 0)
                {
                    continue;
                }

                int texturePropertyId = material.HasProperty("_BaseMap")
                    ? Shader.PropertyToID("_BaseMap")
                    : material.HasProperty("_MainTex")
                        ? Shader.PropertyToID("_MainTex")
                        : 0;
                MaterialPropertyBlock originalPropertyBlock = new MaterialPropertyBlock();
                visualRenderer.GetPropertyBlock(originalPropertyBlock, materialIndex);

                visualMaterialStates.Add(new VisualMaterialState
                {
                    renderer = visualRenderer,
                    materialIndex = materialIndex,
                    colorPropertyId = colorPropertyId,
                    texturePropertyId = texturePropertyId,
                    baseColor = originalPropertyBlock.HasColor(colorPropertyId)
                        ? originalPropertyBlock.GetColor(colorPropertyId)
                        : material.GetColor(colorPropertyId),
                    originalPropertyBlock = originalPropertyBlock,
                    workingPropertyBlock = new MaterialPropertyBlock()
                });
            }
        }
    }

    private void UpdateDamageVisual()
    {
        float healthPercent = Mathf.Clamp01(playerHealth / Mathf.Max(0.01f, maxHealth));
        float fullTintHealth = Mathf.Clamp01(fullDamageTintHealthPercent / 100f);
        float tintAmount = 1f - Mathf.InverseLerp(fullTintHealth, 1f, healthPercent);
        tintAmount = Mathf.SmoothStep(0f, 1f, tintAmount);
        bool fullyTinted = tintAmount >= 1f;

        foreach (VisualMaterialState materialState in visualMaterialStates)
        {
            Color tintedColor = Color.Lerp(materialState.baseColor, damageTint, tintAmount);
            tintedColor.a = materialState.baseColor.a;

            materialState.renderer.SetPropertyBlock(
                materialState.originalPropertyBlock,
                materialState.materialIndex);
            materialState.renderer.GetPropertyBlock(
                materialState.workingPropertyBlock,
                materialState.materialIndex);
            materialState.workingPropertyBlock.SetColor(materialState.colorPropertyId, tintedColor);
            if (fullyTinted && materialState.texturePropertyId != 0)
            {
                materialState.workingPropertyBlock.SetTexture(
                    materialState.texturePropertyId,
                    Texture2D.whiteTexture);
            }
            materialState.renderer.SetPropertyBlock(
                materialState.workingPropertyBlock,
                materialState.materialIndex);
        }
    }

    private void RestoreVisualMaterials()
    {
        foreach (VisualMaterialState materialState in visualMaterialStates)
        {
            if (materialState.renderer != null)
            {
                materialState.renderer.SetPropertyBlock(
                    materialState.originalPropertyBlock,
                    materialState.materialIndex);
            }
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