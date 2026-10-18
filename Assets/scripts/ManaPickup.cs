using UnityEngine;

public class ManaPickup : MonoBehaviour
{
    [Header("Mana Pickup Settings")]
    [SerializeField] private AudioSource manaPickupSFX;
    [SerializeField] private float manaReturnAmountMin = 25f;
    [SerializeField] private float manaReturnAmountMax = 50f;
    [SerializeField] private GameObject pickupSphere;
    [SerializeField] private float spinDegreesPerSecond = 90f;
    [SerializeField] private float bobAmplitude = 0.25f;
    [SerializeField] private float bobFrequency = 1f;
    private Vector3 startPosition;
    private float bobOffset;
    private bool collected;
    [Header("Sun Controller")]
    [SerializeField] private SunController sunController;
    [Header("Player")]
    [SerializeField] private GameObject player;

    private void Awake()
    {
        startPosition = transform.localPosition;
        bobOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Start()
    {
        if (sunController == null)
        {
            sunController = FindAnyObjectByType<SunController>();
        }
        if (player == null)
        {
            player = GameObject.Find("Player");
        }
    }

    private void Update()
    {
        if (collected)
        {
            return;
        }

        // Spin
        transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.Self);

        // Bobbing up/down (local space)
        float bob = Mathf.Sin((Time.time + bobOffset) * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        transform.localPosition = startPosition + new Vector3(0f, bob, 0f);

        CheckForPlayer();
    }

    // The player has no collider/tag, so just compare distances
    private void CheckForPlayer()
    {
        if (collected || player == null)
        {
            return;
        }

        // sqrMagnitude avoids the square root; compare against radius squared
        float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;
        float pickupRadius = pickupSphere.transform.localScale.x * 0.5f;
        if (sqrDistance <= pickupRadius * pickupRadius)
        {
            Collect();
        }
    }

    private void Collect()
    {
        collected = true;
        if (pickupSphere != null)
        {
            pickupSphere.SetActive(false);
        }

        if (manaPickupSFX != null)
        {
            manaPickupSFX.Play();
        }

        float amount = Random.Range(manaReturnAmountMin, manaReturnAmountMax);
        if (sunController != null)
        {
            sunController.RechargeMana(amount);
        }
        else
        {
            Debug.LogWarning("Sun Controller not assigned to ManaPickup Prefab!");
        }
    }

    public void ResetPickup()
    {
        if (manaPickupSFX != null)
        {
            manaPickupSFX.Stop();
        }

        collected = false;
        transform.localPosition = startPosition;
        if (pickupSphere != null)
        {
            pickupSphere.SetActive(true);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (pickupSphere == null)
        {
            return;
        }

        float pickupRadius = pickupSphere.transform.localScale.x * 0.5f;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}