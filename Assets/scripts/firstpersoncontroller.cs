using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class firstpersoncontroller : MonoBehaviour
{
    public bool IsGrounded { get; private set; }

    public Transform cameraTransform;

    public float sensitivity = 0.1f;
    public float maxLookAngle = 90f;
    public float moveSpeed = 6f;
    public float acceleration = 30f;
    public float groundFriction = 10f;
    [Range(0f, 1f)] public float airControl = 0.35f;
    public float jumpHeight = 1.5f;
    public float groundCheckDistance = 0.12f;
    public float maxGroundAngle = 50f;
    public LayerMask groundLayers = ~0;
    [Min(0.01f)] public float capsuleHeight = 2f;
    [Min(0.01f)] public float capsuleRadius = 0.5f;
    public Vector3 capsuleCenter;

    private Rigidbody body;
    private CapsuleCollider capsule;
    private float yaw;
    private float pitch;
    private Vector2 moveInput;
    private bool jumpQueued;
    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private static readonly Vector3[] groundSampleDirections =
    {
        Vector3.zero,
        Vector3.right,
        Vector3.left,
        Vector3.forward,
        Vector3.back
    };

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        ApplyCapsuleDimensions();
        body.useGravity = true;
        body.constraints |= RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        yaw = transform.eulerAngles.y;

        if (cameraTransform == null)
        {
            Camera playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera != null)
                cameraTransform = playerCamera.transform;
        }
    }

    void OnValidate()
    {
        capsuleRadius = Mathf.Max(0.01f, capsuleRadius);
        capsuleHeight = Mathf.Max(capsuleHeight, capsuleRadius * 2f);

        CapsuleCollider existingCapsule = GetComponent<CapsuleCollider>();
        if (existingCapsule != null)
            ApplyCapsuleDimensions(existingCapsule);
    }

    private void ApplyCapsuleDimensions()
    {
        ApplyCapsuleDimensions(capsule);
    }

    private void ApplyCapsuleDimensions(CapsuleCollider target)
    {
        capsuleRadius = Mathf.Max(0.01f, capsuleRadius);
        capsuleHeight = Mathf.Max(capsuleHeight, capsuleRadius * 2f);
        target.radius = capsuleRadius;
        target.height = capsuleHeight;
        target.center = capsuleCenter;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            yaw += mouseDelta.x * sensitivity;
            pitch += mouseDelta.y * sensitivity;
        }

        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(-pitch, 0f, 0f);

        moveInput = ReadMoveInput();
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            jumpQueued = true;
    }

    void FixedUpdate()
    {
        body.MoveRotation(Quaternion.Euler(0f, yaw, 0f));

        IsGrounded = CheckGrounded();
        bool grounded = IsGrounded;
        Vector3 velocity = body.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 moveDirection = Quaternion.Euler(0f, yaw, 0f) * new Vector3(moveInput.x, 0f, moveInput.y);
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        if (moveDirection.sqrMagnitude > 0f)
        {
            float control = grounded ? 1f : airControl;
            body.AddForce(moveDirection * acceleration * control, ForceMode.Acceleration);
        }
        else if (grounded)
        {
            body.AddForce(-horizontalVelocity * groundFriction, ForceMode.Acceleration);
        }

        horizontalVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        if (horizontalVelocity.magnitude > moveSpeed)
        {
            Vector3 limitedVelocity = horizontalVelocity.normalized * moveSpeed;
            body.linearVelocity = new Vector3(limitedVelocity.x, body.linearVelocity.y, limitedVelocity.z);
        }

        if (jumpQueued)
        {
            if (grounded)
            {
                velocity = body.linearVelocity;
                velocity.y = Mathf.Sqrt(2f * Physics.gravity.magnitude * jumpHeight);
                body.linearVelocity = velocity;
            }

            jumpQueued = false;
        }
    }

    private Vector2 ReadMoveInput()
    {
        if (Keyboard.current == null)
            return Vector2.zero;

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical -= 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical += 1f;

        return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
    }

    private bool CheckGrounded()
    {
        Bounds bounds = capsule.bounds;
        float rayDistance = bounds.extents.y + groundCheckDistance;
        float rayOffset = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.65f;

        foreach (Vector3 direction in groundSampleDirections)
        {
            Vector3 origin = bounds.center + direction * rayOffset;
            int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, rayDistance, groundLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.rigidbody == body || hit.collider.transform.IsChildOf(transform))
                    continue;

                if (Vector3.Angle(hit.normal, Vector3.up) <= maxGroundAngle && hit.distance <= rayDistance)
                    return true;
            }
        }

        return false;
    }
}