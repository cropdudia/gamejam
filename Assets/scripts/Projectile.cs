
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 8f;

    [Header("Hit Detection")]
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private float hitRadius = 0f; // 0 = thin ray, >0 = sphere sweep (fatter projectile)
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("On Hit")]
    [SerializeField] private GameObject hitEffectPrefab; // e.g. the "balls of light" burst
    [SerializeField, Min(0f)] private float hitEffectLifetime = 5f;
    [SerializeField] private AudioSource hitEffectSound;
    [Header("Projectile Visual")]
    [SerializeField] private GameObject projectileVisual;

    private Vector3 lastFramePos;
    private bool hasHit;

    private void Start()
    {
        lastFramePos = transform.position;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (hasHit)
        {
            return;
        }

        Vector3 direction = transform.forward;
        Vector3 nextPos = lastFramePos + direction * (speed * Time.deltaTime);

        // Sweep from where we were to where we're about to be, so a fast shot can't skip over a thin collider
        Vector3 segment = nextPos - lastFramePos;
        float distance = segment.magnitude;

        if (distance > 0f && Sweep(lastFramePos, segment / distance, distance, out RaycastHit hit))
        {
            transform.position = hit.point;
            OnHit(hit);
            return;
        }

        transform.position = nextPos;
        lastFramePos = nextPos;
    }

    private bool Sweep(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
    {
        if (hitRadius > 0f)
        {
            return Physics.SphereCast(
                origin, hitRadius, direction, out hit, distance, hitLayers, triggerInteraction);
        }

        return Physics.Raycast(origin, direction, out hit, distance, hitLayers, triggerInteraction);
    }

    private void OnHit(RaycastHit hit)
    {
        hasHit = true;
        float duration = 0.0f;
        if (hitEffectPrefab != null)
        {
            // Face the effect away from the surface we hit
            GameObject hitEffect = Instantiate(
                hitEffectPrefab,
                hit.point,
                Quaternion.LookRotation(hit.normal));
            Destroy(hitEffect, hitEffectLifetime);
            if (hitEffectSound != null)
            {
                hitEffectSound.Play();
                duration = hitEffectSound.clip.length;
            }
        }
        projectileVisual.SetActive(false);
        Destroy(gameObject, duration);
    }
}