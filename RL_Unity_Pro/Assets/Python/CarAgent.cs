using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

/// <summary>
/// CarAgent - ML-Agents script for SAC/PPO training
/// Ye script RobotController ke saath kaam karti hai
/// Car ko obstacles avoid karke track par chalne ki training deta hai
/// </summary>
public class CarAgent : Agent
{
    // =============================================
    // Inspector Variables - Unity mein assign karo
    // =============================================
    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider WheelColliderFrontLeft;
    [SerializeField] private WheelCollider WheelColliderFrontRight;
    [SerializeField] private WheelCollider WheelColliderBackLeft;
    [SerializeField] private WheelCollider WheelColliderBackRight;

    [Header("Wheel Transforms")]
    [SerializeField] private Transform TransformWheelFrontLeft;
    [SerializeField] private Transform TransformWheelFrontRight;
    [SerializeField] private Transform TransformWheelBackLeft;
    [SerializeField] private Transform TransformWheelBackRight;

    [Header("Sensors")]
    [SerializeField] private Transform RaycastSensorFront;
    [SerializeField] private Transform RaycastSensorLeft;
    [SerializeField] private Transform RaycastSensorRight;

    [Header("Checkpoints")]
    [SerializeField] private Transform[] checkpoints;  // Track pe checkpoints lagao
    [SerializeField] private Transform startPosition;  // Car ki starting position

    [Header("Car Settings")]
    [SerializeField] private float engineForce = 480f;
    [SerializeField] private float steerLimit = 40.4f;
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float raycastDistance = 10f;

    // =============================================
    // Private Variables
    // =============================================
    private Rigidbody rb;
    private float activeSteerAngle = 0f;
    private int currentCheckpoint = 0;
    private float episodeTimer = 0f;
    private float maxEpisodeTime = 60f;  // 60 second mein track complete karo
    private Vector3 startPos;
    private Quaternion startRot;
    private float lastDistanceToCheckpoint;
    private int totalCheckpointsPassed = 0;

    // =============================================
    // Unity Functions
    // =============================================
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (startPosition != null)
        {
            startPos = startPosition.position;
            startRot = startPosition.rotation;
        }
        else
        {
            startPos = transform.position;
            startRot = transform.rotation;
        }
    }

    // =============================================
    // ML-Agents Override Functions
    // =============================================

    /// <summary>
    /// Har episode shuru hone pe call hota hai
    /// Car ko reset karta hai
    /// </summary>
    public override void OnEpisodeBegin()
    {
        // Car reset karo
        transform.position = startPos + new Vector3(Random.Range(-0.5f, 0.5f), 0, 0);
        transform.rotation = startRot;

        // Physics reset karo
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Variables reset karo
        activeSteerAngle = 0f;
        currentCheckpoint = 0;
        episodeTimer = 0f;
        totalCheckpointsPassed = 0;

        // Wheels brake reset
        WheelColliderFrontLeft.brakeTorque = 0;
        WheelColliderFrontRight.brakeTorque = 0;
        WheelColliderBackLeft.brakeTorque = 0;
        WheelColliderBackRight.brakeTorque = 0;

        // Pehle checkpoint ki distance
        if (checkpoints != null && checkpoints.Length > 0)
            lastDistanceToCheckpoint = Vector3.Distance(transform.position, checkpoints[0].position);
    }

    /// <summary>
    /// Observations — Agent ko kya dikhe ga (input data)
    /// Total: 14 observations
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. Car ki speed (1)
        sensor.AddObservation(rb.velocity.magnitude / maxSpeed);

        // 2. Car ka forward direction (3)
        sensor.AddObservation(transform.forward);

        // 3. Car ki position normalized (3)
        sensor.AddObservation(rb.velocity.normalized);

        // 4. RayCast distances - obstacles kitni door hain (5)
        sensor.AddObservation(GetRaycastDistance(RaycastSensorFront, 0f));      // seedha aage
        sensor.AddObservation(GetRaycastDistance(RaycastSensorFront, -30f));    // thoda baayen
        sensor.AddObservation(GetRaycastDistance(RaycastSensorFront, 30f));     // thoda dayen
        sensor.AddObservation(GetRaycastDistance(RaycastSensorLeft, -60f));     // zyada baayen
        sensor.AddObservation(GetRaycastDistance(RaycastSensorRight, 60f));     // zyada dayen

        // 5. Agle checkpoint ki direction (2)
        if (checkpoints != null && checkpoints.Length > 0 && currentCheckpoint < checkpoints.Length)
        {
            Vector3 dirToCheckpoint = (checkpoints[currentCheckpoint].position - transform.position).normalized;
            sensor.AddObservation(Vector3.Dot(transform.forward, dirToCheckpoint));   // aage hai ya peeche
            sensor.AddObservation(Vector3.Dot(transform.right, dirToCheckpoint));     // left hai ya right
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    /// <summary>
    /// Actions — Agent kya kare ga (output)
    /// 2 continuous actions: throttle + steering
    /// </summary>
    public override void OnActionReceived(ActionBuffers actions)
    {
        episodeTimer += Time.fixedDeltaTime;

        // Actions le lo
        float throttle = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);  // -1=brake, 1=accelerate
        float steering = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);  // -1=left, 1=right

        // Steering apply karo
        float targetSteer = steering * steerLimit;
        activeSteerAngle = Mathf.Lerp(activeSteerAngle, targetSteer, Time.fixedDeltaTime * 10f);
        WheelColliderFrontLeft.steerAngle = WheelColliderFrontRight.steerAngle = activeSteerAngle;

        // Throttle/Brake apply karo
        if (throttle > 0)
        {
            float torque = throttle * engineForce;
            WheelColliderFrontLeft.motorTorque = WheelColliderFrontRight.motorTorque =
            WheelColliderBackLeft.motorTorque = WheelColliderBackRight.motorTorque = torque;
            WheelColliderFrontLeft.brakeTorque = WheelColliderFrontRight.brakeTorque =
            WheelColliderBackLeft.brakeTorque = WheelColliderBackRight.brakeTorque = 0;
        }
        else
        {
            WheelColliderFrontLeft.motorTorque = WheelColliderFrontRight.motorTorque =
            WheelColliderBackLeft.motorTorque = WheelColliderBackRight.motorTorque = 0;
            float brakeForce = Mathf.Abs(throttle) * 500f;
            WheelColliderFrontLeft.brakeTorque = WheelColliderFrontRight.brakeTorque =
            WheelColliderBackLeft.brakeTorque = WheelColliderBackRight.brakeTorque = brakeForce;
        }

        // Wheel visuals sync karo
        SyncWheelVisuals();

        // =============================================
        // REWARD SYSTEM
        // =============================================

        // 1. Speed reward — teez chalao toh reward
        float speed = rb.velocity.magnitude;
        AddReward(speed * 0.001f);

        // 2. Checkpoint ki taraf jaane pe reward
        if (checkpoints != null && checkpoints.Length > 0 && currentCheckpoint < checkpoints.Length)
        {
            float distToCheckpoint = Vector3.Distance(transform.position, checkpoints[currentCheckpoint].position);
            float distanceDelta = lastDistanceToCheckpoint - distToCheckpoint;
            AddReward(distanceDelta * 0.05f);  // checkpoint ki taraf jaa raha hai toh +reward
            lastDistanceToCheckpoint = distToCheckpoint;
        }

        // 3. Time penalty — jaldi khatam karo
        AddReward(-0.001f);

        // 4. Episode timeout
        if (episodeTimer >= maxEpisodeTime)
        {
            AddReward(-0.5f);
            EndEpisode();
        }
    }

    /// <summary>
    /// Manual control — testing ke liye keyboard se chalao
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Vertical");    // W/S keys
        continuousActions[1] = Input.GetAxis("Horizontal");  // A/D keys
    }

    // =============================================
    // Collision Detection
    // =============================================
    private void OnCollisionEnter(Collision collision)
    {
        // Obstacle se takraya
        if (collision.gameObject.layer == LayerMask.NameToLayer("Obs"))
        {
            AddReward(-1.0f);   // Bari penalty
            EndEpisode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Checkpoint pass kiya
        if (other.CompareTag("Checkpoint"))
        {
            int checkpointIndex = other.GetComponent<CheckpointScript>()?.index ?? 0;
            if (checkpointIndex == currentCheckpoint)
            {
                AddReward(1.0f);    // Checkpoint reward
                currentCheckpoint++;
                totalCheckpointsPassed++;

                // Saare checkpoints pass kar liye = episode complete
                if (currentCheckpoint >= checkpoints.Length)
                {
                    AddReward(5.0f);   // Lap complete reward!
                    EndEpisode();
                }
                else if (checkpoints != null && currentCheckpoint < checkpoints.Length)
                {
                    lastDistanceToCheckpoint = Vector3.Distance(
                        transform.position,
                        checkpoints[currentCheckpoint].position
                    );
                }
            }
        }

        // Track se bahar gaya
       /* if (other.CompareTag("OutOfBounds"))
        {
            AddReward(-2.0f);
            EndEpisode();
        }*/
    }

    // =============================================
    // Helper Functions
    // =============================================

    /// <summary>
    /// RayCast distance normalized (0=obstacle paas, 1=door ya kuch nahi)
    /// </summary>
    private float GetRaycastDistance(Transform sensor, float angle)
    {
        if (sensor == null) return 1f;

        RaycastHit hit;
        Vector3 direction = Quaternion.Euler(0, angle, 0) * sensor.forward;

        if (Physics.Raycast(sensor.position, direction, out hit, raycastDistance))
        {
            // Debug line - Scene view mein dikhega
            Debug.DrawRay(sensor.position, direction * hit.distance, Color.red);
            return hit.distance / raycastDistance;  // 0 to 1 normalized
        }

        Debug.DrawRay(sensor.position, direction * raycastDistance, Color.green);
        return 1f;  // Kuch nahi mila
    }

    private void SyncWheelVisuals()
    {
        UpdateWheelPositioning(WheelColliderFrontLeft, TransformWheelFrontLeft);
        UpdateWheelPositioning(WheelColliderFrontRight, TransformWheelFrontRight);
        UpdateWheelPositioning(WheelColliderBackLeft, TransformWheelBackLeft);
        UpdateWheelPositioning(WheelColliderBackRight, TransformWheelBackRight);
    }

    private void UpdateWheelPositioning(WheelCollider collider, Transform model)
    {
        if (collider == null || model == null) return;
        collider.GetWorldPose(out Vector3 worldPos, out Quaternion worldRot);
        model.position = worldPos;
        model.rotation = worldRot;
    }
}
