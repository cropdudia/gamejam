using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Light))]
public class SunController : MonoBehaviour
{
    public InputActionAsset inputActions;
    public float rotationSpeed = 30f;

    private float sunHeight;
    private float sunDirection;
    private InputAction sunUpAction;
    private InputAction sunDownAction;
    private InputAction sunLeftAction;
    private InputAction sunRightAction;

    void Awake()
    {
        sunHeight = transform.eulerAngles.x;
        sunDirection = transform.eulerAngles.y;
        sunUpAction = inputActions.FindAction("Player/Sun Up");
        sunDownAction = inputActions.FindAction("Player/Sun Down");
        sunLeftAction = inputActions.FindAction("Player/Sun Left");
        sunRightAction = inputActions.FindAction("Player/Sun Right");
    }

    void Update()
    {
        if (sunUpAction == null || sunDownAction == null || sunLeftAction == null || sunRightAction == null)
            return;

        float heightChange = 0f;
        if (sunDownAction.IsPressed()) heightChange -= 1f;
        if (sunUpAction.IsPressed()) heightChange += 1f;

        float directionChange = 0f;
        if (sunLeftAction.IsPressed()) directionChange -= 1f;
        if (sunRightAction.IsPressed()) directionChange += 1f;

        sunHeight = Mathf.Clamp(sunHeight + heightChange * rotationSpeed * Time.deltaTime, 0f, 90f);
        sunDirection += directionChange * rotationSpeed * Time.deltaTime;
        transform.rotation = Quaternion.Euler(sunHeight, sunDirection, 0f);
    }
}