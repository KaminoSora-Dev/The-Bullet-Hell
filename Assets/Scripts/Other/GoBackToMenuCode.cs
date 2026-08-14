using UnityEngine;
using UnityEngine.SceneManagement;
public class GoBackToMenuCode : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SceneManager.LoadScene("MyMainMenu");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
