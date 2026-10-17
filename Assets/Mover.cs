
using UnityEngine;

public class Mover : MonoBehaviour
{
    Vector3 start;
    Vector3 final;
    float time = 25.0f;
    float alpha = 0.0f;
    Vector3 origin;
    Vector3 offset = new Vector3(0.0f, 0.0f, 15.0f);
    int Mode = 1;

    float rotationSpeed = 45.0f; // Degrees per second

    void Start()
    {
        origin = transform.position;
        start = origin;
        final = origin + (offset * Mode);
    }

    void Update()
    {
        alpha += Mathf.Min(Time.deltaTime / time, 1.0f);

        if (alpha >= 1.0f - 0.00001f)
        {
            alpha -= 1.0f;
            Mode *= -1;
            start = final;
            final = origin + (offset * Mode);
        }

        transform.position = Vector3.Lerp(start, final, alpha);

        // Rotate indefinitely around the Y-axis
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }
}
