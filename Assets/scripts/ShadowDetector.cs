using UnityEngine;

public class ShadowDetector : MonoBehaviour
{
    public Light sunLight;
    public LayerMask shadowLayers = ~0;

    private firstpersoncontroller playerController;
    private bool isInShadow;
    private bool gameOver;

    void Awake()
    {
        playerController = GetComponent<firstpersoncontroller>();
    }

    void Update()
    {
        if (gameOver || sunLight == null || playerController == null || !playerController.IsGrounded)
            return;

        Vector3 directionToSun = -sunLight.transform.forward;
        bool nowInShadow = Physics.Raycast(transform.position, 
        directionToSun, Mathf.Infinity, shadowLayers, 
        QueryTriggerInteraction.Ignore);

        if (!isInShadow && nowInShadow)
        {
            isInShadow = true;
            Debug.Log("Player entered shadow.", this);
        }
        else if (isInShadow && !nowInShadow)
        {
            isInShadow = false;
            gameOver = true;
            Time.timeScale = 0f;
            Debug.Log("Game over: player left the shadow.", this);
        }
    }

    void OnGUI()
    {
        if (!gameOver)
            return;

        GUI.Box(new Rect(Screen.width / 2 - 180, Screen.height / 2 - 50, 360, 100), 
        "GAME OVER\nYou left the shadow.");
    }
}