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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        startPosition = transform.localPosition;
        bobOffset = Random.Range(0f, Mathf.PI * 2f);
        if (sunController == null)
        {
            sunController = FindFirstObjectByType<SunController>();
        }
        if (player == null)
        {
            player = GameObject.Find("Player");
        }
    }

    // Update is called once per frame
    void Update()
    {
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
        float duration = 0.0f;
        collected = true;
        if (manaPickupSFX != null)
        {
            manaPickupSFX.Play();
            duration = manaPickupSFX.clip.length;
            pickupSphere.SetActive(false);
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

        Destroy(gameObject, duration);
    }

    private void OnDrawGizmosSelected()
    {
        float pickupRadius = pickupSphere.transform.localScale.x * 0.5f;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}