using UnityEngine;

public class CarHorn : MonoBehaviour
{
    public Transform player;        // referência ao player (XR Origin)
    public float triggerDistance = 5f; // distância para buzinar
    public AudioSource audioSource; // fonte de áudio (buzina)
    public float cooldown = 3f;     // tempo entre buzinas

    private float timer = 0f;

    void Update()
    {
        if (player == null || audioSource == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        timer -= Time.deltaTime;

        if (distance <= triggerDistance && timer <= 0f)
        {
            audioSource.Play();
            timer = cooldown;
        }
    }
}