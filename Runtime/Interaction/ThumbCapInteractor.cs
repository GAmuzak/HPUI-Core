using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace ubco.ovilab.HPUI.Interaction
{

    [RequireComponent(typeof(XRHandTrackingEvents))]
    public class ThumbCapInteractor : XRBaseInteractor, IHPUIInteractor
    {
        public IHPUIGestureLogic GestureLogic { get; set; }
        public float InteractionSelectionRadius => interactionSelectionRadius;
        public float TapDistanceThreshold => tapDistanceThreshold;
        public float TapTimeThreshold => tapTimeThreshold;

        private HPUITapEvent tapEvent = new HPUITapEvent();

        /// <inheritdoc />
        public HPUITapEvent TapEvent { get => tapEvent; set => tapEvent = value; }

        [SerializeField]
        [Tooltip("Event triggered on gesture")]
        private HPUIGestureEvent gestureEvent = new HPUIGestureEvent();

        /// <inheritdoc />
        public HPUIGestureEvent GestureEvent { get => gestureEvent; set => gestureEvent = value; }

        [SerializeField]
        [Tooltip("Event triggered on hover update.")]
        private HPUIHoverUpdateEvent hoverUpdateEvent = new HPUIHoverUpdateEvent();

        [SerializeField]
        [Tooltip("Interaction hover radius.")]
        private float interactionHoverRadius = 0.015f;

        /// <inheritdoc />
        public HPUIHoverUpdateEvent HoverUpdateEvent { get => hoverUpdateEvent; set => hoverUpdateEvent = value; }

        protected IHPUIGestureLogic gestureLogic;

        [SerializeField] private SkinnedMeshRenderer foreSkin;
        [SerializeField] private float interactionSelectionRadius = 0.001f;
        [SerializeField] private float tapDistanceThreshold;
        [SerializeField] private float tapTimeThreshold;
        [SerializeField] private int startIndex = 0;
        [SerializeField] private int endIndex = 2680;

        public bool SelectOnlyPriorityTarget { get => selectOnlyPriorityTarget; set => selectOnlyPriorityTarget = value; }
        private Vector3 lastInteractionPoint;
        private PhysicsScene physicsScene;
        private RaycastHit[] rayCastHits = new RaycastHit[200];
        private GameObject visualsObject;
        private Dictionary<IHPUIInteractable, InteractionInfo> validTargets = new();
        private Dictionary<IHPUIInteractable, List<InteractionInfo>> tempValidTargets = new();
        protected Dictionary<XRHandJointID, Vector3> jointLocations = new();

        [SerializeField]
        [Tooltip("(optional) XR Origin transform. If not set, will attempt to find XROrigin and use its transform.")]
        private Transform xrOriginTransform;

        //Mesh Variables
        private Mesh bakedMesh;
        public event Action<string> data;

        #region JointData
        protected List<XRHandJointID> trackedJoints = new List<XRHandJointID>()
        {
            XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip,
            XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip,
            XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip,
            XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip,
            XRHandJointID.ThumbTip
        };
        private Dictionary<XRHandJointID, List<XRHandJointID>> trackedJointsToRelatedFingerJoints = new ()
        {
            {XRHandJointID.IndexProximal, new List<XRHandJointID>() {XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip}},
            {XRHandJointID.IndexIntermediate, new List<XRHandJointID>() {XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip}},
            {XRHandJointID.IndexDistal, new List<XRHandJointID>() {XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip}},
            {XRHandJointID.IndexTip, new List<XRHandJointID>() {XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip}},
            {XRHandJointID.MiddleProximal, new List<XRHandJointID>() {XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip}},
            {XRHandJointID.MiddleIntermediate, new List<XRHandJointID>() {XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip}},
            {XRHandJointID.MiddleDistal, new List<XRHandJointID>() {XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip}},
            {XRHandJointID.MiddleTip, new List<XRHandJointID>() {XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip}},
            {XRHandJointID.RingProximal, new List<XRHandJointID>() {XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip}},
            {XRHandJointID.RingIntermediate, new List<XRHandJointID>() {XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip}},
            {XRHandJointID.RingDistal, new List<XRHandJointID>() {XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip}},
            {XRHandJointID.RingTip, new List<XRHandJointID>() {XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip}},
            {XRHandJointID.LittleProximal, new List<XRHandJointID>() {XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip}},
            {XRHandJointID.LittleIntermediate, new List<XRHandJointID>() {XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip}},
            {XRHandJointID.LittleDistal, new List<XRHandJointID>() {XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip}},
            {XRHandJointID.LittleTip, new List<XRHandJointID>() {XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip}},
        };



        #endregion

        private XRHandTrackingEvents xrHandTrackingEvents;
        private bool receivedNewJointData;
        [SerializeField] private Vector3[] vertices;
        [SerializeField] private Vector3[] normals;
        private bool selectOnlyPriorityTarget = true;


        #region UnityEvents

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        protected override void Awake()
        {
            base.Awake();
            foreSkin = GetComponent<SkinnedMeshRenderer>();
            physicsScene = gameObject.scene.GetPhysicsScene();
            xrHandTrackingEvents = GetComponent<XRHandTrackingEvents>();
            foreach(XRHandJointID id in trackedJoints)
            {
                jointLocations.Add(id, Vector3.zero);
            }
            xrHandTrackingEvents.jointsUpdated.AddListener(UpdateJointsData);
        }

        protected void UpdateJointsData(XRHandJointsUpdatedEventArgs args)
        {
            foreach(XRHandJointID id in trackedJoints)
            {
                if ( args.hand.GetJoint(id).TryGetPose(out Pose pose) )
                {
                    jointLocations[id] = xrOriginTransform.TransformPoint(pose.position);
                    receivedNewJointData = true;
                }
            }
        }

        void Start()
        {
            InitialiseMesh();
            UpdateLogic();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }


        #endregion


        #region XRBaseInteractable

        public override void PreprocessInteractor(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            Transform attachTransform = GetAttachTransform(null);
            Vector3 interactionPoint = attachTransform.position;
            Vector3 hoverEndPoint = attachTransform.position;

            base.PreprocessInteractor(updatePhase);
            UnityEngine.Profiling.Profiler.BeginSample("HPUIInteractor.ProcessInteractor");
            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
            {
                validTargets.Clear();
                foreSkin.BakeMesh(bakedMesh, true);
                vertices = bakedMesh.vertices;
                normals = bakedMesh.normals;

                transform.TransformPoints(vertices);

                tempValidTargets.Clear();
                ShootRayCastsFromSurface(vertices, normals, out List<RaycastHit> raycastHits);
                foreach (RaycastHit rayCastHit in raycastHits)
                {
                    Debug.Log(rayCastHits.Length);
                    bool validInteractable = false;
                    if (interactionManager.TryGetInteractableForCollider(rayCastHit.collider, out var interactable) &&
                        interactable is IHPUIInteractable hpuiInteractable && hpuiInteractable.IsHoverableBy(this))
                    {
                        if (!tempValidTargets.TryGetValue(hpuiInteractable, out List<InteractionInfo> infoList))
                        {
                            infoList = ListPool<InteractionInfo>.Get();
                            tempValidTargets.Add(hpuiInteractable, infoList);
                        }
                        infoList.Add(new InteractionInfo(rayCastHit.distance, rayCastHit.point, rayCastHit.collider));
                    }
                }
                Vector3 centroid;
                float xEndPoint = 0, yEndPoint = 0, zEndPoint = 0;
                float count = tempValidTargets.Sum(kvp => kvp.Value.Count);
                Debug.Log($"Temp Valid Targets {tempValidTargets.Count}");
                UnityEngine.Profiling.Profiler.BeginSample("raycast centroid");

                foreach (KeyValuePair<IHPUIInteractable, List<InteractionInfo>> kvp in tempValidTargets)
                {
                    int localCount = kvp.Value.Count;
                    float localXEndPoint = 0, localYEndPoint = 0, localZEndPoint = 0;

                    foreach(InteractionInfo i in kvp.Value)
                    {
                        xEndPoint += i.point.x;
                        yEndPoint += i.point.y;
                        zEndPoint += i.point.z;
                        localXEndPoint += i.point.x;
                        localYEndPoint += i.point.y;
                        localZEndPoint += i.point.z;
                    }

                    centroid = new Vector3(localXEndPoint, localYEndPoint, localZEndPoint) / count;
                    InteractionInfo closestToCentroid = kvp.Value.OrderBy(el => (el.point - centroid).magnitude).First();
                    // This distance is needed to compute the selection
                    float shortestDistance = kvp.Value.Min(el => el.distance);
                    closestToCentroid.heuristic = (((float)count / (float)localCount) + 1) * shortestDistance;
                    closestToCentroid.distance = shortestDistance;
                    closestToCentroid.extra = (float)localCount;

                    validTargets.Add(kvp.Key, closestToCentroid);
                    Debug.Log($"Valid Targets: {validTargets.Count}");
                    ListPool<InteractionInfo>.Release(kvp.Value);
                }

                if (count > 0)
                {
                    hoverEndPoint = new Vector3(xEndPoint, yEndPoint, zEndPoint) / count;;
                }
                UnityEngine.Profiling.Profiler.EndSample();

                try
                {
                    if (validTargets.Count > 0)
                    {
                        HoverUpdateEvent?.Invoke(new HPUIHoverUpdateEventArgs(this, hoverEndPoint, attachTransform.position));
                    }
                }
                finally
                {
                    //Debug.Log($"Valid Target Count {validTargets.Count}");
                    UnityEngine.Profiling.Profiler.BeginSample("gestureLogic");
                    GestureLogic.Update(validTargets.ToDictionary(kvp => kvp.Key, kvp => new HPUIInteractionData(kvp.Value.distance, kvp.Value.heuristic, kvp.Value.extra)));
                    UnityEngine.Profiling.Profiler.EndSample();
                }
            }
            UnityEngine.Profiling.Profiler.EndSample();
        }


        #endregion

        protected void InitialiseMesh()
        {
            bakedMesh = new Mesh();
            int vertexCount = foreSkin.sharedMesh.vertexCount;
        }

        private void UpdateLogic()
        {
            // When values are changed in inspector, update the values
            if (GestureLogic != null)
            {
                if (!(GestureLogic is HPUIGestureLogic))
                {
                    Debug.Log($"Non HPUIGestureLogic being used");
                    return;
                }
                GestureLogic.Dispose();
            }

            // If using raycast, use heuristic
            GestureLogic = new HPUIGestureLogic(this, TapTimeThreshold, TapDistanceThreshold, InteractionSelectionRadius, useHeuristic: true);
        }

        public override void GetValidTargets(List<IXRInteractable> targets)
        {
            base.GetValidTargets(targets);

            targets.Clear();
            IEnumerable<IHPUIInteractable> filteredValidTargets = validTargets
                .Where(kvp => (kvp.Key is IHPUIInteractable))
                .GroupBy(kvp => kvp.Key.zOrder)
                .Select(g => g
                    .OrderBy(kvp => kvp.Value.distance)
                    .First()
                    .Key)
                .OrderBy(ht => ht.zOrder);
            targets.AddRange(filteredValidTargets);
        }

        public override bool CanSelect(IXRSelectInteractable interactable)
        {
            bool canSelect = validTargets.TryGetValue(interactable as IHPUIInteractable, out InteractionInfo info) &&
                             info.distance < interactionSelectionRadius &&
                             ProcessSelectFilters(interactable);
            return canSelect && (!SelectOnlyPriorityTarget || GestureLogic.IsPriorityTarget(interactable as IHPUIInteractable));
        }

        protected void ShootRayCastsFromSurface(Vector3[] _vertices, Vector3[] _normals, out List<RaycastHit> raycastHits)
        {
            UnityEngine.Profiling.Profiler.BeginSample("raycasts");
            raycastHits = new List<RaycastHit>();
            for (int i = 0; i < _vertices.Length; i++)
            {
                if (i < startIndex || i > endIndex)
                {
                    continue;
                }
                Vector3 start = _vertices[i];
                Vector3 direction = transform.rotation  * _normals[i];
                Ray rayCast = new(start, direction.normalized);
                //Optimisation: Should be using RayCastNonAlloc but we have enough compute :)
                if (Physics.Raycast(rayCast, out RaycastHit hit, interactionSelectionRadius))
                {
                    raycastHits.Add(hit);
                    Debug.DrawLine(start, start + direction.normalized * interactionSelectionRadius, Color.green);
                }
                else
                {
                    Debug.DrawLine(start, start + direction.normalized * interactionSelectionRadius, Color.red);
                }
            }
            UnityEngine.Profiling.Profiler.EndSample();
        }


        #region IHPUIInteractor


        public void OnTap(HPUITapEventArgs args)
        {
            tapEvent?.Invoke(args);
        }

        public void OnGesture(HPUIGestureEventArgs args)
        {
            gestureEvent?.Invoke(args);
        }

        public bool GetDistanceInfo(IHPUIInteractable interactable, out DistanceInfo distanceInfo)
        {
            if (validTargets.TryGetValue(interactable, out InteractionInfo info))
            {
                distanceInfo = new DistanceInfo
                {
                    point = info.point,
                    distanceSqr = (info.collider.transform.position - info.point).sqrMagnitude,
                    collider = info.collider
                };
                return true;
            }
            distanceInfo = new DistanceInfo();
            return false;
        }


        #endregion

    }
}

public struct InteractionInfo
{
    public float distance;
    public Vector3 point;
    public Collider collider;
    public float heuristic;
    public float extra;

    public InteractionInfo(float distance, Vector3 point, Collider collider, float heuristic=0, float extra=0) : this()
    {
        this.distance = distance;
        this.point = point;
        this.collider = collider;
        this.heuristic = heuristic;
        this.extra = extra;
    }
}


