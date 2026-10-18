using UnityEngine;

public class moving : MonoBehaviour
{
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField, Min(0f)] private float speed = 2f;

    private Transform destination;

    private void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogError("Moving platform requires both Point A and Point B to be assigned.", this);
            enabled = false;
            return;
        }

        transform.position = pointA.position;
        destination = pointB;
    }

    private void Update()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            destination.position,
            speed * Time.deltaTime);

        if (transform.position == destination.position)
        {
            destination = destination == pointA ? pointB : pointA;
        }
    }
}
