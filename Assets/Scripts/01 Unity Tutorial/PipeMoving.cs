using UnityEngine;
using UnityEngine.Rendering;

public class PipeMoving : MonoBehaviour
{
    public float pipeMoveSpeed = 2f;

    public float spawnDeadzone = -45f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = transform.position + (Vector3.left * pipeMoveSpeed) * Time.deltaTime;
        if (transform.position.x < spawnDeadzone)
        {
            Debug.Log("Object Destroyed!");
            Destroy(gameObject);
        }
    }
}
