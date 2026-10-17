using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(100)] // run after platform movers (default order 0)
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NavMeshAgent))]
public class tpAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;      // leave empty to auto-find the tpPlayer
    [SerializeField] private Light sunLight;        // leave empty to fall back to RenderSettings.sun
    [SerializeField] private LayerMask shadowLayers = ~0;
    [SerializeField] private LayerMask fireLayers;

    [Header("Health (inverse of the player)")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private float shadowDamagePerSecond = 10f;
    [SerializeField] private float lightRegenerationPerSecond = 5f;
    [SerializeField] private GameObject deathEffectPrefab;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float turnSmoothTime = 0.1f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Patrol")]
    [SerializeField] private float patrolRadius = 10f;
    [SerializeField] private float minWaitTime = 1f;
    [SerializeField] private float maxWaitTime = 4f;

    [Header("Sight (radius only, no cone)")]
    [SerializeField] private float minSightRadius = 8f;
    [SerializeField] private float maxSightRadius = 16f;

    [Header("Attack")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;               // optional, falls back to body center
    [SerializeField] private float reactionDelay = 0.6f;        // spotted -> first shot
    [SerializeField] private int projectilesPerBurst = 3;
    [SerializeField] private float delayBetweenProjectiles = 0.25f;
    [SerializeField] private float burstCooldown = 3f;

    [Header("Moving Platforms")]
    [SerializeField] private LayerMask platformLayers = ~0;
    [SerializeField] private float platformCheckDistance = 0.3f;
    private const string PlatformTag = "Platform";

    private CharacterController controller;
    private NavMeshAgent agent;
    private CharacterController targetController;

    private Vector3 patrolOrigin;
    private float sightRadius;
    private float health;
    private bool isDead;

    private float waitTimer;
    private float verticalVelocity;
    private float turnSmoothingVelocity;

    private bool isFiring;
    private float nextFireTime;
    private Vector3 lastKnownTargetPosition;

    private Transform currentPlatform;
    private Vector3 lastPlatformPosition;
    private Quaternion lastPlatformRotation;
    private Vector3 platformCarryOffset;

    private void OnValidate()
    {
        maxSightRadius = Mathf.Max(minSightRadius, maxSightRadius);
        maxWaitTime = Mathf.Max(minWaitTime, maxWaitTime);
        projectilesPerBurst = Mathf.Max(1, projectilesPerBurst);
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();

        // The CharacterController does the actual moving; the agent only does pathfinding
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.speed = walkSpeed;

        patrolOrigin = transform.position;
        sightRadius = Random.Range(minSightRadius, maxSightRadius);
        health = maxHealth;

        if (target == null)
        {
            tpPlayer player = FindFirstObjectByType<tpPlayer>();
            if (player != null)
            {
                target = player.transform;
                targetController = player.playerCharacterController;
            }
        }
        else
        {
            targetController = target.GetComponent<CharacterController>();
        }
    }

    private void Start()
    {
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning("tpAI is not on a baked NavMesh, so it can't patrol.", this);
            return;
        }

        PickPatrolPoint();
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        UpdateLight();

        if (isDead)
        {
            return;
        }

        UpdatePlatformCarry();

        bool seesTarget = CanSeeTarget();

        if (seesTarget)
        {
            Debug.Log("Enemy sees target: " + target.name, this);
            lastKnownTargetPosition = GetTargetAimPoint();

            if (!isFiring && Time.time >= nextFireTime && projectilePrefab != null)
            {
                StartCoroutine(FireBurst());
            }
        }

        // Pathfinding: stand still while winding up / firing, patrol otherwise
        Vector3 desiredVelocity = Vector3.zero;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = isFiring;

            if (!isFiring)
            {
                UpdatePatrol();
                desiredVelocity = agent.desiredVelocity;
                desiredVelocity.y = 0f;
            }
        }

        // Gravity
        if (controller.isGrounded)
        {
            verticalVelocity = -1f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 motion = (desiredVelocity + Vector3.up * verticalVelocity) * Time.deltaTime
                         + platformCarryOffset;
        controller.Move(motion);

        if (agent.isOnNavMesh)
        {
            agent.nextPosition = transform.position;
        }

        // Face the shot while firing, otherwise face where we're walking
        Vector3 faceDirection = isFiring
            ? lastKnownTargetPosition - transform.position
            : desiredVelocity;
        faceDirection.y = 0f;

        if (faceDirection.sqrMagnitude > 0.01f)
        {
            float targetAngle = Mathf.Atan2(faceDirection.x, faceDirection.z) * Mathf.Rad2Deg;
            float smoothAngle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref turnSmoothingVelocity,
                turnSmoothTime);

            transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
        }
    }

    // ---------------------------------------------------------------- light

    // Inverse of the player: shadow hurts, light heals
    private void UpdateLight()
    {
        Light sun = sunLight != null ? sunLight : RenderSettings.sun;
        if (sun == null)
        {
            return;
        }

        Vector3 center = controller.bounds.center;

        bool nearFire = Physics.CheckSphere(
            center,
            0.1f,
            fireLayers,
            QueryTriggerInteraction.Collide);

        bool isInShadow = !nearFire && Physics.Raycast(
            center,
            -sun.transform.forward,
            Mathf.Infinity,
            shadowLayers,
            QueryTriggerInteraction.Ignore);

        if (isInShadow)
        {
            health -= shadowDamagePerSecond * Time.deltaTime;

            if (health <= 0f)
            {
                Die();
            }
        }
        else
        {
            health = Mathf.Min(maxHealth, health + lightRegenerationPerSecond * Time.deltaTime);
        }
    }

    private void Die()
    {
        isDead = true;

        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, controller.bounds.center, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    // --------------------------------------------------------------- patrol

    private void UpdatePatrol()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0f)
            {
                PickPatrolPoint();
            }

            return;
        }

        bool arrived = !agent.pathPending &&
                       agent.remainingDistance <= agent.stoppingDistance + 0.1f;

        if (arrived)
        {
            waitTimer = Random.Range(minWaitTime, maxWaitTime);
        }
    }

    private void PickPatrolPoint()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 circle = Random.insideUnitCircle * patrolRadius;
            Vector3 candidate = patrolOrigin + new Vector3(circle.x, 0f, circle.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                return;
            }
        }
    }

    // ---------------------------------------------------------------- sight

    // Radius check only, and the radius is rolled once per enemy so they aren't all equally alert
    private bool CanSeeTarget()
    {
        if (target == null)
        {
            return false;
        }

        Vector3 offset = target.position - transform.position;
        return offset.sqrMagnitude <= sightRadius * sightRadius;
    }

    private Vector3 GetTargetAimPoint()
    {
        return targetController != null
            ? targetController.bounds.center
            : target.position + Vector3.up;
    }

    // --------------------------------------------------------------- attack

    private IEnumerator FireBurst()
    {
        isFiring = true;

        yield return new WaitForSeconds(reactionDelay);

        for (int i = 0; i < projectilesPerBurst; i++)
        {
            FireProjectile();

            if (i < projectilesPerBurst - 1)
            {
                yield return new WaitForSeconds(delayBetweenProjectiles);
            }
        }

        isFiring = false;
        nextFireTime = Time.time + burstCooldown;
    }

    private void FireProjectile()
    {
        Vector3 origin = firePoint != null ? firePoint.position : controller.bounds.center;
        Vector3 direction = lastKnownTargetPosition - origin;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }

        direction.Normalize();

        // Without a fire point, nudge the spawn out of our own capsule so it doesn't hit us
        if (firePoint == null)
        {
            origin += direction * (controller.radius + 0.3f);
        }

        Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction));
    }

    // ------------------------------------------------------------ platforms

    private Transform FindPlatformBelow()
    {
        Bounds bounds = controller.bounds;
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

        for (Transform t = hit.collider.transform; t != null; t = t.parent)
        {
            if (t.CompareTag(PlatformTag))
            {
                return t;
            }
        }

        return null;
    }

    private void UpdatePlatformCarry()
    {
        platformCarryOffset = Vector3.zero;

        Transform platform = FindPlatformBelow();

        if (platform == null)
        {
            currentPlatform = null;
            return;
        }

        if (platform == currentPlatform)
        {
            Quaternion deltaRotation = platform.rotation * Quaternion.Inverse(lastPlatformRotation);
            Vector3 position = transform.position;

            Vector3 carriedPosition = platform.position +
                                      deltaRotation * (position - lastPlatformPosition);
            platformCarryOffset = carriedPosition - position;

            Vector3 flatForward = deltaRotation * Vector3.forward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude > 0.0001f)
            {
                float yawDelta = Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, yawDelta, 0f) * transform.rotation;
            }
        }

        currentPlatform = platform;
        lastPlatformPosition = platform.position;
        lastPlatformRotation = platform.rotation;
    }

    // --------------------------------------------------------------- gizmos

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? patrolOrigin : transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, patrolRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            Application.isPlaying ? sightRadius : maxSightRadius);
    }
}