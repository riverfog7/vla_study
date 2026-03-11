using System;
using UnityEngine;

namespace VlaStudy.UnityHarness.Data
{
    [Serializable]
    public class PoseCommand
    {
        public string frame = "world";
        public Vector3Data position = new Vector3Data();
        public QuaternionData rotation = new QuaternionData();
        public float gripper;
        public bool blocking;

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(frame) && position != null && rotation != null;
        }
    }

    [Serializable]
    public class Vector3Data
    {
        public float x;
        public float y;
        public float z;

        public Vector3Data()
        {
        }

        public Vector3Data(Vector3 value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
        }

        public Vector3 ToUnityVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    [Serializable]
    public class QuaternionData
    {
        public float x;
        public float y;
        public float z;
        public float w = 1f;

        public QuaternionData()
        {
        }

        public QuaternionData(Quaternion value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
            w = value.w;
        }

        public Quaternion ToUnityQuaternion()
        {
            return new Quaternion(x, y, z, w);
        }
    }

    [Serializable]
    public class PoseData
    {
        public Vector3Data position = new Vector3Data();
        public QuaternionData rotation = new QuaternionData();

        public PoseData()
        {
        }

        public PoseData(Vector3 positionValue, Quaternion rotationValue)
        {
            position = new Vector3Data(positionValue);
            rotation = new QuaternionData(rotationValue);
        }
    }
}
