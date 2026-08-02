using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PipeDeathTrigger : MonoBehaviour
{
    public void deathTrigger()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        deathTrigger();
    }
}
