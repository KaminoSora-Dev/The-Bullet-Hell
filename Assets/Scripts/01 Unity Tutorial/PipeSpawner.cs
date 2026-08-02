using UnityEngine;

public class PipeSpawner : MonoBehaviour
{
    public GameObject pipePrefab;
    public float spawnRate = 1f;
    private float timer = 4f;

    public float heightOffset = 10f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (timer < spawnRate)
        {
            timer = timer + Time.deltaTime;
        }

        else
        {
            SpawnPipe();
            timer = 0f;
            
        }

    }

    void SpawnPipe()
    {
        float lowestPoint = transform.position.y - heightOffset; //Calculate the lowest point for the pipe spawn position
        float highestPoint = transform.position.y + heightOffset; //Calculate the highest point for the pipe spawn position
        Instantiate(pipePrefab, new Vector3(transform.position.x, Random.Range(lowestPoint, highestPoint), transform.position.z), transform.rotation);
    }
}
