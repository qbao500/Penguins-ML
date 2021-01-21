using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAgents;

public class PenguinAgent : Agent
{
    [Tooltip("How fast the agent moves forward")]
    public float moveSpeed = 5f;

    [Tooltip("How fast the agent turns")]
    public float turnSpeed = 180f;

    [Tooltip("Prefab of the heart that appears when the baby is fed")]
    public GameObject heartPrefab;

    [Tooltip("Prefab of the regurgitated fish that appears when the baby is fed")]
    public GameObject regurgitatedFishPrefab;

    private PenguinArea penguinArea;

    new private Rigidbody rigidbody;

    private GameObject baby;

    private GameObject bin;

    private bool isFull;

    private int item; // 0 = not carrying anything, 1 = carrying fish, 2 = carrying bone

    private float feedRadius = 0f;

    public override void Initialize()
    {
        base.Initialize();
        penguinArea = GetComponentInParent<PenguinArea>();
        baby = penguinArea.penguinBaby;
        bin = penguinArea.trashBin;
        rigidbody = GetComponent<Rigidbody>();
    }

    /// Perform actions based on a vector of numbers
    /// <param name="vectorAction">The list of actions to take</param>
    public override void OnActionReceived(float[] vectorAction)
    {
        // Convert the first action to forward movement
        float fowardAmount = vectorAction[0];

        // Convert the second action to turning left or right
        float turnAmount = 0f;
        if (vectorAction[1] == 1f)
        {
            turnAmount = -1f;
        }
        else if (vectorAction[1] == 2f)
        {
            turnAmount = 1f;
        }

        // Apply movement
        rigidbody.MovePosition(transform.position + transform.forward * fowardAmount * moveSpeed * Time.fixedDeltaTime);
        transform.Rotate(transform.up * turnAmount * turnSpeed * Time.fixedDeltaTime);

        // Apply a tiny negative reward every step to encourage action
        if (maxStep > 0)
        {
            AddReward(-1f / maxStep);
        }
    }

    /// Read inputs from the keyboard and convert them to a list of actions.
    /// This is called only when the player wants to control the agent and has set
    /// Behavior Type to "Heuristic Only" in the Behavior Parameters inspector.
    /// <returns>A vectorAction array of floats that will be passed into <see cref="AgentAction(float[])"/></returns>
    public override float[] Heuristic()
    {
        float forwardAction = 0f;
        float turnAction = 0f;

        if (Input.GetKey(KeyCode.W))
        {
            // move forward
            forwardAction = 1f;
        }
        if (Input.GetKey(KeyCode.A))
        {
            // turn left
            turnAction = 1f;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            // turn right
            turnAction = 2f;
        }

        // Put the actions into an array and return
        return new float[] { forwardAction, turnAction };
    }

    // Reset the agent and area
    public override void OnEpisodeBegin()
    {
        isFull = false;
        item = 0;
        penguinArea.ResetArea();
        feedRadius = Academy.Instance.FloatProperties.GetPropertyWithDefault("feed_radius", 0f);
    }

    // Collect all non-Raycast observations
    public override void CollectObservations(MLAgents.Sensors.VectorSensor sensor)
    {
        // Check item carrying (1 int = 1 value)
        sensor.AddObservation(item);

        // Whether the penguin has eaten a fish (1 bool = 1 value)
        sensor.AddObservation(isFull);

        // Distance to the bin (1 float = 1 value)
        sensor.AddObservation(Vector3.Distance(bin.transform.position, transform.position));

        // Direction to bin (1 Vector3 = 3 values)
        sensor.AddObservation((bin.transform.position - transform.position).normalized);

        // Distance to the baby (1 float = 1 value)
        sensor.AddObservation(Vector3.Distance(baby.transform.position, transform.position));
        
        // Direction to baby (1 Vector3 = 3 values)
        sensor.AddObservation((baby.transform.position - transform.position).normalized);
    
        // Direction penguin is facing (1 Vector3 = 3 values)
        sensor.AddObservation(transform.forward);

        // 1 +  1 + 1 + 3 + 1 + 3 + 3 = 13 total values
    }

    private void FixedUpdate()
    {
        // Request a decision every 5 steps. RequestDecision() automatically calls RequestAction(),
        // but for the steps in between, we need to call it explicitly to take action using the results
        // of the previous decision
        if (StepCount % 5 == 0)
        {
            RequestDecision();
        }
        else
        {
            RequestAction();
        }

        // Test if the agent is close enough to feed the baby
        if (Vector3.Distance(transform.position, baby.transform.position) < feedRadius)
        {
            // Close enough, try to feed the baby
            RegurgitateFish();
        }

        // Test if the agent is close enough to throw the bone
        if (Vector3.Distance(transform.position, bin.transform.position) < feedRadius)
        {
            ThrowBone();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.CompareTag("fish"))
        {
            // Try to eat the fish
            EatFish(collision.gameObject);
        }
        else if (collision.transform.CompareTag("bone"))
        {
            CarryBone(collision.gameObject);
        }
        else if (collision.transform.CompareTag("baby"))
        {
            // Try to feed the baby
            RegurgitateFish();
        }
        else if (collision.transform.CompareTag("bin"))
        {
            ThrowBone();
        }
    }

    // Check if agent is full, if not, eat the fish and get a reward
    private void EatFish(GameObject fishObject)
    {
        if (isFull) return; // Can't eat another fish while full
        isFull = true;
        item = 1;

        penguinArea.RemoveSpecificFish(fishObject);
                
        AddReward(1f);
    }

    private void CarryBone(GameObject boneObject)
    {
        if (isFull) return; // Can't carry another while full
        isFull = true;
        item = 2;

        penguinArea.RemoveSpecificBone(boneObject);

        AddReward(1f);   
    }

    // Check if agent is full, if yes, feed the baby
    private void RegurgitateFish()
    {
        if (!isFull) return; // Nothing to regurgitate
        isFull = false;
 
        if (item != 1)
        {
            AddReward(-1f);

            item = 0;
        }
        else
        {
            // Spawn regurgitated fish
            GameObject regurgitatedFish = Instantiate<GameObject>(regurgitatedFishPrefab);
            regurgitatedFish.transform.parent = transform.parent;
            regurgitatedFish.transform.position = baby.transform.position;
            Destroy(regurgitatedFish, 4f);

            // Spawn heart
            GameObject heart = Instantiate<GameObject>(heartPrefab);
            heart.transform.parent = transform.parent;
            heart.transform.position = baby.transform.position + Vector3.up;
            Destroy(heart, 4f);
                        
            AddReward(1f);

            item = 0;
        }

        if (penguinArea.FishRemaining <= 0 && penguinArea.BoneRemaining <= 0)
        {
            EndEpisode();
        }
    }

    private void ThrowBone()
    {
        if (!isFull) return; // Nothing to throw
        isFull = false;
      
        if (item == 2)
        {
            // Spawn heart
            GameObject heart = Instantiate<GameObject>(heartPrefab);
            heart.transform.parent = transform.parent;
            heart.transform.position = bin.transform.position + Vector3.up;
            Destroy(heart, 4f);
                        
            AddReward(1f);

            item = 0;
        }
        else
        {
            AddReward(-1f);

            item = 0;
        }

        if (penguinArea.FishRemaining <= 0 && penguinArea.BoneRemaining <= 0)
        {
            EndEpisode();
        }
    }
}
