using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class ShooterAgent : Agent
{
    [Header("Prefabs and Environment")]
    public GameObject birdPrefab;
    [Tooltip("Container that moves the table and pyramid")]
    public Transform targetZone;
    [Tooltip("The parent object that contains all the targets")]
    public Transform pyramidParent;

    [Header("Cannon Components")]
    public Transform launcherBase;
    public Transform launcherBarrel;
    public Transform launchPoint;

    [Header("Shooting Physics")]
    public float maxForce = 30f;
    public float rotationSpeed = 200f;
    [Tooltip("Time to wait to see the result of its shot before resetting.")]
    public float timeToWaitAfterShot = 3.0f;

    [Header("Rotation Limits")]
    public float maxYaw = 50f;
    public float maxPitch = 11f;
    public float minPitch = -50f;

    [Header("Random Spawn Area")]
    public Vector2 spawnRangeX = new Vector2(-10.0f, 10.0f);
    public Vector2 spawnRangeZ = new Vector2(3.0f, 20.0f);

    [Header("Triggers and Reward System")]
    public RewardTrigger floorTrigger;

    // --- Internal State Variables ---
    private float currentYaw = 0f;
    private float currentPitch = 0f;
    private bool hasFired = false;
    private float waitTimer = 0f;
    private GameObject currentBird;
    private float humanPower = 0.5f;

    private Transform[] cans;
    private Vector3[] startPositions;
    private Quaternion[] startRotations;
    private Rigidbody[] canRigidbodies;



    public override void Initialize()
    {
        int canCount = pyramidParent.childCount;
        cans = new Transform[canCount];
        startPositions = new Vector3[canCount];
        startRotations = new Quaternion[canCount];
        canRigidbodies = new Rigidbody[canCount];

        for (int i = 0; i < canCount; i++)
        {
            cans[i] = pyramidParent.GetChild(i);
            startPositions[i] = cans[i].localPosition;
            startRotations[i] = cans[i].localRotation;
            canRigidbodies[i] = cans[i].GetComponent<Rigidbody>();
        }
    }



    public override void OnEpisodeBegin()
    {
        // Clean up the previous projectile
        if (currentBird != null)
        {
            Destroy(currentBird);
        }

        // Move the pyramid to a random position within the spawn area
        float randomX = Random.Range(spawnRangeX.x, spawnRangeX.y);
        float randomZ = Random.Range(spawnRangeZ.x, spawnRangeZ.y);
        targetZone.localPosition = new Vector3(randomX, targetZone.localPosition.y, randomZ);

        // Restore each pig to its original state
        for (int i = 0; i < cans.Length; i++)
        {
            cans[i].localPosition = startPositions[i];
            cans[i].localRotation = startRotations[i];

            if (canRigidbodies[i] != null)
            {
                canRigidbodies[i].velocity = Vector3.zero;
                canRigidbodies[i].angularVelocity = Vector3.zero;
            }

            Collider canCollider = cans[i].GetComponent<Collider>();
            if (canCollider != null)
            {
                canCollider.enabled = true;
            }

            cans[i].gameObject.SetActive(true);
        }

        // Reset cannon rotation
        launcherBase.localRotation = Quaternion.identity;
        launcherBarrel.localRotation = Quaternion.identity;
        currentYaw = 0f;
        currentPitch = 0f;

        // Reset trigger
        if (floorTrigger != null)
        {
            floorTrigger.ResetTrigger();
        }

        // Reset state variables
        hasFired = false;
        waitTimer = 0f;
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        // Cannon orientation (normalized)
        sensor.AddObservation(currentYaw / maxYaw);         // [-1, 1]
        sensor.AddObservation(currentPitch / maxPitch);     // [-1, 1]

        // Target relative position 
        Vector3 toTarget = targetZone.position - launchPoint.position;
        sensor.AddObservation(toTarget.normalized);        
        sensor.AddObservation(toTarget.magnitude / 70f);   
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        if (hasFired) return;

        float yawInput = actions.ContinuousActions[0];
        float pitchInput = actions.ContinuousActions[1];
        float forceInput = actions.ContinuousActions[2];

        // Apply yaw (left/right)
        currentYaw += yawInput * rotationSpeed * Time.fixedDeltaTime;
        currentYaw = Mathf.Clamp(currentYaw, -maxYaw, maxYaw);
        launcherBase.localRotation = Quaternion.Euler(0f, currentYaw, 0f);

        // Apply pitch (up/down)
        currentPitch += pitchInput * rotationSpeed * Time.fixedDeltaTime;
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        launcherBarrel.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);

        // Fire threshold
        if (forceInput > 0.5f)
        {
            Shoot(forceInput);
        }

        // Small per-step penalty to encourage quick, decisive shots
        AddReward(-0.001f);
    }

    private void Shoot(float normalizedForce)
    {
        hasFired = true;
        waitTimer = timeToWaitAfterShot;

        currentBird = Instantiate(birdPrefab, launchPoint.position, launchPoint.rotation);
        Rigidbody birdRb = currentBird.GetComponent<Rigidbody>();

        // Map force from [0.5, 1.0] to [0, maxForce]
        float actualForce = (normalizedForce - 0.5f) * 2f * maxForce;
        birdRb.AddForce(launchPoint.forward * actualForce, ForceMode.Impulse);
    }

    private void FixedUpdate()
    {
        // Count down after firing and end episode if timer expires
        if (hasFired)
        {
            waitTimer -= Time.fixedDeltaTime;
            if (waitTimer <= 0f)
            {
                EndEpisode();
            }
        }
    }


    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;

        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Input.GetAxis("Vertical");

        if (Input.GetKey(KeyCode.LeftShift)) humanPower += Time.deltaTime;
        if (Input.GetKey(KeyCode.LeftControl)) humanPower -= Time.deltaTime;
        humanPower = Mathf.Clamp(humanPower, 0.5f, 1.0f);

        if (Input.GetKey(KeyCode.Space))
        {
            continuousActions[2] = humanPower;
            Debug.Log($"Firing! Power: {humanPower}");
        }
        else
        {
            continuousActions[2] = 0f;
        }
    }



    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;

        float centerX = (spawnRangeX.x + spawnRangeX.y) / 2f;
        float centerZ = (spawnRangeZ.x + spawnRangeZ.y) / 2f;
        float tableHeight = targetZone != null ? targetZone.localPosition.y : 0f;

        Vector3 center = new Vector3(centerX, tableHeight, centerZ);
        Vector3 size = new Vector3(spawnRangeX.y - spawnRangeX.x, 0.1f, spawnRangeZ.y - spawnRangeZ.x);

        if (transform.parent != null)
        {
            Gizmos.matrix = transform.parent.localToWorldMatrix;
        }

        Gizmos.DrawWireCube(center, size);
    }
}