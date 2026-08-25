using UnityEngine;
using UnityEngine.SceneManagement;
public class GoBackToMenuCode : MonoBehaviour
{
    public string sceneName = "MainMenu"; // ชื่อของ Scene ที่ต้อง
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SceneManager.LoadScene(sceneName);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
