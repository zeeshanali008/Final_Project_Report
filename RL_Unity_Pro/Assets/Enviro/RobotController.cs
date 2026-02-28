using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

public class RobotController : MonoBehaviour
{
    
    [SerializeField] private WheelCollider WheelColliderFrontLeft;
    [SerializeField] private WheelCollider WheelColliderFrontRight;
    [SerializeField] private WheelCollider WheelColliderBackLeft;
    [SerializeField] private WheelCollider WheelColliderBackRight;

    [SerializeField] private Transform TransformWheelFrontLeft;
    [SerializeField] private Transform TransformWheelFrontRight;
    [SerializeField] private Transform TransformWheelBackLeft;
    [SerializeField] private Transform TransformWheelBackRight;

    [SerializeField] private Transform RaycastSensorFront;
    [SerializeField] private Transform RaycastSensorLeft;
    [SerializeField] private Transform RaycastSensorRight;
    [SerializeField] private Transform EulerAnglesSensor;

  
    private float engineForce = 480f;
    private float steerLimit = 40.4f;
    private float cruiseVelocity = 5.1f;
    private float activeSteerAngle = 0f;
    private float handlingSmoothness = 10.2f;
    private float pitchAngle = 45.1f;
    private float portSideAngle = -60.5f;
    private float starboardSideAngle = 60.5f;
    private float beamLength = 10.13f;
    private float turnMultiplier = 0.8736111f;
    private float lateralImpulse = 1538.194f;
    private bool isPushEnabled = false;

    private void FixedUpdate()
    {
        ProcessAutonomousNavigation();
        SyncWheelVisuals();
    }

    private void ProcessAutonomousNavigation()
    {
        Rigidbody physicsBody = GetComponent<Rigidbody>();
        float currentMagnitude = physicsBody != null ? physicsBody.velocity.magnitude : 0;
        float focalGap;

        // Obstacle Sensing
        bool isBlockedCenter = EvaluateHazardPresence(RaycastSensorFront, 0f, out focalGap, 8f);
        bool isBlockedLeftSkew = EvaluateHazardPresence(RaycastSensorFront, -20f, out _, 6f);
        bool isBlockedRightSkew = EvaluateHazardPresence(RaycastSensorFront, 20f, out _, 6f);

        // Path Sensing
        bool hasGroundLeft = DetectSurfaceStatus(RaycastSensorLeft, portSideAngle, beamLength, pitchAngle);
        bool hasGroundRight = DetectSurfaceStatus(RaycastSensorRight, starboardSideAngle, beamLength, pitchAngle);

        float steeringRequest = 0;

        if (isBlockedCenter || isBlockedLeftSkew || isBlockedRightSkew)
        {
            if (isBlockedLeftSkew) steeringRequest = steerLimit;
            else if (isBlockedRightSkew) steeringRequest = -steerLimit;
            else steeringRequest = hasGroundLeft ? -steerLimit : steerLimit;
        }
        else if (!hasGroundLeft && hasGroundRight)
        {
            steeringRequest = steerLimit * turnMultiplier;

            if (isPushEnabled && physicsBody != null)
                physicsBody.AddForce(transform.right * lateralImpulse);
        }
        else if (!hasGroundRight && hasGroundLeft)
        {
            steeringRequest = -steerLimit * turnMultiplier;

            if (isPushEnabled && physicsBody != null)
                physicsBody.AddForce(-transform.right * lateralImpulse);
        }

        activeSteerAngle = Mathf.Lerp(activeSteerAngle, steeringRequest, Time.fixedDeltaTime * handlingSmoothness);
        WheelColliderFrontLeft.steerAngle = WheelColliderFrontRight.steerAngle = Mathf.Clamp(activeSteerAngle, -steerLimit, steerLimit);

        float calculatedTorque = (currentMagnitude < cruiseVelocity) ? engineForce : 0;
        float calculatedBrake = (currentMagnitude > cruiseVelocity + 0.2f) ? 300f : 0;

        if (isBlockedCenter && focalGap < 3f)
        {
            calculatedTorque = 0;
            calculatedBrake = 1500f;
        }

        TransmitMotorOutput(calculatedTorque);
        TransmitBrakeOutput(calculatedBrake);
    }

    private void TransmitMotorOutput(float torqueValue)
    {
        WheelColliderFrontLeft.motorTorque = WheelColliderFrontRight.motorTorque =
        WheelColliderBackLeft.motorTorque = WheelColliderBackRight.motorTorque = torqueValue;
    }

    private void TransmitBrakeOutput(float brakeValue)
    {
        WheelColliderFrontLeft.brakeTorque = WheelColliderFrontRight.brakeTorque =
        WheelColliderBackLeft.brakeTorque = WheelColliderBackRight.brakeTorque = brakeValue;
    }

    private bool DetectSurfaceStatus(Transform origin, float yaw, float reach, float dip)
    {
        RaycastHit hitInfo;
        Vector3 trajectory = Quaternion.AngleAxis(yaw, Vector3.up) * origin.forward;
        trajectory = Quaternion.AngleAxis(dip, origin.right) * trajectory;

        if (Physics.Raycast(origin.position, trajectory, out hitInfo, reach))
            return hitInfo.collider.gameObject.name.StartsWith("MT_");

        return false;
    }

    private bool EvaluateHazardPresence(Transform mount, float bias, out float distance, float span)
    {
        RaycastHit hazardHit;
        Vector3 vectorDir = Quaternion.Euler(0, bias, 0) * mount.forward;

        if (Physics.Raycast(mount.position, vectorDir, out hazardHit, span))
        {
            string tagIdentity = hazardHit.collider.gameObject.name;
            if (tagIdentity.StartsWith("CP") || tagIdentity.StartsWith("MT_"))
            {
                distance = span;
                return false;
            }

            distance = hazardHit.distance;
            return true;
        }

        distance = span;
        return false;
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
        collider.GetWorldPose(out Vector3 worldPos, out Quaternion worldRot);
        model.position = worldPos;
        model.rotation = worldRot;
    }
}