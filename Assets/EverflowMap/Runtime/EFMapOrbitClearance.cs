using Unity.Cinemachine;
using UnityEngine;

namespace EverflowMap
{
    [DisallowMultipleComponent]
    [AddComponentMenu("EVERFLOW/Orbit Camera Clearance")]
    public sealed class EFMapOrbitClearance : CinemachineExtension
    {
        [SerializeField] LayerMask obstacles = 1;
        [SerializeField, Min(.05f)] float cameraRadius = .22f;
        [SerializeField, Min(0)] float wallPadding = .025f;
        [SerializeField, Min(0)] float edgeBuffer = .26f;
        [SerializeField, Min(0)] float nearWallRange = .95f;
        [SerializeField, Min(0)] float predictionTime = .16f;
        [SerializeField, Min(0)] float releaseHold = .20f;
        [SerializeField, Min(.01f)] float retractTime = .085f;
        [SerializeField, Min(.01f)] float extendTime = .34f;

        Vector3 previousPivot;
        float distance, velocity, holdRemaining;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Body || !state.HasLookAt())
                return;

            Vector3 pivot = state.ReferenceLookAt;
            Vector3 position = state.GetCorrectedPosition();
            Vector3 offset = position - pivot;
            float nominalDistance = offset.magnitude;
            if (nominalDistance < .01f)
                return;

            bool reset = deltaTime < 0 || !vcam.PreviousStateIsValid;
            Vector3 direction = offset / nominalDistance;
            float safeDistance = Clearance(pivot, direction, nominalDistance, cameraRadius);
            float desiredDistance = Mathf.Min(safeDistance, ComfortableDistance(pivot, direction, nominalDistance));

            if (!reset && deltaTime > .0001f)
            {
                Vector3 motion = Vector3.ClampMagnitude((pivot - previousPivot) * (predictionTime / deltaTime), 1f);
                float travel = motion.magnitude;
                if (travel > .015f)
                {
                    Vector3 alongMotion = motion / travel;
                    float forecastTravel = Clearance(pivot, alongMotion, travel, cameraRadius);
                    desiredDistance = Mathf.Min(desiredDistance,
                        ComfortableDistance(pivot + alongMotion * forecastTravel, direction, nominalDistance));
                }
            }
            previousPivot = pivot;

            if (reset)
            {
                distance = desiredDistance;
                velocity = 0;
                holdRemaining = 0;
            }
            else if (deltaTime > 0)
            {
                if (desiredDistance < distance - .02f)
                    holdRemaining = releaseHold;
                else
                    holdRemaining = Mathf.Max(0, holdRemaining - deltaTime);
                if (holdRemaining > 0)
                    desiredDistance = Mathf.Min(distance, desiredDistance);

                float smoothTime = desiredDistance < distance ? retractTime : extendTime;
                distance = Mathf.SmoothDamp(distance, desiredDistance, ref velocity, smoothTime,
                    Mathf.Infinity, deltaTime);
            }

            // Smoothing may lag a sudden obstruction; the smaller sweep is the hard limit.
            if (distance > safeDistance)
            {
                distance = safeDistance;
                velocity = Mathf.Min(0, velocity);
            }
            state.PositionCorrection += pivot + direction * distance - position;
        }

        float ComfortableDistance(Vector3 pivot, Vector3 direction, float nominalDistance)
        {
            float space = Clearance(pivot, direction, nominalDistance, cameraRadius + edgeBuffer);
            // Horizontal probes anticipate corners without treating the floor as a side wall.
            Vector3 horizontal = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, horizontal);
            float wall = Mathf.Min(Mathf.Min(WallDistance(pivot, side), WallDistance(pivot, -side)),
                Mathf.Min(WallDistance(pivot, horizontal), WallDistance(pivot, -horizontal)));
            float clearance = Mathf.InverseLerp(cameraRadius, nearWallRange, wall);
            return Mathf.Min(space, Mathf.Lerp(1f, nominalDistance, clearance));
        }

        float WallDistance(Vector3 pivot, Vector3 direction)
        {
            return Physics.Raycast(pivot, direction, out var hit, nearWallRange, obstacles, QueryTriggerInteraction.Ignore)
                ? hit.distance
                : nearWallRange;
        }

        float Clearance(Vector3 pivot, Vector3 direction, float length, float radius)
        {
            if (!Physics.SphereCast(pivot, radius, direction, out var hit, length, obstacles, QueryTriggerInteraction.Ignore))
                return length;
            return Mathf.Clamp(hit.distance - wallPadding, .01f, length);
        }
    }
}
