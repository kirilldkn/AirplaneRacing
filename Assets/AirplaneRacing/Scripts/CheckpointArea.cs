using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a collection of Checkpoint objects and attached Checkpoints
/// </summary>

public class CheckpointArea : MonoBehaviour
{
    // The diameter of the area where the agent and flwers can be
    // used for observing relative distance from agent to Checkpoint
    public const float AreaDiameter = 20f;

    // The list of all Checkpoint objects in this Checkpoint area
    private List<GameObject> Checkpointobjects;

    // A lookup dictionary for looking up a Checkpoint from a Points collider
    private Dictionary<Collider, Checkpoint> PointsCheckpointDictionary;

    /// <summary>
    /// The list of all Checkpoints in the Checkpoint area
    /// </summary>
    public List<Checkpoint> Checkpoints { get; private set; }

    /// <summary>
    /// Reset the Checkpoints and Checkpoint objects in trainingMode
    /// </summary>
    public void ResetCheckpoints()
    {
        // Rotate each Checkpoint object around the Y axis and subtly around X and Z
        foreach (GameObject Checkpointobject in Checkpointobjects)
        {
            bool safePositionFound = false;
            int attemptsRemaining = 100; // Prevent an infinite loop
            Vector3 newPosition = Vector3.zero;

            // Loop until a safe position is found or we run out of attempts
            while (!safePositionFound && attemptsRemaining > 0)
            {
                attemptsRemaining--;

                {
                    // Pick a random height from the ground
                    float xMove = UnityEngine.Random.Range(-3f, 3f);

                    // Pick a random radius from the center of the area
                    float yMove = UnityEngine.Random.Range(0.1f, 4f);

                    // Pick a random direction rotated around the y axis
                    float zMove = UnityEngine.Random.Range(-3f, 3f);


                    // Combine height, radius and direction to pick a potential position
                    newPosition = new Vector3(xMove, yMove, zMove);

                }

                // Check to see if the agent will collide with anything
                Collider[] colliders = Physics.OverlapSphere(newPosition, 0.3f);

                // Safe position has been found if no colliders are overlapped
                safePositionFound = colliders.Length == 0;
            }

            Debug.Assert(safePositionFound, "Could not find a safe position to spawn");

            // Set the position and rotation
            Checkpointobject.transform.localPosition = newPosition;

        }

        // Reset each Checkpoint
        foreach (Checkpoint Checkpoint in Checkpoints)
        {
            Checkpoint.ResetCheckpoint();
        }
    }
    /// <summary>
    /// Reset the Checkpoints and Checkpoint objects in updateArea mode (Game or demo mode)
    /// </summary>
    public void ResetCheckpointsGame()
    {
        // Rotate each Checkpoint object around the Y axis and subtly around X and Z
        foreach (GameObject Checkpointobject in Checkpointobjects)
        {
            bool safePositionFound = false;
            int attemptsRemaining = 100; // Prevent an infinite loop
            Vector3 newPosition = Vector3.zero;

            // Loop until a safe position is found or we run out of attempts
            while (!safePositionFound && attemptsRemaining > 0)
            {
                attemptsRemaining--;

                {
                    // Pick a random height from the ground
                    float xMove = UnityEngine.Random.Range(-6f, 6f);

                    // Pick a random radius from the center of the area
                    float yMove = UnityEngine.Random.Range(0.8f, 4f);

                    // Pick a random direction rotated around the y axis
                    float zMove = UnityEngine.Random.Range(-6f, 6f);


                    // Combine height, radius and direction to pick a potential position
                    newPosition = new Vector3(xMove, yMove, zMove);

                }

                // Check to see if the agent will collide with anything
                Collider[] colliders = Physics.OverlapSphere(newPosition, 0.3f);

                // Safe position has been found if no colliders are overlapped
                safePositionFound = colliders.Length == 0;
            }

            Debug.Assert(safePositionFound, "Could not find a safe position to spawn");

            // Set the position and rotation
            Checkpointobject.transform.position = newPosition;

        }

        // Reset each Checkpoint
        foreach (Checkpoint Checkpoint in Checkpoints)
        {
            Checkpoint.ResetCheckpoint();
        }
    }
    /// <summary>
    /// Gets the <see cref="Checkpoint"/> that a Points collider belongs to
    /// </summary>
    /// <param name="collider">The Points collider</param>
    /// <returns>The matching Checkpoint</returns>
    public Checkpoint GetCheckpointFromPoints(Collider collider)
    {
        return PointsCheckpointDictionary[collider];
    }

    /// <summary>
    /// Called when the area wakes up
    /// </summary>
    private void Awake()
    {
        // Initialize variables
        Checkpointobjects = new List<GameObject>();
        PointsCheckpointDictionary = new Dictionary<Collider, Checkpoint>();
        Checkpoints = new List<Checkpoint>();
    }

    /// <summary>
    /// Called when the game starts
    /// </summary>
    private void Start()
    {
        // Find all Checkpoints that are children of this GameObject/Transform
        FindChildCheckpoints(transform);
    }

    /// <summary>
    /// Recursively finds all Checkpoints and Checkpoint objects that are children of a parent transform
    /// </summary>
    /// <param name="parent">The parent of the children to check</param>
    private void FindChildCheckpoints(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if(child.CompareTag("Checkpoint_object"))
            {
                // Found a Checkpoint object, add it to the Checkpointobjects list
                Checkpointobjects.Add(child.gameObject);

                // Look for Checkpoints within the Checkpoint object
                FindChildCheckpoints(child);
            }
            else
            {
                // Not a Checkpoint object, look for a Checkpoint component
                Checkpoint Checkpoint = child.GetComponent<Checkpoint>();
                if (Checkpoint != null)
                {
                    // Found a Checkpoint, add it to the Checkpoints list
                    Checkpoints.Add(Checkpoint);

                    // Add the Points collider to the lookup dictionary
                    PointsCheckpointDictionary.Add(Checkpoint.PointsCollider, Checkpoint);

                    // Note: there are no Checkpoints that are children of other Checkpoints
                }
                else
                {
                    // Checkpoint component not found, so check children
                    FindChildCheckpoints(child);
                }
            }
        }
    }
}
