using UnityEngine;

public class BirdMoving : MonoBehaviour
{
    public Rigidbody2D myRigidBody;
    // Reference to the Rigidbody2D component of the bird object

    public float birdJumpPower = 5f;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameObject.name = "The Bird Is Flying";
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            myRigidBody.linearVelocity = Vector2.up * birdJumpPower;
            // Set the linear velocity of the bird to move upwards at a speed of 5 units per second
            // Call the Jump method when the space key is pressed
        }


    }
}
