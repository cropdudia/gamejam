using UnityEngine;
using UnityEngine.InputSystem;

public class tpPlayer : MonoBehaviour
{
    public InputActionAsset InputActions;
    public CharacterController playerCharacterController;

    [SerializeField] private Transform playerCamera;

    public Light sunLight;
    public LayerMask shadowLayers = ~0;
    public LayerMask fireLayers;

    public float playerWalkSpeed = 5.0f;
    public float gravity = -9.81f;
    public float jumpHeight = 5.0f;

    private InputAction playerMoveAction;
    private InputAction playerJumpAction;

    private Vector2 playerMoveAmount;
    private float playerRotateDampening = 10.0f;
    private float turnSmoothingVelocity;
    private float verticalVelocity;

    private bool hasEnteredShadow;
    private bool gameOver;

    private void OnEnable()
    {
        if (InputActions == null)
        {
            return;
        }

        InputActions.bindingMask = InputBinding.MaskByGroup("Gamepad");
        InputActions.FindActionMap("Player").Enable();
    }

    private void OnDisable()
    {
        if (InputActions == null)
        {
            return;
        }

        InputActions.FindActionMap("Player").Disable();
    }

    private void Awake()
    {
        if (InputActions == null)
        {
            return;
        }

        playerMoveAction = InputActions.FindAction("Player/Move");
        playerJumpAction = InputActions.FindAction("Player/Jump");
    }

    private void Update()
    {
        if (gameOver ||
            playerMoveAction == null ||
            playerJumpAction == null ||
            playerCharacterController == null)
        {
            return;
        }

        playerMoveAmount = playerMoveAction.ReadValue<Vector2>();
        playerMoveAmount = Vector2.ClampMagnitude(playerMoveAmount, 1f);

        Jump();
        PlayerMoveAndRotate();
        CheckForShadow();
    }

    private void CheckForShadow()
    {
        if (sunLight == null || !playerCharacterController.isGrounded)
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

        if (!hasEnteredShadow && isInShadow)
        {
            hasEnteredShadow = true;
            Debug.Log("Player entered shadow.", this);
        }
        else if (hasEnteredShadow && !isInShadow)
        {
            gameOver = true;
            Time.timeScale = 0f;
            Debug.Log("Game over: player left the shadow.", this);
        }
    }

    private void OnGUI()
    {
        if (gameOver)
        {
            GUI.Box(
                new Rect(Screen.width / 2 - 180, Screen.height / 2 - 50, 360, 100),
                "GAME OVER\nYou left the shadow.");
        }
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
        Vector3 verticalMove = new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime;

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
}