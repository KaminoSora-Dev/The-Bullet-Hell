using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class PlayerScore : MonoBehaviour
{
    public int playerScore = 0;
    public TMP_Text scoreText;

    [ContextMenu("increaseScore")] // For Testing, letting the Void code works when it doesn't use anywhere.
    public void increaseScore()
    {
        playerScore = playerScore+1;
        scoreText.text = playerScore.ToString();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
