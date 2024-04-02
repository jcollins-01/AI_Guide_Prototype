using System.Text;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEditor;
using static TrackedDataSource;
using System.Collections;

public class TrackedDataSource : Tracker
{
    [System.Flags]
    public enum logtype
    {
        //Position = 1,
        //Position_X = 2,
        //Position_Y = 4,
        //Position_Z = 8,
        //Rotation = 16,
        //Rotation_X = 32,
        //Rotation_Y = 64,
        //Rotation_Z = 128,
        //Scale = 256,
        //Scale_X = 512,
        //Scale_Y = 1024,
        //Scale_Z = 2048,
        //State = 4096,
        Position = 1,
        Rotation = 2,
        Scale = 4,
        State = 8,
        Vision = 16,
        VRRig = 32,
        Gaze = 64,
    }

    public enum logmethod
    {
        discrete,
        continuous,
    }

    [SerializeField] public bool trackingActive = true;

    public Transform head = null;
    public Transform headAnchor = null;
    public Transform leftHand = null;
    public Transform leftHandAnchor = null;
    public Transform leftControllerAnchor = null;

    public Transform rightHand = null;
    public Transform rightHandAnchor = null;
    public Transform rightControllerAnchor = null;

    //public HandVisual handVisualLeft = null;
    //public HandVisual handVisualRight = null;

    [SerializeField] private bool oculusIntegration = true;

    [SerializeField] private Transform headRig = null;
    [SerializeField] private Transform leftHandRig = null;
    [SerializeField] private Transform rightHandRig = null;

    [SerializeField] private Transform rig = null;
    //[SerializeField] private OVRCameraRig ovrRig = null;
    //[SerializeField] private OVRCameraRigRef ovrRigRef = null;

    private bool useRecordedPoses = false;

    public string[] splitString;
    public string[] headSplit;
    public string[] leftHandSplit;
    public string[] leftControllerAnchorSplit;
    public string[] rightHandSplit;
    public string[] rightControllerAnchorSplit;

    public override void StartReplayMode()
    {
        base.StartReplayMode();
        useRecordedPoses = true;
    }


    private void Awake()
    {
        // prevent assert error when the Hand script is null in HandVisual start()
        //if (trackingActive)
        //{
        //    handVisualLeft.enabled = false;
        //    handVisualRight.enabled = false;
        //}
    }

    public VRRigTransform GetVRRigTransform()
    {
        return new VRRigTransform(head.position, leftHand.position, rightHand.position);
    }

    private void Init()
    {
        if (trackingActive)
        {
            /*
            if (oculusIntegration)
            {
                ovrRig = FindObjectOfType<OVRCameraRig>(true); //both active and inactive objects
                if (!useRecordedPoses)
                {
                    if (ovrRig != null)
                    {
                        ovrRigRef = FindObjectOfType<OVRCameraRigRef>(true);

                        Hand leftHandScript = ovrRigRef.gameObject.transform.Find("Hands/LeftHand").GetComponent<Hand>();
                        //handVisualLeft.Hand = leftHandScript;

                        Hand rightHandScript = ovrRigRef.gameObject.transform.Find("Hands/RightHand").GetComponent<Hand>();
                        //handVisualRight.Hand = rightHandScript;

                        handVisualLeft.enabled = true;
                        handVisualRight.enabled = true;

                    }
                }
            }
            */
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        Init();
    }

    public override void ApplyValue(string type, string value)
    {
        base.ApplyValue(type, value);
        if (trackingActive)
        {
            if (type == logtype.VRRig.ToString())
            {

                splitString = value.Split('_');

                headSplit = splitString[0].Split('|');
                leftHandSplit = splitString[1].Split('|');
                leftControllerAnchorSplit = splitString[2].Split('|');
                rightHandSplit = splitString[3].Split('|');
                rightControllerAnchorSplit = splitString[4].Split('|');

                rig.position = parseVector3(headSplit[0]);
                rig.rotation = parseQuaternion(headSplit[1]);
                rig.localScale = parseVector3(headSplit[2]);

                /*if (oculusIntegration)
                {
                    ovrRig.centerEyeAnchor.position = parseVector3(headSplit[0]);
                    ovrRig.centerEyeAnchor.rotation = parseQuaternion(headSplit[1]);
                    ovrRig.centerEyeAnchor.localScale = parseVector3(headSplit[2]);
                }
                */

                head.position = parseVector3(headSplit[0]);
                head.rotation = parseQuaternion(headSplit[1]);
                head.localScale = parseVector3(headSplit[2]);

                leftHand.position = parseVector3(leftHandSplit[0]);
                leftHand.rotation = parseQuaternion(leftHandSplit[1]);
                leftHand.localScale = parseVector3(leftHandSplit[2]);

                leftControllerAnchor.position = parseVector3(leftControllerAnchorSplit[0]);
                leftControllerAnchor.rotation = parseQuaternion(leftControllerAnchorSplit[1]);
                leftControllerAnchor.localScale = parseVector3(leftControllerAnchorSplit[2]);

                rightHand.position = parseVector3(rightHandSplit[0]);
                rightHand.rotation = parseQuaternion(rightHandSplit[1]);
                rightHand.localScale = parseVector3(rightHandSplit[2]);

                rightControllerAnchor.position = parseVector3(rightControllerAnchorSplit[0]);
                rightControllerAnchor.rotation = parseQuaternion(rightControllerAnchorSplit[1]);
                rightControllerAnchor.localScale = parseVector3(rightControllerAnchorSplit[2]);
            }
        }
    }

    protected override void Update()
    {
        base.Update();
        if (trackingActive)
        {
            if (!useRecordedPoses)
            {
                /*if (oculusIntegration)
                {
                    StringBuilder sb = new StringBuilder();
                    Utils.CopyTransform(ovrRig.centerEyeAnchor, head);
                    sb.Append($"{ovrRig.centerEyeAnchor.position}|{ovrRig.centerEyeAnchor.rotation}|{ovrRig.centerEyeAnchor.localScale}");
                    Utils.CopyTransform(ovrRig.leftHandAnchor, leftHand);
                    sb.Append($"_{ovrRig.leftHandAnchor.position}|{ovrRig.leftHandAnchor.rotation}|{ovrRig.leftHandAnchor.localScale}");
                    Utils.CopyTransform(ovrRig.leftControllerAnchor, leftControllerAnchor);
                    sb.Append($"_{ovrRig.leftControllerAnchor.position}|{ovrRig.leftControllerAnchor.rotation}|{ovrRig.leftControllerAnchor.localScale}");
                    Utils.CopyTransform(ovrRig.rightHandAnchor, rightHand);
                    sb.Append($"_{ovrRig.rightHandAnchor.position}|{ovrRig.rightHandAnchor.rotation}|{ovrRig.rightHandAnchor.localScale}");
                    Utils.CopyTransform(ovrRig.rightControllerAnchor, rightControllerAnchor);
                    sb.Append($"_{ovrRig.rightControllerAnchor.position}|{ovrRig.rightControllerAnchor.rotation}|{ovrRig.rightControllerAnchor.localScale}");
                    Map.UpdateOrCreate(new KVPair<logtype, string>(logtype.VRRig, sb.ToString()));
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    Utils.CopyTransform(headRig, head);
                    sb.Append($"{headRig.position}|{headRig.rotation}|{headRig.localScale}");
                    Utils.CopyTransform(leftHandRig, leftHand);
                    sb.Append($"_{leftHandRig.position}|{leftHandRig.rotation}|{leftHandRig.localScale}");
                    Utils.CopyTransform(leftHandRig, leftControllerAnchor);
                    sb.Append($"_{rightHandRig.position}|{rightHandRig.rotation}|{rightHandRig.localScale}");
                    Utils.CopyTransform(rightHandRig, rightHand);
                    sb.Append($"_{rightHandRig.position}|{rightHandRig.rotation}|{rightHandRig.localScale}");
                    Utils.CopyTransform(rightHandRig, rightControllerAnchor);
                    sb.Append($"_{rightHandRig.position}|{rightHandRig.rotation}|{rightHandRig.localScale}");
                    Map.UpdateOrCreate(new KVPair<logtype, string>(logtype.VRRig, sb.ToString()));
                }
                */
                StringBuilder sb = new StringBuilder();
                Utils.CopyTransform(headRig, head);
                sb.Append($"{headRig.position}|{headRig.rotation}|{headRig.localScale}");
                Utils.CopyTransform(leftHandRig, leftHand);
                sb.Append($"_{leftHandRig.position}|{leftHandRig.rotation}|{leftHandRig.localScale}");
                Utils.CopyTransform(leftHandRig, leftControllerAnchor);
                sb.Append($"_{rightHandRig.position}|{rightHandRig.rotation}|{rightHandRig.localScale}");
                Utils.CopyTransform(rightHandRig, rightHand);
                sb.Append($"_{rightHandRig.position}|{rightHandRig.rotation}|{rightHandRig.localScale}");
                Utils.CopyTransform(rightHandRig, rightControllerAnchor);
                sb.Append($"_{rightHandRig.position}|{rightHandRig.rotation}|{rightHandRig.localScale}");
                Map.UpdateOrCreate(new KVPair<logtype, string>(logtype.VRRig, sb.ToString()));
            }
        }
        else
        {
            Map.UpdateOrCreate(new KVPair<logtype, string>(logtype.VRRig, "(0.0, 0.0, 0.0) | (0.00000, 0.00000, 0.00000, 0.00000) | (0.0, 0.0, 0.0)_(0.0, 0.0, 0.0) | (0.00000, 0.00000, 0.00000, 0.00000) | (0.0, 0.0, 0.0)_(0.0, 0.0, 0.0) | (0.00000, 0.00000, 0.00000, 0.00000) | (0.0, 0.0, 0.0)_(0.0, 0.0, 0.0) | (0.00000, 0.00000, 0.00000, 0.00000) | (0.0, 0.0, 0.0)_(0.0, 0.0, 0.0) | (0.00000, 0.00000, 0.00000, 0.00000) | (0.0, 0.0, 0.0)"));
        }
    }
}

public class VRRigTransform
{
    public VRRigTransform()
    {
    }

    public VRRigTransform(Vector3 head, Vector3 leftHand, Vector3 rightHand)
    {
        Head = head;
        LeftHand = leftHand;
        RightHand = rightHand;
    }

    [field: SerializeField] public Vector3 Head;
    [field: SerializeField] public Vector3 LeftHand;
    [field: SerializeField] public Vector3 RightHand;
}

[System.Serializable]
public class VRRigTransformQueue
{
    public VRRigTransformQueue()
    {
        list = new List<VRRigTransform>();
    }

    public void Add(VRRigTransform vRRigTransform)
    {
        list.Add(vRRigTransform);
    }

    public Vector3 HeadDiff()
    {
        if (list.Count > 1)
        {
            return new Vector3(
                Mathf.Pow(list[list.Count - 1].Head.x - list[list.Count - 2].Head.x, 2),
                Mathf.Pow(list[list.Count - 1].Head.y - list[list.Count - 2].Head.y, 2),
                Mathf.Pow(list[list.Count - 1].Head.z - list[list.Count - 2].Head.z, 2));
        }
        else
        {
            return new Vector3(0, 0, 0);
        }
    }

    public Vector3 LPDiff()
    {
        if (list.Count > 1)
        {
            return new Vector3(
                Mathf.Pow(list[list.Count - 1].LeftHand.x - list[list.Count - 2].LeftHand.x, 2),
                Mathf.Pow(list[list.Count - 1].LeftHand.y - list[list.Count - 2].LeftHand.y, 2),
                Mathf.Pow(list[list.Count - 1].LeftHand.z - list[list.Count - 2].LeftHand.z, 2));
        }
        else
        {
            return new Vector3(0, 0, 0);
        }
    }

    public Vector3 RPDiff()
    {
        if (list.Count > 1)
        {
            return new Vector3(
                Mathf.Pow(list[list.Count - 1].RightHand.x - list[list.Count - 2].RightHand.x, 2),
                Mathf.Pow(list[list.Count - 1].RightHand.y - list[list.Count - 2].RightHand.y, 2),
                Mathf.Pow(list[list.Count - 1].RightHand.z - list[list.Count - 2].RightHand.z, 2));
        }
        else
        {
            return new Vector3(0, 0, 0);
        }
    }

    [field: SerializeField] public List<VRRigTransform> list;
}

[DisallowMultipleComponent]
/* Contains a list of attributes to be tracked and saved to a log file.
 * These will enable the replaying of these elements. The Logger will go
 * through all of these classes and get the values in the Attributes list
 * every log cycle. We are storing the Items in a list to allow Unity to
 * serialize the data but are using a Dictionary to map the indexes for
 * fast retreival */
public abstract class Tracker : MonoBehaviour
{
    [Header("Tracker")]
    [EnumFlags] public TrackedDataSource.logtype m_options;
    [SerializeField] protected ValueMap<logtype, string> Map = new ValueMap<logtype, string>();

    public virtual void StartReplayMode() { }

    //public void StartReplayMode()
    //{
    //    MonoBehaviour[] comps = GetComponents<MonoBehaviour>();
    //    foreach (MonoBehaviour c in comps)
    //    {
    //        if (c.GetType() != typeof(Tracker))
    //        {
    //            c.enabled = false;
    //        }
    //    }
    //}

    /* Add or Update value on the value map */
    protected void UpdateOrCreate(KVPair<logtype, string> input)
    {
        Map.UpdateOrCreate(input);
    }

    /* Returns the list of attributes */
    public List<KVPair<logtype, string>> GetAttributes()
    {
        return Map.GetAttributes();
    }

    public virtual void ApplyValue(string type, string value)
    {
        if ((logtype)Enum.Parse(typeof(logtype), type) == logtype.Position)
            transform.position = parseVector3(value);
        if ((logtype)Enum.Parse(typeof(logtype), type) == logtype.Rotation)
            transform.localEulerAngles = parseVector3(value);
        if ((logtype)Enum.Parse(typeof(logtype), type) == logtype.Scale)
            transform.localScale = parseVector3(value);
    }

    /* Keep Attributes up to date with values */
    protected virtual void Update()
    {
        List<string> logList = EnumFlagsAttribute.GetSelectedStrings(m_options);
        if (logList.Contains(logtype.Position.ToString()))
        {
            Map.UpdateOrCreate(new KVPair<logtype, string>(logtype.Position, transform.localPosition.ToString()));
        }
        if (logList.Contains(logtype.Rotation.ToString()))
        {
            Map.UpdateOrCreate(new KVPair<logtype, string>(logtype.Rotation, transform.localEulerAngles.ToString()));
        }
        if (logList.Contains(logtype.Scale.ToString()))
        {
            Map.UpdateOrCreate(new KVPair<logtype, string>(logtype.Scale, transform.localScale.ToString()));
        }
    }

    protected Vector3 parseVector3(string sourceString)
    {
        string outString;
        Vector3 outVector3;
        string[] splitString;
        outString = sourceString.Substring(1, sourceString.Length - 2);
        splitString = outString.Split(","[0]);
        outVector3.x = float.Parse(splitString[0]);
        outVector3.y = float.Parse(splitString[1]);
        outVector3.z = float.Parse(splitString[2]);
        return outVector3;
    }

    protected Quaternion parseQuaternion(string sourceString)
    {
        string outString;
        string[] splitString;
        outString = sourceString.Substring(1, sourceString.Length - 2);
        splitString = outString.Split(","[0]);
        return new Quaternion(float.Parse(splitString[0]), float.Parse(splitString[1]), float.Parse(splitString[2]), float.Parse(splitString[3]));
    }
}

public static class Utils
{
    public static string[] OculusHandSidePrefix = { "l_", "r_" };
    public static string[] AvatarHandSidePrefix = { "LeftHand", "RightHand" };
    public static string OculusHandBonePrefix = "b_";

    public static string[] OculusHandBoneNames =
    {
            "wrist",
            "forearm_stub",
            "thumb0",
            "thumb1",
            "thumb2",
            "thumb3",
            "index1",
            "index2",
            "index3",
            "middle1",
            "middle2",
            "middle3",
            "ring1",
            "ring2",
            "ring3",
            "pinky0",
            "pinky1",
            "pinky2",
            "pinky3"
    };

    public static string[] AvatarHandBoneNames =
    {
        "",
        "NA",
        "NA",
        "Thumb1",
        "Thumb2",
        "Thumb3",
        "Index1",
        "Index2",
        "Index3",
        "Middle1",
        "Middle2",
        "Middle3",
        "Ring1",
        "Ring2",
        "Ring3",
        "NA",
        "Pinky1",
        "Pinky2",
        "Pinky3"
    };

    public static string[] OculusHandFingerNames =
    {
            "thumb",
            "index",
            "middle",
            "ring",
            "pinky"
    };

    public static string OculusBoneNameFromHandJointId(Handedness handedness, HandJointId handJointId)
    {
        if (handJointId >= HandJointId.HandThumbTip && handJointId <= HandJointId.HandPinkyTip)
        {
            return OculusHandSidePrefix[(int)handedness] + OculusHandFingerNames[(int)handJointId - (int)HandJointId.HandThumbTip] + "_finger_tip_marker";
        }
        else
        {
            return OculusHandBonePrefix + OculusHandSidePrefix[(int)handedness] + OculusHandBoneNames[(int)handJointId];
        }
    }

    public static string AvatarBoneNameFromHandJointId(Handedness handedness, HandJointId handJointId)
    {
        if ((int)handJointId > AvatarHandBoneNames.Length)
        {
            return "";
        }
        else if (AvatarHandBoneNames[(int)handJointId] != "NA")
        {
            return AvatarHandSidePrefix[(int)handedness] + AvatarHandBoneNames[(int)handJointId];
        }
        else
        {
            return "";
        }
    }

    public static void CopyTransform(Transform source, Transform target, bool useLocal = false)
    {
        if (useLocal)
        {
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }
        else
        {
            target.position = source.position;
            target.rotation = source.rotation;
            target.localScale = source.localScale;
        }
    }

    public static class Constants
    {
        public const int NUM_HAND_JOINTS = (int)HandJointId.HandEnd;
        public const int NUM_FINGERS = 5;
    }

    public enum Handedness
    {
        Left = 0,
        Right = 1,
    }

    public enum HandFinger
    {
        Invalid = -1,
        Thumb = 0,
        Index = 1,
        Middle = 2,
        Ring = 3,
        Pinky = 4,
        Max = 4
    }

    [Flags]
    public enum HandFingerFlags
    {
        None = 0,
        Thumb = 1 << 0,
        Index = 1 << 1,
        Middle = 1 << 2,
        Ring = 1 << 3,
        Pinky = 1 << 4,
        All = (1 << 5) - 1
    }


    public enum PinchGrabParam
    {
        PinchDistanceStart = 0,
        PinchDistanceStopMax,
        PinchDistanceStopOffset,
        PinchHqDistanceStart,
        PinchHqDistanceStopMax,
        PinchHqDistanceStopOffset,
        PinchHqViewAngleThreshold,
        ThumbDistanceStart,
        ThumbDistanceStopMax,
        ThumbDistanceStopOffset,
        ThumbMaxDot,
    }

    [Flags]
    public enum HandFingerJointFlags
    {
        None = 0,
        Wrist = 1 << HandJointId.HandWristRoot,
        ForearmStub = 1 << HandJointId.HandForearmStub,
        Thumb0 = 1 << HandJointId.HandThumb0,
        Thumb1 = 1 << HandJointId.HandThumb1,
        Thumb2 = 1 << HandJointId.HandThumb2,
        Thumb3 = 1 << HandJointId.HandThumb3,
        Index1 = 1 << HandJointId.HandIndex1,
        Index2 = 1 << HandJointId.HandIndex2,
        Index3 = 1 << HandJointId.HandIndex3,
        Middle1 = 1 << HandJointId.HandMiddle1,
        Middle2 = 1 << HandJointId.HandMiddle2,
        Middle3 = 1 << HandJointId.HandMiddle3,
        Ring1 = 1 << HandJointId.HandRing1,
        Ring2 = 1 << HandJointId.HandRing2,
        Ring3 = 1 << HandJointId.HandRing3,
        Pinky0 = 1 << HandJointId.HandPinky0,
        Pinky1 = 1 << HandJointId.HandPinky1,
        Pinky2 = 1 << HandJointId.HandPinky2,
        Pinky3 = 1 << HandJointId.HandPinky3,
        ThumbTip = 1 << HandJointId.HandThumbTip,
        IndexTip = 1 << HandJointId.HandIndexTip,
        MiddleTip = 1 << HandJointId.HandMiddleTip,
        RingTip = 1 << HandJointId.HandRingTip,
        PinkyTip = 1 << HandJointId.HandPinkyTip,
        All = (1 << HandJointId.HandEnd) - 1
    }

    public static class HandFingerUtils
    {
        public static HandFingerFlags ToFlags(HandFinger handFinger)
        {
            return (HandFingerFlags)(1 << (int)handFinger);
        }
    }

    public enum HandJointId
    {
        Invalid = -1,

        // hand bones
        HandStart = 0,
        HandWristRoot = HandStart + 0, // root frame of the hand, where the wrist is located
        HandForearmStub = HandStart + 1, // frame for user's forearm
        HandThumb0 = HandStart + 2, // thumb trapezium bone
        HandThumb1 = HandStart + 3, // thumb metacarpal bone
        HandThumb2 = HandStart + 4, // thumb proximal phalange bone
        HandThumb3 = HandStart + 5, // thumb distal phalange bone
        HandIndex1 = HandStart + 6, // index proximal phalange bone
        HandIndex2 = HandStart + 7, // index intermediate phalange bone
        HandIndex3 = HandStart + 8, // index distal phalange bone
        HandMiddle1 = HandStart + 9, // middle proximal phalange bone
        HandMiddle2 = HandStart + 10, // middle intermediate phalange bone
        HandMiddle3 = HandStart + 11, // middle distal phalange bone
        HandRing1 = HandStart + 12, // ring proximal phalange bone
        HandRing2 = HandStart + 13, // ring intermediate phalange bone
        HandRing3 = HandStart + 14, // ring distal phalange bone
        HandPinky0 = HandStart + 15, // pinky metacarpal bone
        HandPinky1 = HandStart + 16, // pinky proximal phalange bone
        HandPinky2 = HandStart + 17, // pinky intermediate phalange bone
        HandPinky3 = HandStart + 18, // pinky distal phalange bone
        HandMaxSkinnable = HandStart + 19,
        // Bone tips are position only.
        // They are not used for skinning but are useful for hit-testing.
        // NOTE: HandThumbTip == HandMaxSkinnable since the extended tips need to be contiguous
        HandThumbTip = HandMaxSkinnable + 0, // tip of the thumb
        HandIndexTip = HandMaxSkinnable + 1, // tip of the index finger
        HandMiddleTip = HandMaxSkinnable + 2, // tip of the middle finger
        HandRingTip = HandMaxSkinnable + 3, // tip of the ring finger
        HandPinkyTip = HandMaxSkinnable + 4, // tip of the pinky
        HandEnd = HandMaxSkinnable + 5,
    }



    public class HandJointUtils
    {
        public static List<HandJointId[]> FingerToJointList = new List<HandJointId[]>()
        {
            new[] {HandJointId.HandThumb0,HandJointId.HandThumb1,HandJointId.HandThumb2,HandJointId.HandThumb3},
            new[] {HandJointId.HandIndex1, HandJointId.HandIndex2, HandJointId.HandIndex3},
            new[] {HandJointId.HandMiddle1, HandJointId.HandMiddle2, HandJointId.HandMiddle3},
            new[] {HandJointId.HandRing1,HandJointId.HandRing2,HandJointId.HandRing3},
            new[] {HandJointId.HandPinky0, HandJointId.HandPinky1, HandJointId.HandPinky2, HandJointId.HandPinky3}
        };

        public static HandJointId[] JointParentList = new[]
        {
            HandJointId.Invalid,
            HandJointId.HandStart,
            HandJointId.HandStart,
            HandJointId.HandThumb0,
            HandJointId.HandThumb1,
            HandJointId.HandThumb2,
            HandJointId.HandStart,
            HandJointId.HandIndex1,
            HandJointId.HandIndex2,
            HandJointId.HandStart,
            HandJointId.HandMiddle1,
            HandJointId.HandMiddle2,
            HandJointId.HandStart,
            HandJointId.HandRing1,
            HandJointId.HandRing2,
            HandJointId.HandStart,
            HandJointId.HandPinky0,
            HandJointId.HandPinky1,
            HandJointId.HandPinky2,
            HandJointId.HandThumb3,
            HandJointId.HandIndex3,
            HandJointId.HandMiddle3,
            HandJointId.HandRing3,
            HandJointId.HandPinky3
        };

        public static HandJointId[][] JointChildrenList = new[]
        {
            new []
            {
                HandJointId.HandThumb0,
                HandJointId.HandIndex1,
                HandJointId.HandMiddle1,
                HandJointId.HandRing1,
                HandJointId.HandPinky0
            },
            new HandJointId[0],
            new []{ HandJointId.HandThumb1 },
            new []{ HandJointId.HandThumb2 },
            new []{ HandJointId.HandThumb3 },
            new []{ HandJointId.HandThumbTip },
            new []{ HandJointId.HandIndex2 },
            new []{ HandJointId.HandIndex3 },
            new []{ HandJointId.HandIndexTip },
            new []{ HandJointId.HandMiddle2 },
            new []{ HandJointId.HandMiddle3 },
            new []{ HandJointId.HandMiddleTip },
            new []{ HandJointId.HandRing2 },
            new []{ HandJointId.HandRing3 },
            new []{ HandJointId.HandRingTip },
            new []{ HandJointId.HandPinky1 },
            new []{ HandJointId.HandPinky2 },
            new []{ HandJointId.HandPinky3 },
            new []{ HandJointId.HandPinkyTip },
            new HandJointId[0],
            new HandJointId[0],
            new HandJointId[0],
            new HandJointId[0],
            new HandJointId[0]
        };

        public static List<HandJointId> JointIds = new List<HandJointId>()
        {
            HandJointId.HandIndex1,
            HandJointId.HandIndex2,
            HandJointId.HandIndex3,
            HandJointId.HandMiddle1,
            HandJointId.HandMiddle2,
            HandJointId.HandMiddle3,
            HandJointId.HandRing1,
            HandJointId.HandRing2,
            HandJointId.HandRing3,
            HandJointId.HandPinky0,
            HandJointId.HandPinky1,
            HandJointId.HandPinky2,
            HandJointId.HandPinky3,
            HandJointId.HandThumb0,
            HandJointId.HandThumb1,
            HandJointId.HandThumb2,
            HandJointId.HandThumb3
        };

        private static readonly HandJointId[] _handFingerProximals =
        {
            HandJointId.HandThumb2, HandJointId.HandIndex1, HandJointId.HandMiddle1,
            HandJointId.HandRing1, HandJointId.HandPinky1
        };

        public static HandJointId GetHandFingerTip(HandFinger finger)
        {
            return HandJointId.HandMaxSkinnable + (int)finger;
        }

        /// <summary>
        /// Returns the "proximal" JointId for the given finger.
        /// This is commonly known as the Knuckle.
        /// For fingers, proximal is the join with index 1; eg HandIndex1.
        /// For thumb, proximal is the joint with index 2; eg HandThumb2.
        /// </summary>
        public static HandJointId GetHandFingerProximal(HandFinger finger)
        {
            return _handFingerProximals[(int)finger];
        }
    }

    public struct HandSkeletonJoint
    {
        /// <summary>
        /// Id of the parent joint in the skeleton hierarchy. Must always have a lower index than
        /// this joint.
        /// </summary>
        public int parent;

        /// <summary>
        /// Stores the pose of the joint, in local space.
        /// </summary>
        public Pose pose;

        /// <summary>
        /// Radius of the bones starting at this joint
        /// </summary>
        public float radius;
    }

    public interface IReadOnlyHandSkeletonJointList
    {
        ref readonly HandSkeletonJoint this[int jointId] { get; }
    }

    public interface IReadOnlyHandSkeleton
    {
        IReadOnlyHandSkeletonJointList Joints { get; }
    }

    public interface ICopyFrom<in TSelfType>
    {
        void CopyFrom(TSelfType source);
    }

    public class ReadOnlyHandJointPoses : IReadOnlyList<Pose>
    {
        private Pose[] _poses;

        public ReadOnlyHandJointPoses(Pose[] poses)
        {
            _poses = poses;
        }

        public IEnumerator<Pose> GetEnumerator()
        {
            foreach (var pose in _poses)
            {
                yield return pose;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public static ReadOnlyHandJointPoses Empty { get; } = new ReadOnlyHandJointPoses(Array.Empty<Pose>());

        public int Count => _poses.Length;

        public Pose this[int index] => _poses[index];

        public ref readonly Pose this[HandJointId index] => ref _poses[(int)index];
    }

    public class HandSkeleton : IReadOnlyHandSkeleton, IReadOnlyHandSkeletonJointList
    {
        public HandSkeletonJoint[] joints = new HandSkeletonJoint[Constants.NUM_HAND_JOINTS];
        public IReadOnlyHandSkeletonJointList Joints => this;
        public ref readonly HandSkeletonJoint this[int jointId] => ref joints[jointId];


        public static readonly HandSkeleton DefaultLeftSkeleton = new HandSkeleton()
        {
            joints = new HandSkeletonJoint[]
            {
                new HandSkeletonJoint(){parent = -1, pose = new Pose(new Vector3(0f,0f,0f), new Quaternion(0f,0f,0f,-1f))},
                new HandSkeletonJoint(){parent = 0,  pose = new Pose(new Vector3(0f,0f,0f), new Quaternion(0f,0f,0f,-1f))},
                new HandSkeletonJoint(){parent = 0,  pose = new Pose(new Vector3(-0.0200693f,0.0115541f,-0.01049652f), new Quaternion(-0.3753869f,0.4245841f,-0.007778856f,-0.8238644f))},
                new HandSkeletonJoint(){parent = 2,  pose = new Pose(new Vector3(-0.02485256f,-9.31E-10f,-1.863E-09f), new Quaternion(-0.2602303f,0.02433088f,0.125678f,-0.9570231f))},
                new HandSkeletonJoint(){parent = 3,  pose = new Pose(new Vector3(-0.03251291f,5.82E-10f,1.863E-09f), new Quaternion(0.08270377f,-0.0769617f,-0.08406223f,-0.9900357f))},
                new HandSkeletonJoint(){parent = 4,  pose = new Pose(new Vector3(-0.0337931f,3.26E-09f,1.863E-09f), new Quaternion(-0.08350593f,0.06501573f,-0.05827406f,-0.9926752f))},
                new HandSkeletonJoint(){parent = 0,  pose = new Pose(new Vector3(-0.09599624f,0.007316455f,-0.02355068f), new Quaternion(-0.03068309f,-0.01885559f,0.04328144f,-0.9984136f))},
                new HandSkeletonJoint(){parent = 6,  pose = new Pose(new Vector3(-0.0379273f,-5.82E-10f,-5.97E-10f), new Quaternion(0.02585241f,-0.007116061f,0.003292944f,-0.999635f))},
                new HandSkeletonJoint(){parent = 7,  pose = new Pose(new Vector3(-0.02430365f,-6.73E-10f,-6.75E-10f), new Quaternion(0.016056f,-0.02714872f,-0.072034f,-0.9969034f))},
                new HandSkeletonJoint(){parent = 0,  pose = new Pose(new Vector3(-0.09564661f,0.002543155f,-0.001725906f), new Quaternion(0.009066326f,-0.05146559f,0.05183575f,-0.9972874f))},
                new HandSkeletonJoint(){parent = 9,  pose = new Pose(new Vector3(-0.042927f,-8.51E-10f,-1.193E-09f), new Quaternion(0.01122823f,-0.004378874f,-0.001978267f,-0.9999254f))},
                new HandSkeletonJoint(){parent = 10, pose = new Pose(new Vector3(-0.02754958f,3.09E-10f,1.128E-09f), new Quaternion(0.03431955f,-0.004611839f,-0.09300701f,-0.9950631f))},
                new HandSkeletonJoint(){parent = 0,  pose = new Pose(new Vector3(-0.0886938f,0.006529308f,0.01746524f), new Quaternion(0.05315936f,-0.1231034f,0.04981349f,-0.9897162f))},
                new HandSkeletonJoint(){parent = 12, pose = new Pose(new Vector3(-0.0389961f,0f,5.24E-10f), new Quaternion(0.03363252f,-0.00278984f,0.00567602f,-0.9994143f))},
                new HandSkeletonJoint(){parent = 13, pose = new Pose(new Vector3(-0.02657339f,1.281E-09f,1.63E-09f), new Quaternion(0.003477462f,0.02917945f,-0.02502854f,-0.9992548f))},
                new HandSkeletonJoint(){parent = 0,  pose = new Pose(new Vector3(-0.03407356f,0.009419836f,0.02299858f), new Quaternion(0.207036f,-0.1403428f,0.0183118f,-0.9680417f))},
                new HandSkeletonJoint(){parent = 15, pose = new Pose(new Vector3(-0.04565055f,9.97679E-07f,-2.193963E-06f), new Quaternion(-0.09111304f,0.00407137f,0.02812923f,-0.9954349f))},
                new HandSkeletonJoint(){parent = 16, pose = new Pose(new Vector3(-0.03072042f,1.048E-09f,-1.75E-10f), new Quaternion(0.03761665f,-0.04293772f,-0.01328605f,-0.9982809f))},
                new HandSkeletonJoint(){parent = 17, pose = new Pose(new Vector3(-0.02031138f,-2.91E-10f,9.31E-10f), new Quaternion(-0.0006447434f,0.04917067f,-0.02401883f,-0.9985014f))},
                new HandSkeletonJoint(){parent = 5,  pose = new Pose(new Vector3(-0.02459077f,-0.001026974f,0.0006703701f), new Quaternion(0f,0f,0f,-1f))},
                new HandSkeletonJoint(){parent = 8,  pose = new Pose(new Vector3(-0.02236338f,-0.00102507f,0.0002956076f), new Quaternion(0f,0f,0f,-1f))},
                new HandSkeletonJoint(){parent = 11, pose = new Pose(new Vector3(-0.02496492f,-0.001137299f,0.0003086528f), new Quaternion(0f,0f,0f,-1f))},
                new HandSkeletonJoint(){parent = 14, pose = new Pose(new Vector3(-0.02432613f,-0.001608172f,0.000257905f), new Quaternion(0f,0f,0f,-1f))},
                new HandSkeletonJoint(){parent = 18, pose = new Pose(new Vector3(-0.02192238f,-0.001216086f,-0.0002464796f), new Quaternion(0f,0f,0f,-1f)) }
            }
        };

        public static readonly HandSkeleton DefaultRightSkeleton = new HandSkeleton()
        {
            joints = DefaultLeftSkeleton.joints.Select(joint => new HandSkeletonJoint()
            {
                parent = joint.parent,
                pose = new Pose(-joint.pose.position, joint.pose.rotation)
            }).ToArray()
        };
    }
}

[System.Serializable]
public class KVPair<TKey, TValue>
{
    public KVPair()
    {
    }

    public KVPair(TKey key, TValue value)
    {
        Key = key;
        Value = value;
    }

    [field: SerializeField] public TKey Key { set; get; }
    [field: SerializeField] public TValue Value { set; get; }
}

public sealed class EnumFlagsAttribute : PropertyAttribute
{
    public EnumFlagsAttribute() { }

    public static List<int> GetSelectedIndexes<T>(T val) where T : IConvertible
    {
        List<int> selectedIndexes = new List<int>();
        for (int i = 0; i < Enum.GetValues(typeof(T)).Length; i++)
        {
            int layer = 1 << i;
            if ((Convert.ToInt32(val) & layer) != 0)
            {
                selectedIndexes.Add(i);
            }
        }
        return selectedIndexes;
    }
    public static List<string> GetSelectedStrings<T>(T val) where T : IConvertible
    {
        List<string> selectedStrings = new List<string>();
        for (int i = 0; i < Enum.GetValues(typeof(T)).Length; i++)
        {
            int layer = 1 << i;
            if ((Convert.ToInt32(val) & layer) != 0)
            {
                selectedStrings.Add(Enum.GetValues(typeof(T)).GetValue(i).ToString());
            }
        }
        return selectedStrings;
    }
}
#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(EnumFlagsAttribute))]
public class EnumFlagsAttributeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        property.intValue = EditorGUI.MaskField(position, label, property.intValue, property.enumNames);
    }
}
#endif

[Serializable]
    public class ValueMap<TKey, TValue>
    {
        [SerializeField]
        protected List<KVPair<TKey, TValue>> Attributes = null;
        private Dictionary<TKey, int> Mapper = null;

        public ValueMap()
        {
            Attributes = new List<KVPair<TKey, TValue>>();
            Mapper = new Dictionary<TKey, int>();
        }

        /* Update or Create an item in the attributes array. Whenever the value
         * changes this will be called with the new keyvaluepair to update the
         * attribute. This can also be used to create a new value. */
        public void UpdateOrCreate(KVPair<TKey, TValue> input)
        {
            if (Mapper.ContainsKey(input.Key))
            {
                int index = Mapper[input.Key];
                Attributes[index] = input;

            }
            else
            {
                Attributes.Add(input);
                Mapper.Add(input.Key, Attributes.IndexOf(input));
            }
        }

        /* Returns the list of attributes */
        public List<KVPair<TKey, TValue>> GetAttributes()
        {
            return Attributes;
        }

        public override string ToString()
        {
            return string.Join(",", Attributes.Select(t => string.Format("{0}, {1}", t.Key, t.Value)));
        }

        public string ToLine(char delimiter)
        {
            return string.Join(delimiter, Attributes.Select(t => string.Format("{0}:{1}", t.Key, t.Value)));
        }

    }