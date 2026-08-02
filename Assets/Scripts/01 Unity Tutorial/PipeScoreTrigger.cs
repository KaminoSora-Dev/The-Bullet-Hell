using Unity.VisualScripting.Dependencies.NCalc;
using UnityEngine;

public class PipeScoreTrigger : MonoBehaviour
{
    public PlayerScore onMiddleTrigger;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        onMiddleTrigger = GameObject.FindGameObjectWithTag("GameManager").GetComponent<PlayerScore>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        onMiddleTrigger.increaseScore();
    }
}
