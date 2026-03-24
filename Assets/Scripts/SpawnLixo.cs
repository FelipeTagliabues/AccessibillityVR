using UnityEngine;

public class SpawnLixo : MonoBehaviour
{
    public GameObject lixoPrefab;
    public Transform spawnPoint;

    public void SpawnarLixo()
    {
        Debug.Log("BOTÃO FUNCIONOU!");

        Instantiate(lixoPrefab, spawnPoint.position, spawnPoint.rotation);
    }
}