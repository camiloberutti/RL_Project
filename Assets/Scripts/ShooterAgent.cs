using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class ShooterAgent : Agent
{
    [Header("Prefabs")]
    public GameObject birdPrefab;
    // ¡Adiós al pyramidPrefab! Ya no lo necesitamos.

    [Header("Environment Setup")]
    public Transform pyramidParent; // El objeto padre que contiene a todos tus cerdos/latas

    [Header("Launcher Parts")]
    public Transform launcherBase;
    public Transform launcherBarrel;
    public Transform launchPoint;

    [Header("Shooting Stats")]
    public float maxForce = 30f;
    public float rotationSpeed = 500f;

    [Header("Limites de Rotacion")]
    public float maxYaw = 30f;    // Cuánto puede girar a izquierda/derecha
    public float maxPitch = 11f;  // Cuánto puede apuntar hacia arriba
    public float minPitch = -89f; // Cuánto puede apuntar hacia abajo

    private float currentYaw = 0f;
    private float currentPitch = 0f;
    private bool hasFired = false;
    private float waitTimer = 0f;
    private float timeToWaitAfterShot = 3.0f;
    private GameObject currentBird;
    [Header("Triggers")]
    public RewardTrigger floorTrigger;
    // --- NUEVAS VARIABLES PARA MEMORIZAR LA PIRÁMIDE ---
    private Transform[] cans;
    private Vector3[] startPositions;
    private Quaternion[] startRotations;
    private Rigidbody[] canRigidbodies;

    // Initialize se ejecuta solo UNA vez cuando le das a Play
    public override void Initialize()
    {
        int canCount = pyramidParent.childCount;
        cans = new Transform[canCount];
        startPositions = new Vector3[canCount];
        startRotations = new Quaternion[canCount];
        canRigidbodies = new Rigidbody[canCount];

        // Guardamos la posición exacta, rotación y físicas de cada cerdo individual
        for (int i = 0; i < canCount; i++)
        {
            cans[i] = pyramidParent.GetChild(i);
            startPositions[i] = cans[i].localPosition; // Local, para que funcione al duplicar entornos
            startRotations[i] = cans[i].localRotation;
            canRigidbodies[i] = cans[i].GetComponent<Rigidbody>();
        }
    }

    public override void OnEpisodeBegin()
    {
        // 1. Limpiar el pájaro viejo
        if (currentBird != null)
        {
            Destroy(currentBird);
        }

        // 2. Restaurar cada cerdo
        for (int i = 0; i < cans.Length; i++)
        {
            // Volver a la posición original
            cans[i].localPosition = startPositions[i];
            cans[i].localRotation = startRotations[i];

            // Frenar cualquier movimiento que tuvieran al caer
            if (canRigidbodies[i] != null)
            {
                canRigidbodies[i].velocity = Vector3.zero;
                canRigidbodies[i].angularVelocity = Vector3.zero;
            }

            // ¡LA SOLUCIÓN! -> Volver a encender su cuerpo físico (Collider)
            Collider canCollider = cans[i].GetComponent<Collider>();
            if (canCollider != null)
            {
                canCollider.enabled = true;
            }

            // Por si acaso algún otro script apagó el objeto entero
            cans[i].gameObject.SetActive(true);
        }

        // 3. Resetear el cañón
        launcherBase.localRotation = Quaternion.identity;
        launcherBarrel.localRotation = Quaternion.identity;

        // Resetear tu trigger del piso (si conectaste la variable)
        if (floorTrigger != null)
        {
            floorTrigger.ResetTrigger();
        }

        hasFired = false;
        waitTimer = 0f;
    }
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (hasFired) return;

        float yawInput = actions.ContinuousActions[0];
        float pitchInput = actions.ContinuousActions[1];
        float forceInput = actions.ContinuousActions[2];

        // 1. Calculamos el nuevo ángulo (Izquierda/Derecha) y le ponemos el límite
        currentYaw += yawInput * rotationSpeed * Time.fixedDeltaTime;
        currentYaw = Mathf.Clamp(currentYaw, -maxYaw, maxYaw);
        // Aplicamos el ángulo exacto a la base
        launcherBase.localRotation = Quaternion.Euler(0f, currentYaw, 0f);

        // 2. Calculamos el nuevo ángulo (Arriba/Abajo) y le ponemos el límite
        currentPitch += pitchInput * rotationSpeed * Time.fixedDeltaTime;
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        // Aplicamos el ángulo exacto al cañón
        launcherBarrel.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);

        if (forceInput > 0.5f)
        {
            Shoot(forceInput);
        }

        AddReward(-0.001f);
    }

    private void Shoot(float normalizedForce)
    {
        hasFired = true;
        waitTimer = timeToWaitAfterShot;

        currentBird = Instantiate(birdPrefab, launchPoint.position, launchPoint.rotation);
        Rigidbody birdRb = currentBird.GetComponent<Rigidbody>();

        float actualForce = (normalizedForce - 0.5f) * 2f * maxForce;
        birdRb.AddForce(launchPoint.forward * actualForce, ForceMode.Impulse);
    }

    private void FixedUpdate()
    {
        if (hasFired)
        {
            waitTimer -= Time.fixedDeltaTime;
            if (waitTimer <= 0f)
            {
                EndEpisode();
            }
        }
    }

    // --- CONTROLES MANUALES ---
    private float humanPower = 0.5f;

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
            Debug.Log("Firing! Power: " + humanPower);
        }
        else
        {
            continuousActions[2] = 0f;
        }
    }

    // FOR THE AGENT

    public override void CollectObservations(VectorSensor sensor)
    {
        // Tell the AI exactly where the barrel is currently pointing
        sensor.AddObservation(currentYaw);
        sensor.AddObservation(currentPitch);
    }



}