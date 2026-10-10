using System;
using UnityEngine;

namespace EverflowMVP
{
    // The editor rebuild command consumes the same specification as the included scene.
    [Serializable] public class EFLayout
    {
        public EFObjectSpec[] objects;
        public EFStageSpec[] stages;
    }
    [Serializable] public class EFStageSpec
    {
        public string title, hint, spawn;
        public string[] nodes, requirements;
    }
    [Serializable] public class EFObjectSpec
    {
        public string id, parent, shape, role, label, material;
        public Vector3 position, rotation, scale;
        public bool collider, trigger, kinematic, hidden;
        public int stage, kind;
        public bool acceptsPulse, requiresPower, alignmentConducts, supportOnlyWhenFrozen;
        public string supply, growthObject, handle, rideStart, rideEnd, rideVisual, destination;
        public string[] outputs;
        public Vector3 axis, launchVelocity;
        public float speed, amplitude, targetAngle, tolerance, initialAngle, ropeLength;
        public bool checkpoint;
    }
}
