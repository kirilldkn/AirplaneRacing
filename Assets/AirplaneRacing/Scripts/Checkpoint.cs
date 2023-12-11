using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a single Checkpoint with Points
/// </summary>
public class Checkpoint : MonoBehaviour
{
    [Tooltip("The color when the Checkpoint is active")]
    public Color fullCheckpointColor = new Color(1f, 0f, 3f);

    [Tooltip("The color when the Checkpoint is inactive")]
    public Color emptyCheckpointColor = new Color(5f, 0f, 1f);

    /// <summary>
    /// The trigger collider representing the Points gain
    /// </summary>
    [HideInInspector]
    public Collider PointsCollider;

    // The Checkpoint's material
    private Material CheckpointMaterial;

    /// <summary>
    /// The center position of the Points collider
    /// </summary>
    public Vector3 CheckpointCenterPosition
    {
        get
        {
            return PointsCollider.transform.position;
        }
    }

    /// <summary>
    /// The amount of Points remaining in the Checkpoint
    /// </summary>
    public float PointsAmount { get; private set; }

    /// <summary>
    /// Whether the Checkpoint has any Points remaining
    /// </summary>
    public bool HasPoints
    {
        get
        {
            return PointsAmount > 0f;
        }
    }

    /// <summary>
    /// Attempts to remove Points from the Checkpoint
    /// </summary>
    /// <param name="amount">The amount of Points to remove</param>
    /// <returns>The actual amount successfully removed</returns>
    public float GainPoints(float amount)
    {
        // Track how much Points was successfully gained (cannot take more than is available)
        float PointsTaken = Mathf.Clamp(amount, 0f, PointsAmount);

        // Subtract the Points
        PointsAmount -= amount;

        if (PointsAmount <= 0)
        {
            // No Points remaining
            PointsAmount = 0;

            // Disable the Points colliders
            PointsCollider.gameObject.SetActive(false);

            // Change the Checkpoint color to indicate that it is empty
            CheckpointMaterial.SetColor("_BaseColor", emptyCheckpointColor);
        }

        // Return the amount of Points that was taken
        return PointsTaken;
    }

    /// <summary>
    /// Resets the Checkpoint
    /// </summary>
    public void ResetCheckpoint()
    {
        // Refill the Points
        PointsAmount = 1f;

        // Enable the Points colliders
        PointsCollider.gameObject.SetActive(true);

        // Change the Checkpoint color to indicate that it is full
        CheckpointMaterial.SetColor("_BaseColor", fullCheckpointColor);
    }

    /// <summary>
    /// Called when the Checkpoint wakes up
    /// </summary>
    private void Awake()
    {
        // Find the Checkpoint's mesh renderer and get the main material
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        CheckpointMaterial = meshRenderer.material;

        // Find Points colliders
        PointsCollider = transform.Find("CheckpointPointsCollider").GetComponent<Collider>();
    }
}
