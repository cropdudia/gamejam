using UnityEngine;

public class ShadowDetector : MonoBehaviour
{
    [Header("References")]
    public Light sunLight;
    [Tooltip("Layers that cast shadows. Do NOT include the player's own layer.")]
    public LayerMask shadowLayers = ~0;

    [Header("Sampling")]
    [Tooltip("Radius of the ring of sample points around the player (match your capsule radius or a bit smaller).")]
    public float sampleRadius = 0.25f;
    [Tooltip("Heights (above the feet) at which sample rings are placed.")]
    public float[] sampleHeights = { 0.1f, 0.9f, 1.7f };
    public int pointsPerRing = 6;
    [Tooltip("Small push toward the sun so rays don't start inside the ground.")]
    public float originBias = 0.03f;
    public float maxDistance = 500f;

    [Header("Decision")]
    [Tooltip("Fraction of sample points that must be shaded to count as 'in shadow'. 1 = fully shaded, 0.5 = half.")]
    [Range(0.01f, 1f)] public float shadedThreshold = 0.6f;
    [Tooltip("Seconds the state must stay lit before it counts (removes flicker at borders).")]
    public float litGraceTime = 0.1f;

    /// <summary>0 = fully lit, 1 = fully shaded.</summary>
    public float ShadedFraction { get; private set; } = 1f;
    public bool IsInShadow { get; private set; } = true;
    public bool IsInSunlight => !IsInShadow;

    private readonly RaycastHit[] hits = new RaycastHit[8];
    private float litTimer;

    void Update()
    {
        if (sunLight == null) return;

        Vector3 toSun = -sunLight.transform.forward;
        Vector3 feet = FeetPosition();

        int total = 0, shaded = 0;
        foreach (float h in sampleHeights)
        {
            // center point of the ring, then ring around it
            Sample(feet + Vector3.up * h, toSun, ref total, ref shaded);
            for (int i = 0; i < pointsPerRing; i++)
            {
                float a = i * Mathf.PI * 2f / pointsPerRing;
                Vector3 offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * sampleRadius;
                Sample(feet + Vector3.up * h + offset, toSun, ref total, ref shaded);
            }
        }

        ShadedFraction = total > 0 ? (float)shaded / total : 1f;
        bool rawShaded = ShadedFraction >= shadedThreshold;

        if (rawShaded)
        {
            litTimer = 0f;
            IsInShadow = true;
        }
        else
        {
            litTimer += Time.deltaTime;
            if (litTimer >= litGraceTime) IsInShadow = false;
        }
    }

    // Player's pivot may be at the center of the capsule; find the feet instead.
    Vector3 FeetPosition()
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null)
            return transform.TransformPoint(cc.center) - Vector3.up * (cc.height * 0.5f);
        return transform.position;
    }

    void Sample(Vector3 point, Vector3 toSun, ref int total, ref int shaded)
    {
        total++;
        Vector3 origin = point + toSun * originBias;
        int count = Physics.RaycastNonAlloc(origin, toSun, hits, maxDistance,
            shadowLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            // ignore the player's own colliders
            if (hits[i].collider.transform.IsChildOf(transform)) continue;
            shaded++;
            return;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (sunLight == null) return;
        Vector3 toSun = -sunLight.transform.forward;
        Vector3 feet = FeetPosition();
        Gizmos.color = Color.yellow;
        foreach (float h in sampleHeights)
        {
            Gizmos.DrawRay(feet + Vector3.up * h, toSun * 2f);
            for (int i = 0; i < pointsPerRing; i++)
            {
                float a = i * Mathf.PI * 2f / pointsPerRing;
                Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * sampleRadius;
                Gizmos.DrawRay(feet + Vector3.up * h + o, toSun * 2f);
            }
        }
    }
}