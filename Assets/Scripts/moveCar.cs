using UnityEngine;

public class CarMovement : MonoBehaviour
{
    public float speed = 10f;
    public float moveTime = 10f;

    [Header("Direção do movimento")]
    public Vector3 direction = Vector3.forward; // você controla isso na Unity

    private Vector3 startPosition;
    private float timer;

    void Start()
    {
        startPosition = transform.position;
        timer = moveTime;

        // Garante que a direção esteja normalizada
        direction = direction.normalized;
    }

    void Update()
    {
        // Move na direção definida
        transform.Translate(direction * speed * Time.deltaTime, Space.World);

        timer -= Time.deltaTime;

        if (timer <= 0)
        {
            transform.position = startPosition;
            timer = moveTime;
        }
    }
}