/*using System;
using UnityEngine;

public class RobotController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider WheelColliderFrontLeft;
    [SerializeField] private WheelCollider WheelColliderFrontRight;
    [SerializeField] private WheelCollider WheelColliderBackLeft;
    [SerializeField] private WheelCollider WheelColliderBackRight;

    [Header("Wheel Visuals")]
    [SerializeField] private Transform TransformWheelFrontLeft;
    [SerializeField] private Transform TransformWheelFrontRight;
    [SerializeField] private Transform TransformWheelBackLeft;
    [SerializeField] private Transform TransformWheelBackRight;

    [Header("Sensors")]
    [SerializeField] private Transform RaycastSensorFront;
    [SerializeField] private Transform RaycastSensorLeft;
    [SerializeField] private Transform RaycastSensorRight;

    // --- NAVIGATION SETTINGS ---
    private float motorTorque = 120f;
    private float maxSteerAngle = 40f;
    public float targetSpeed = 0.01f;

    private float currentSteerAngle = 0f;
    private float steeringSmoothness = 10f;

    // --- CONSOLE ADJUSTABLE RAYCAST PARAMETERS ---
    [Header("Road Detection Settings (Runtime Adjustable)")]
    [SerializeField] private float raycastDownAngle = 80f;        // Neeche ki taraf tilt angle
    [SerializeField] private float raycastSideAngleLeft = -30f;   // Left sensor ka side angle
    [SerializeField] private float raycastSideAngleRight = 30f;   // Right sensor ka side angle
    [SerializeField] private float raycastRange = 5f;             // Raycast ki range
    [SerializeField] private float boundarySteerStrength = 0.8f;  // Boundary se bachne ki force

    [Header("Correction Force Settings")]
    [SerializeField] private float correctionForce = 500f;        // Side force ki strength
    [SerializeField] private bool useDirectForce = false;         // Direct force ya steering?

    private void FixedUpdate()
    {
        NavigateWithRoadDetection();
        UpdateWheelTransforms();
    }

    private void NavigateWithRoadDetection()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        float currentSpeed = rb != null ? rb.velocity.magnitude : 0;

        // 1. OBSTACLE DETECTION
        float fDist;
        bool frontHit = GetObstacleData(RaycastSensorFront, 0f, out fDist, 8f);
        bool leftFrontHit = GetObstacleData(RaycastSensorFront, -20f, out _, 6f);
        bool rightFrontHit = GetObstacleData(RaycastSensorFront, 20f, out _, 6f);

        // 2. ROAD EDGE DETECTION (Ab Console se adjust ho sakta hai)
        bool isRoadBelowLeft = CheckRoadBelow(RaycastSensorLeft, raycastSideAngleLeft, raycastRange, raycastDownAngle);
        bool isRoadBelowRight = CheckRoadBelow(RaycastSensorRight, raycastSideAngleRight, raycastRange, raycastDownAngle);

        float desiredSteer = 0;

        // --- STEERING DECISION ---
        if (frontHit || leftFrontHit || rightFrontHit)
        {
            if (leftFrontHit) desiredSteer = maxSteerAngle;
            else if (rightFrontHit) desiredSteer = -maxSteerAngle;
            else desiredSteer = isRoadBelowLeft ? -maxSteerAngle : maxSteerAngle;
            currentSteerAngle = desiredSteer;
        }
        else if (!isRoadBelowLeft && isRoadBelowRight)
        {
            // Left boundary cross ho rahi hai - RIGHT mudo
            desiredSteer = maxSteerAngle * boundarySteerStrength;

            // Optional: Direct force bhi apply kar sakte hain
            if (useDirectForce && rb != null)
            {
                rb.AddForce(transform.right * correctionForce);
                Debug.Log("LEFT BOUNDARY! Applying RIGHT force");
            }
        }
        else if (!isRoadBelowRight && isRoadBelowLeft)
        {
            // Right boundary cross ho rahi hai - LEFT mudo
            desiredSteer = -maxSteerAngle * boundarySteerStrength;

            if (useDirectForce && rb != null)
            {
                rb.AddForce(-transform.right * correctionForce);
                Debug.Log("RIGHT BOUNDARY! Applying LEFT force");
            }
        }
        else if (!isRoadBelowLeft && !isRoadBelowRight)
        {
            // Dono sides se road nahi - Emergency!
            desiredSteer = 0;
            Debug.LogWarning("Both sensors off road!");
        }
        else
        {
            // Road pe hain, center mein chalo
            desiredSteer = 0;
        }

        // Apply Steering
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, desiredSteer, Time.fixedDeltaTime * steeringSmoothness);
        WheelColliderFrontLeft.steerAngle = WheelColliderFrontRight.steerAngle = Mathf.Clamp(currentSteerAngle, -maxSteerAngle, maxSteerAngle);

        // --- SPEED & BRAKING ---
        float torque = (currentSpeed < targetSpeed) ? motorTorque : 0;
        float brake = (currentSpeed > targetSpeed + 0.2f) ? 300f : 0;
        if (frontHit && fDist < 3f) { torque = 0; brake = 1500f; }

        ApplyTorque(torque);
        ApplyBraking(brake);
    }

    // Updated function with adjustable parameters
    private bool CheckRoadBelow(Transform sensor, float sideAngle, float range, float downAngle)
    {
        RaycastHit hit;

        // 1. Forward direction
        Vector3 forwardDir = sensor.forward;

        // 2. Side rotation (console se adjust)
        Quaternion sideRotation = Quaternion.Euler(0, sideAngle, 0);
        Vector3 sideDir = sideRotation * forwardDir;

        // 3. Downward tilt (console se adjust)
        Vector3 finalDir = Quaternion.AngleAxis(downAngle, sensor.right) * sideDir;

        if (Physics.Raycast(sensor.position, finalDir, out hit, range))
        {
            // Road detected - Blue line
            Debug.DrawLine(sensor.position, hit.point, Color.blue);

            // Ye bhi check karo ke road tag hai ya nahi
            if (hit.collider.CompareTag("Road"))
            {
                return true;
            }
        }

        // No road detected - Yellow line
        Debug.DrawRay(sensor.position, finalDir * range, Color.yellow);
        return false;
    }

    private bool GetObstacleData(Transform sensor, float angle, out float dist, float range)
    {
        RaycastHit hit;
        Vector3 dir = Quaternion.Euler(0, angle, 0) * sensor.forward;
        if (Physics.Raycast(sensor.position, dir, out hit, range))
        {
            dist = hit.distance;
            Debug.DrawLine(sensor.position, hit.point, Color.red);
            return true;
        }
        dist = range;
        return false;
    }

    private void ApplyTorque(float t)
    {
        WheelColliderFrontLeft.motorTorque = WheelColliderFrontRight.motorTorque = t;
        WheelColliderBackLeft.motorTorque = WheelColliderBackRight.motorTorque = t;
    }

    private void ApplyBraking(float b)
    {
        WheelColliderFrontLeft.brakeTorque = WheelColliderFrontRight.brakeTorque = b;
        WheelColliderBackLeft.brakeTorque = WheelColliderBackRight.brakeTorque = b;
    }

    private void UpdateWheelTransforms()
    {
        UpdateWheelVisual(WheelColliderFrontLeft, TransformWheelFrontLeft);
        UpdateWheelVisual(WheelColliderFrontRight, TransformWheelFrontRight);
        UpdateWheelVisual(WheelColliderBackLeft, TransformWheelBackLeft);
        UpdateWheelVisual(WheelColliderBackRight, TransformWheelBackRight);
    }

    private void UpdateWheelVisual(WheelCollider col, Transform trans)
    {
        Vector3 pos; Quaternion rot;
        col.GetWorldPose(out pos, out rot);
        trans.position = pos; trans.rotation = rot;
    }

    // --- CONSOLE COMMANDS FOR RUNTIME ADJUSTMENT ---
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 400));
        GUILayout.Label("=== Road Detection Settings ===");

        GUILayout.Label($"Down Angle: {raycastDownAngle:F1}");
        raycastDownAngle = GUILayout.HorizontalSlider(raycastDownAngle, 45f, 90f);

        GUILayout.Label($"Left Side Angle: {raycastSideAngleLeft:F1}");
        raycastSideAngleLeft = GUILayout.HorizontalSlider(raycastSideAngleLeft, -60f, 0f);

        GUILayout.Label($"Right Side Angle: {raycastSideAngleRight:F1}");
        raycastSideAngleRight = GUILayout.HorizontalSlider(raycastSideAngleRight, 0f, 60f);

        GUILayout.Label($"Range: {raycastRange:F1}");
        raycastRange = GUILayout.HorizontalSlider(raycastRange, 2f, 10f);

        GUILayout.Label($"Steer Strength: {boundarySteerStrength:F2}");
        boundarySteerStrength = GUILayout.HorizontalSlider(boundarySteerStrength, 0.3f, 1f);

        GUILayout.Label($"Correction Force: {correctionForce:F0}");
        correctionForce = GUILayout.HorizontalSlider(correctionForce, 100f, 2000f);

        useDirectForce = GUILayout.Toggle(useDirectForce, "Use Direct Force");

        GUILayout.EndArea();
    }
}*/




////// 100 % Prefect 
/*
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

public class RobotController : MonoBehaviour
{
    // Wheel Colliders
    [SerializeField] private WheelCollider WheelColliderFrontLeft;
    [SerializeField] private WheelCollider WheelColliderFrontRight;
    [SerializeField] private WheelCollider WheelColliderBackLeft;
    [SerializeField] private WheelCollider WheelColliderBackRight;

    // Wheel Visual Transforms
    [SerializeField] private Transform TransformWheelFrontLeft;
    [SerializeField] private Transform TransformWheelFrontRight;
    [SerializeField] private Transform TransformWheelBackLeft;
    [SerializeField] private Transform TransformWheelBackRight;

    // Sensors
    [SerializeField] private Transform RaycastSensorFront;
    [SerializeField] private Transform RaycastSensorLeft;
    [SerializeField] private Transform RaycastSensorRight;
    [SerializeField] private Transform EulerAnglesSensor;

    // Tuning Parameters
    float howMuchToTurn = 37.5f;
    float steeringSmoothing = 0.41f;
    bool useOrientationCorrection = true;
    float alignmentSensitivity = 0.63f;
    float maxOrientationCorrection = 18.2f;
    float howFarToLookForRoad = 12.8f;
    float angleLookingDown = 40f;
    float howAggressivelyToCorrect = 0.98f;
    bool shouldPushPhysically = false;
    float physicalPushStrength = 1840f;

    // Internal State Variables
    Rigidbody rb;
    float wheelAngle = 0.2f;
    float smoothAngle = 0.1f;
    float speedNow = 0f;
    float roll = 0f;
    float pitch = 0f;
    bool isTilt = false;

    // Step 2 - Make robot move by itself
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Step 4 - Setup velocity sensor and call functions
    void FixedUpdate()
    {
        // Update speed from rigidbody velocity
        if (rb != null)
            speedNow = rb.velocity.magnitude;

        // Step 3 - Change sensors (tilt checking)
        CheckSensors();

        // Step 9 - Stay on road
        StayOnRoad();

        // Step 10 - Adjust motor torque based on speed
        AdjustSpeedBasedOnConditions();

        // Step 5 - Deliver power to wheels
        ApplyMotorHandler();

        // Step 11 - Avoid obstacles
        AvoidObstacles();

        // Step 7 - Handle steering
        HandleSteering();

        // Step 6 - Update wheel visuals
        UpdateWheels();
    }

    // Step 3 - Function to facilitate sensor changes
    void CheckSensors()
    {
        if (EulerAnglesSensor == null) return;

        Vector3 e = EulerAnglesSensor.eulerAngles;

        roll = e.z;
        pitch = e.x;

        // Normalize angles to -180 to 180 range
        if (roll > 180) roll -= 360;
        if (roll < -180) roll += 360;

        if (pitch > 180) pitch -= 360;
        if (pitch < -180) pitch += 360;

        // Check if robot is tilted
        isTilt = Mathf.Abs(roll) > 6.2f || Mathf.Abs(pitch) > 4.8f;
    }

    // Step 5 - Function to deliver power to wheels
    void ApplyMotorHandler()
    {
        // This is now handled in AdjustSpeedBasedOnConditions
        // Keeping this as a placeholder for the structure
    }

    // Step 6 - Update wheel visuals
    void UpdateWheels()
    {
        SyncWheelTransform(WheelColliderFrontLeft, TransformWheelFrontLeft);
        SyncWheelTransform(WheelColliderFrontRight, TransformWheelFrontRight);
        SyncWheelTransform(WheelColliderBackLeft, TransformWheelBackLeft);
        SyncWheelTransform(WheelColliderBackRight, TransformWheelBackRight);
    }

    void SyncWheelTransform(WheelCollider collider, Transform wheelTransform)
    {
        if (wheelTransform == null) return;

        collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
        wheelTransform.position = position;
        wheelTransform.rotation = rotation;
    }

    // Step 7 - Handle steering
    void HandleSteering()
    {
        // Smoothly interpolate wheel angle
        smoothAngle = Mathf.Lerp(smoothAngle, wheelAngle, steeringSmoothing);

        // Apply steering to front wheels
        WheelColliderFrontLeft.steerAngle = smoothAngle;
        WheelColliderFrontRight.steerAngle = smoothAngle;
    }

    // Step 8 - Operate the sensors
    RoadSituation OperateSensors()
    {
        RoadSituation situation = new RoadSituation();

        // Check for obstacles
        situation.somethingInFront = CheckForObstacle(RaycastSensorFront, 0f, 14.3f, out situation.howCloseItIs);
        situation.somethingOnLeft = CheckForObstacle(RaycastSensorFront, -17f, 9.2f, out _);
        situation.somethingOnRight = CheckForObstacle(RaycastSensorFront, 22f, 11.1f, out _);

        // Check for road
        situation.roadUnderLeft = CheckForRoad(RaycastSensorLeft, -68.2f);
        situation.roadUnderRight = CheckForRoad(RaycastSensorRight, 68.2f);

        // Add tilt information
        situation.isRobotTilted = isTilt;
        situation.rollAngle = roll;

        return situation;
    }

    bool CheckForObstacle(Transform sensor, float angleY, float distance, out float hitDistance)
    {
        Vector3 direction = Quaternion.Euler(0, angleY, 0) * sensor.forward;

        if (Physics.Raycast(sensor.position, direction, out RaycastHit hit, distance))
        {
            hitDistance = hit.distance;

            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("CP"))
            {
                Debug.Log("Obstacle");
                return false;
            }
            if (hit.collider is BoxCollider)
            {
                return true;
            }
            if (hit.collider is MeshCollider)
            {
                return false;
            }

            // Agar koi aur collider type hai (CapsuleCollider, SphereCollider, etc.)
            // To use bhi obstacle consider karen
            return true;
        }

        hitDistance = distance;
        return false;
    }

    bool CheckForRoad(Transform sensor, float angleY)
    {
        Vector3 direction = Quaternion.Euler(0, angleY, 0) * sensor.forward;
        direction = Quaternion.AngleAxis(angleLookingDown, sensor.right) * direction;

        if (Physics.Raycast(sensor.position, direction, out RaycastHit hit, howFarToLookForRoad))
        {
            // Agar MeshCollider hai to road hai
            if (hit.collider is MeshCollider)
            {
                return true;
            }

            // Agar BoxCollider ya koi aur collider hai to road nahi
            return false;
        }

        return false;
    }

    // Step 9 - Use sensors to stay on road
    void StayOnRoad()
    {
        RoadSituation situation = OperateSensors();

        float targetSteerAngle = CalculateSteeringForRoad(situation);

        // Apply orientation correction if enabled
        if (useOrientationCorrection)
        {
            float correction = -roll * alignmentSensitivity;
            correction = Mathf.Clamp(correction, -maxOrientationCorrection, maxOrientationCorrection);
            targetSteerAngle += correction;
        }

        // Update wheel angle with smoothing
        wheelAngle = Mathf.Lerp(wheelAngle, targetSteerAngle, Time.fixedDeltaTime * 8.6f);
        wheelAngle = Mathf.Clamp(wheelAngle, -howMuchToTurn, howMuchToTurn);
    }

    float CalculateSteeringForRoad(RoadSituation situation)
    {
        float steer = 0f;

        // If there are no obstacles, focus on staying on road
        if (!situation.HasObstacles())
        {
            if (!situation.roadUnderLeft && situation.roadUnderRight)
            {
                steer = howMuchToTurn * howAggressivelyToCorrect;
                if (shouldPushPhysically) ApplyPhysicalPush(Vector3.right);
            }
            else if (!situation.roadUnderRight && situation.roadUnderLeft)
            {
                steer = -howMuchToTurn * howAggressivelyToCorrect;
                if (shouldPushPhysically) ApplyPhysicalPush(Vector3.left);
            }
        }

        // Reduce steering effectiveness when tilted
        if (situation.isRobotTilted)
        {
            steer *= 0.72f;
        }

        return steer;
    }

    // Step 10 - Adjust motor torque based on speed
    void AdjustSpeedBasedOnConditions()
    {
        RoadSituation situation = OperateSensors();

        float motorPower = 0f;
        float brakePower = 0f;

        // Adjust target speed based on tilt
        float speedModifier = situation.isRobotTilted ? 0.77f : 1f;
        float targetSpeed = 3f * speedModifier;

        // Accelerate if below target speed
        if (speedNow < targetSpeed)
        {
            motorPower = 465f * speedModifier;
        }
        // Brake if significantly above target speed
        else if (speedNow > targetSpeed + 0.18f)
        {
            brakePower = 280f;
        }

        // Emergency brake for close obstacles
        if (situation.somethingInFront && situation.howCloseItIs < 3.4f)
        {
            motorPower = 0f;
            brakePower = 1650f;
        }

        // Apply power and brakes to all wheels
        ApplyPowerToAllWheels(motorPower);
        ApplyBrakesToAllWheels(brakePower);
    }

    void ApplyPowerToAllWheels(float power)
    {
        WheelColliderFrontLeft.motorTorque = power;
        WheelColliderFrontRight.motorTorque = power;
        WheelColliderBackLeft.motorTorque = power;
        WheelColliderBackRight.motorTorque = power;
    }

    void ApplyBrakesToAllWheels(float brake)
    {
        WheelColliderFrontLeft.brakeTorque = brake;
        WheelColliderFrontRight.brakeTorque = brake;
        WheelColliderBackLeft.brakeTorque = brake;
        WheelColliderBackRight.brakeTorque = brake;
    }

    // Step 11 - Avoid obstacles
    void AvoidObstacles()
    {
        RoadSituation situation = OperateSensors();

        if (situation.HasObstacles())
        {
            float avoidanceSteer = 0f;

            if (situation.somethingOnLeft)
            {
                avoidanceSteer = howMuchToTurn; // Turn right
            }
            else if (situation.somethingOnRight)
            {
                avoidanceSteer = -howMuchToTurn; // Turn left
            }
            else if (situation.somethingInFront)
            {
                // Choose direction based on road availability
                avoidanceSteer = situation.roadUnderLeft ? -howMuchToTurn : howMuchToTurn;
            }

            // Update wheel angle for obstacle avoidance
            wheelAngle = Mathf.Lerp(wheelAngle, avoidanceSteer, Time.fixedDeltaTime * 8.6f);
            wheelAngle = Mathf.Clamp(wheelAngle, -howMuchToTurn, howMuchToTurn);
        }
    }

    void ApplyPhysicalPush(Vector3 localDirection)
    {
        if (rb != null)
        {
            Vector3 worldDirection = transform.TransformDirection(localDirection);
            rb.AddForce(worldDirection * physicalPushStrength);
        }
    }
}

// Helper class to store road and obstacle situation
class RoadSituation
{
    public bool somethingInFront;
    public bool somethingOnLeft;
    public bool somethingOnRight;
    public float howCloseItIs;

    public bool roadUnderLeft;
    public bool roadUnderRight;

    public bool isRobotTilted;
    public float rollAngle;

    public bool HasObstacles()
    {
        return somethingInFront || somethingOnLeft || somethingOnRight;
    }
} */



//////
/* using System;
using System.Collections.Generic;
using UnityEngine;

public class RobotController : MonoBehaviour
{
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

    [Header("Drive Settings")]
    [SerializeField] private float maxMotorTorque = 400f;
    [SerializeField] private float maxSteerAngle = 40f;
    [SerializeField] private float targetSpeed = 3.0f;
    [SerializeField] private float steeringSmoothness = 10f;

    [Header("Navigation Parameters")]
    [SerializeField] private float roadDetectionAngle = 30f; // 30 degree se kam slope = Road
    [SerializeField] private float rayRange = 10f;
    [SerializeField] private float boundarySteerStrength = 0.85f;

    private Rigidbody rb;
    private float currentSteerAngle = 0f;
    private float avoidTimer = 0f;
    private float avoidDir = 0f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.centerOfMass = Vector3.zero;
    }

    private void FixedUpdate()
    {
        HandleNavigation();
        UpdateWheelTransforms();
    }

    private void HandleNavigation()
    {
        // 1. Front Obstacle Check (Vertical Surfaces)
        float fDist;
        bool frontHit = IsObstacleAhead(RaycastSensorFront, 0f, out fDist, 8f);
        bool leftFrontHit = IsObstacleAhead(RaycastSensorFront, -25f, out _, 6f);
        bool rightFrontHit = IsObstacleAhead(RaycastSensorFront, 25f, out _, 6f);

        // 2. Road Detection (Flat Surfaces)
        bool roadLeft = IsRoadBelow(RaycastSensorLeft, -60f);
        bool roadRight = IsRoadBelow(RaycastSensorRight, 60f);

        float desiredSteer = 0;

        // --- Priority 1: Obstacle Avoidance ---
        if (frontHit || leftFrontHit || rightFrontHit)
        {
            if (leftFrontHit) avoidDir = 1f; // Steer Right
            else if (rightFrontHit) avoidDir = -1f; // Steer Left
            else avoidDir = roadLeft ? -1f : 1f;

            desiredSteer = avoidDir * maxSteerAngle;
            avoidTimer = 0.5f;
        }
        else if (avoidTimer > 0)
        {
            avoidTimer -= Time.fixedDeltaTime;
            desiredSteer = avoidDir * maxSteerAngle;
        }
        // --- Priority 2: Road Following ---
        else
        {
            if (!roadLeft && roadRight) desiredSteer = maxSteerAngle * boundarySteerStrength;
            else if (!roadRight && roadLeft) desiredSteer = -maxSteerAngle * boundarySteerStrength;
        }

        // Apply Steering
        currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, desiredSteer, steeringSmoothness * maxSteerAngle * Time.fixedDeltaTime);
        WheelColliderFrontLeft.steerAngle = WheelColliderFrontRight.steerAngle = currentSteerAngle;

        // Motor & Braking
        float speed = rb.velocity.magnitude;
        float torque = (speed < targetSpeed) ? maxMotorTorque : 0;
        float brake = (frontHit && fDist < 3f) ? 2000f : (speed > targetSpeed + 0.5f ? 400f : 0);

        ApplyTorque(torque);
        ApplyBraking(brake);
    }

    // Naya Method: Surface ke angle se pehchanna ke ye Obstacle hai ya nahi
    private bool IsObstacleAhead(Transform sensor, float angleOffset, out float distance, float range)
    {
        distance = range;
        Vector3 dir = Quaternion.Euler(0, angleOffset, 0) * sensor.forward;

        if (Physics.Raycast(sensor.position, dir, out RaycastHit hit, range))
        {
            // Agar surface khadi (Vertical) hai, to ye obstacle hai
            float surfaceAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (surfaceAngle > roadDetectionAngle)
            {
                distance = hit.distance;
                return true;
            }
        }
        return false;
    }

    // Naya Method: Surface ke angle se pehchanna ke ye Road hai ya nahi
    private bool IsRoadBelow(Transform sensor, float sideAngle)
    {
        Vector3 dir = Quaternion.AngleAxis(45f, sensor.right) * (Quaternion.Euler(0, sideAngle, 0) * sensor.forward);

        if (Physics.Raycast(sensor.position, dir, out RaycastHit hit, rayRange))
        {
            // Agar surface flat hai (Road), to angle kam hoga
            float surfaceAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (surfaceAngle < roadDetectionAngle) return true;
        }
        return false;
    }

    private void ApplyTorque(float t)
    {
        WheelColliderBackLeft.motorTorque = WheelColliderBackRight.motorTorque = t;
    }

    private void ApplyBraking(float b)
    {
        WheelColliderFrontLeft.brakeTorque = WheelColliderFrontRight.brakeTorque = b;
        WheelColliderBackLeft.brakeTorque = WheelColliderBackRight.brakeTorque = b;
    }

    private void UpdateWheelTransforms()
    {
        UpdateWheelVisual(WheelColliderFrontLeft, TransformWheelFrontLeft);
        UpdateWheelVisual(WheelColliderFrontRight, TransformWheelFrontRight);
        UpdateWheelVisual(WheelColliderBackLeft, TransformWheelBackLeft);
        UpdateWheelVisual(WheelColliderBackRight, TransformWheelBackRight);
    }

    private void UpdateWheelVisual(WheelCollider col, Transform trans)
    {
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        trans.position = pos; trans.rotation = rot;
    }
}*/