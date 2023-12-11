using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// A Airplane Machine Learning Agent
/// </summary>
public class AirplaneAgent : Agent
{
    [Tooltip("Force to apply when moving")]
    public float moveForce = 4f;

    [Tooltip("Speed to pitch up or down")]
    public float pitchSpeed = 100f;

    [Tooltip("Speed to rotate around the up axis")]
    public float yawSpeed = 100f;

    [Tooltip("Transform at the tip of the airplane nose")]
    public Transform airplaneNose;

    [Tooltip("The agent's camera")]
    public Camera agentCamera;

    [Tooltip("Whether this is training mode or gameplay mode")]
    public bool trainingMode;

    [Tooltip("Update Checkpoint area")]
    public bool updateArea;

    // The rigidbody of the agent
    new private Rigidbody rigidbody;

    // The Checkpoint area that the agent is in
    private CheckpointArea CheckpointArea;

    // The nearest Checkpoint to the agent
    private Checkpoint nearestCheckpoint;

    // Allows for smoother pitch changes
    private float smoothPitchChange = 0f;

    // Allow for smoother yaw changes
    private float smoothYawChange = 0f;

    // Maximum angle that the airplane can pitch up or down
    private const float MaxPitchAngle = 80f;

    // Maximum distance from the airplane nose to accept Points collision
    private const float AirplaneNoseRadius = 0.02f;

    // Whether the agent is frozen
    private bool frozen = false;

    /// <summary>
    /// The amount of Points the agent has obtained this episode
    /// </summary>
    public float PointsObtained { get; private set; }

    /// <summary>
    /// Initialize the agent
    /// </summary>
    public override void Initialize()
    {
        rigidbody = GetComponent<Rigidbody>();
        CheckpointArea = GetComponentInParent<CheckpointArea>();

        // If not training mode, no max step, play forever
        if (!trainingMode) MaxStep = 0;
    }

    /// <summary>
    /// Reset the agent when an episode begins
    /// </summary>
    public override void OnEpisodeBegin()
    {
        if (trainingMode)
        {
            // Only reset Checkpoints in training when there is one agent per area
            CheckpointArea.ResetCheckpoints();
        }

        if (updateArea)
        {
            // Only reset Checkpoints in game when there is one agent with flag per area(activate in testing purposes)
            CheckpointArea.ResetCheckpointsGame();
        }

        // Reset Points obtained
        PointsObtained = 0f;

        // Zero out velocities so that movement steps before a new episode begins
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;

        // Default to spawning in front of a Checkpoint(in case of adding additional modes)
        bool inFrontOfCheckpoint = true;
        if (trainingMode)
        {
            // Spawn in front of Checkpoint 0% of the time during training
            inFrontOfCheckpoint = UnityEngine.Random.value > 1f;
        }
        else
        {
            // Spawn in front of Checkpoint 50% of the time during the game
            inFrontOfCheckpoint = UnityEngine.Random.value >= .5f;
        }

        // Move the agent to a new random position
        MoveToSafeRandomPosition(inFrontOfCheckpoint);

        // Recalculate the nearest Checkpoint now that the agent has moved
        UpdateNearestCheckpoint();
    }

    /// <summary>
    /// Called when action is received from either the player input or the neural network
    /// 
    /// </summary>
    /// <param name="vectorAction">The actions to take</param>
    public override void OnActionReceived(float[] vectorAction)
    {
        // Don't take actions if frosen
        if (frozen) return;

        rigidbody.AddRelativeForce(0f, 0f, (moveForce * 0.5f));
        float move_forward_backward = Mathf.Clamp(vectorAction[0], -1f, 1f);
        if (move_forward_backward >= 0)
        {
            rigidbody.AddRelativeForce(0f, 0f, move_forward_backward * moveForce);
            rigidbody.AddForce(0f, 0.95f, 0f);
        }
        if (move_forward_backward < 0)
        {
            rigidbody.AddRelativeForce(0f, 0f, move_forward_backward * moveForce * 0.3f);
            rigidbody.AddForce(0f, (0.9f + 0.5f * move_forward_backward), 0f);
        }

        // Get current rotation
        Vector3 rotationVector = transform.rotation.eulerAngles;

        // Calculate pitch and yaw rotation
        float pitchChange = vectorAction[1];
        float yawChange = vectorAction[2];

        // Calculate smooth rotation changes
        smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
        smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);

        // Calculate new pitch and yaw based on smooth values
        // Clamp pitch to avoid flipping upside down
        float pitch = rotationVector.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
        if (pitch > 180f) pitch -= 360f;
        pitch = Mathf.Clamp(pitch, -MaxPitchAngle, MaxPitchAngle);
        

        float yaw = rotationVector.y + smoothYawChange * Time.fixedDeltaTime * yawSpeed;

        // Apply the new rotation
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>
    /// Collect vector observations from the environment
    /// </summary>
    /// <param name="sensor">The vector sensor</param>
    public override void CollectObservations(VectorSensor sensor)
    {
        // If nearestCheckpoint is null, observe an empty array and return early
        if (nearestCheckpoint == null)
        {
            sensor.AddObservation(new float[9]);
            return;
        }
        
        // Observe the agent's local rotation (4 observations)
        sensor.AddObservation(transform.localRotation.normalized);

        // Get a vector from the airplane nose to the nearest Checkpoint
        Vector3 toCheckpoint = nearestCheckpoint.CheckpointCenterPosition - airplaneNose.position;

        // Observe a normalized vector pointing to the nearest Checkpoint (3 observations)
        sensor.AddObservation(toCheckpoint.normalized);

        // Observe a dot product that indicates whether the airplane nose is pointing toward the Checkpoint (1 observation)
        // (+1 means that the airplane nose is pointing directly at the Checkpoint, -1 means directly away)
        sensor.AddObservation(Vector3.Dot(airplaneNose.forward.normalized, toCheckpoint.normalized));

        // Observe the relative distance from the airplane nose to the Checkpoint (1 observation)
        sensor.AddObservation(toCheckpoint.magnitude / CheckpointArea.AreaDiameter);

        // 9 total observations
    }

    /// <summary>
    /// When Behavior Type is set to "Hueristic Only" on the agent's Behavior Parameters,
    /// this function will be called. Its return values will be fed into
    /// <see cref="OnActionReceived(float[])"/> instead of using the neural network
    /// </summary>
    /// <param name="actionsOut">And output action array</param>
    public override void Heuristic(float[] actionsOut)
    {
        float move = 0f;
        float pitch = 0f;
        float yaw = 0f;
        // Convert keyboard inputs to movement and turning
        // All values should be between -1 and +1

        // Forward/backward
        if (Input.GetKey(KeyCode.W)) move = 1f;
        else if (Input.GetKey(KeyCode.S)) move = -1f;

        // Pitch up/down
        if (Input.GetKey(KeyCode.UpArrow)) pitch = 1f;
        else if (Input.GetKey(KeyCode.DownArrow)) pitch = -1f;

        // Turn left/right
        if (Input.GetKey(KeyCode.LeftArrow)) yaw = -1f;
        else if (Input.GetKey(KeyCode.RightArrow)) yaw = 1f;

        // Add the movement value, pitch and yaw to the actionsOut array
        actionsOut[0] = move;
        actionsOut[1] = pitch;
        actionsOut[2] = yaw;
    }

    /// <summary>
    /// Prevent the agent from moving and taking actions
    /// </summary>
    public void FreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not supported in training");
        frozen = true;
        rigidbody.useGravity = false;
        rigidbody.Sleep();
    }

    /// <summary>
    /// Resume agent moving and taking actions
    /// </summary>
    public void UnfreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not supported in training");
        frozen = false;
        rigidbody.useGravity = true;
        rigidbody.WakeUp();
    }

    /// <summary>
    /// Move the agent to a safe random position (i.e. does not collide with anything)
    /// If in front of Checkpoint, also point the airplane nose at the Checkpoint
    /// </summary>
    /// <param name="inFrontOfCheckpoint">Whether to choose a spot in front of a Checkpoint</param>
    private void MoveToSafeRandomPosition(bool inFrontOfCheckpoint)
    {
        bool safePositionFound = false;
        int attemptsRemaining = 100; // Prevent an infinite loop
        Vector3 potentialPosition = Vector3.zero;
        Quaternion potentialRotation = new Quaternion();

        // Loop until a safe position is found or we run out of attempts
        while(!safePositionFound && attemptsRemaining > 0)
        {
            attemptsRemaining--;
            if (inFrontOfCheckpoint)
            {
                //Pick a random Checkpoint
                Checkpoint randomCheckpoint = CheckpointArea.Checkpoints[UnityEngine.Random.Range(0, CheckpointArea.Checkpoints.Count)];

                // Position in front of the Checkpoint
                float distanceFromCheckpoint = UnityEngine.Random.Range(.2f, 1f);
                potentialPosition = randomCheckpoint.transform.position + randomCheckpoint.CheckpointCenterPosition * distanceFromCheckpoint;

                // Point airplane nose at Checkpoint
                Vector3 toCheckpoint = randomCheckpoint.CheckpointCenterPosition - potentialPosition;
                potentialRotation = Quaternion.LookRotation(toCheckpoint, Vector3.up);
            }
            else
            {
                // Pick a random height from the ground
                float height = UnityEngine.Random.Range(1.2f, 3.5f);

                // Pick a random radius from the center of the area
                float radius = UnityEngine.Random.Range(2f, 5f);

                // Pick a random direction rotated around the y axis
                Quaternion direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-180f, 180f), 0f);

                // Combine height, radius and direction to pick a potential position
                potentialPosition = CheckpointArea.transform.position + Vector3.up * height + direction * Vector3.forward * radius;

                // Choose and set random starting pitch and yaw
                float pitch = UnityEngine.Random.Range(-10f, 10f);
                float yaw = UnityEngine.Random.Range(-180f, 180f);
                potentialRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // Check to see if the agent will collide with anything
            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 0.5f);

            // Safe position has been found if no colliders are overlapped
            safePositionFound = colliders.Length == 0;
        }

        Debug.Assert(safePositionFound, "Could not find a safe position to spawn");

        // Set the position and rotation
        transform.position = potentialPosition;
        transform.rotation = potentialRotation;
    }

    /// <summary>
    /// Update the nearest Checkpoint to the agent
    /// </summary>
    private void UpdateNearestCheckpoint()
    {
        foreach (Checkpoint Checkpoint in CheckpointArea.Checkpoints)
        {
            if (nearestCheckpoint == null && Checkpoint.HasPoints)
            {
                // No current nearest Checkpoint and this Checkpoint has Points, so set to this Checkpoint
                nearestCheckpoint = Checkpoint;
            }
            else if (Checkpoint.HasPoints)
            {
                // Calculate distance to this Checkpoint and distance to the current nearest Checkpoint
                float distanceToCheckpoint = Vector3.Distance(Checkpoint.transform.position, airplaneNose.position);
                float distanceToCurrentNearestCheckpoint = Vector3.Distance(nearestCheckpoint.transform.position, airplaneNose.position);

                // If current nearest Checkpoint is empty OR this Checkpoint is closer, update the nearest Checkpoint
                if (!nearestCheckpoint.HasPoints || distanceToCheckpoint < distanceToCurrentNearestCheckpoint)
                {
                    nearestCheckpoint = Checkpoint;
                }
            }
        }
    }

    /// <summary>
    /// Called when the agent's collider enters a trigger collider
    /// </summary>
    /// <param name="other">The trigger collider</param>
    private void OnTriggerEnter(Collider other)
    {
        TriggerEnterOrStay(other);
    }

    /// <summary>
    /// Called when the agent's collider stays in a trigger collider
    /// </summary>
    /// <param name="other">The trigger collider</param>
    private void OnTriggerStay(Collider other)
    {
        TriggerEnterOrStay(other);
    }

    /// <summary>
    /// Handles when the agent's collider enters or stays in a trigger collider
    /// </summary>
    /// <param name="collider">The trigger collider</param>
    private void TriggerEnterOrStay(Collider collider)
    {
        // Check if agent is colliding with Points
        if (collider.CompareTag("Points"))
        {
            Vector3 closestPointToAirplaneNose = collider.ClosestPoint(airplaneNose.position);

            // Check if the closest collision point is close to the airplane nose
            // Note: a collision with anything but the airplane nose should not count
            if (Vector3.Distance(airplaneNose.position, closestPointToAirplaneNose) < AirplaneNoseRadius)
            {
                // Look up the Checkpoint for this Points collider
                Checkpoint Checkpoint = CheckpointArea.GetCheckpointFromPoints(collider);

                // Attempt to take Points
                float PointsRecieved = Checkpoint.GainPoints(1f);

                // Keep track of Points obtained
                PointsObtained += PointsRecieved;

                if (trainingMode)
                {
                    //Calculate reward for getting Points
                    float bonus = 0.2f * Mathf.Clamp01(Vector3.Dot(transform.forward.normalized, (nearestCheckpoint.CheckpointCenterPosition - airplaneNose.position).normalized));
                    AddReward(2f + bonus);
                }

                // If Checkpoint is empty, update the nearest Checkpoint
                if (!Checkpoint.HasPoints)
                {
                    UpdateNearestCheckpoint();
                }
            }
        }
    }

    /// <summary>
    /// Called when the agent collides with something solid
    /// </summary>
    /// <param name="collision">The collision info</param>
    private void OnCollisionEnter(Collision collision)
    {
        if (trainingMode && collision.collider.CompareTag("boundary"))
        {
            // Collided with the area boundary, give a negative reward
            AddReward(-0.2f);
        }
    }

    /// <summary>
    /// Called every frame
    /// </summary>
    private void Update()
    {
        // Draw a line from the airplane nose to the nearest Checkpoint
        if (nearestCheckpoint != null)
            Debug.DrawLine(airplaneNose.position, nearestCheckpoint.CheckpointCenterPosition, Color.green);
    }

    /// <summary>
    /// Called every .02 seconds
    /// </summary>
    private void FixedUpdate()
    {
        // Avoids scenario where nearest Checkpoint Points is stolen by opponent and not updated
        if (nearestCheckpoint != null && !nearestCheckpoint.HasPoints)
            UpdateNearestCheckpoint();
    }
}
