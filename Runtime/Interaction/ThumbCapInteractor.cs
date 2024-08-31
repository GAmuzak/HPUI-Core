using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ubco.ovilab.HPUI.Interaction
{
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRHandTrackingEvents))]
    public class ThumbCapInteractor: XRBaseInteractor, IHPUIInteractor
    {
        public new InteractorHandedness handedness
        {
            get => base.handedness;
            set => base.handedness = value;
        }

        [Tooltip("The thumb cap mesh with somewhat equidistant vertices using QuadriFlow Requadrangulation")]
        [SerializeField]
        private SkinnedMeshRenderer thumbCapMesh;

        [Tooltip("The time threshold at which an interaction would be treated as a gesture.")]
        [SerializeField]
        private float tapTimeThreshold;

        /// <summary>
        /// The time threshold at which an interaction would be treated as a gesture.
        /// That is, if the interactor is in contact with an
        /// interactable for more than this threshold, it would be
        /// treated as a gesture.
        /// </summary>
        public float TapTimeThreshold
        {
            get => tapTimeThreshold;
            set => tapTimeThreshold = value;
        }

        [Tooltip("The distance threshold at which an interaction would be treated as a gesture.")]
        [SerializeField]
        private float tapDistanceThreshold;

        /// <summary>
        /// The distance threshold at which an interaction would be treated as a gesture.
        /// That is, if the interactor has moved more than this value
        /// after coming into contact with an interactable, it would be
        /// treated as a gesture.
        /// </summary>
        public float TapDistanceThreshold
        {
            get => tapDistanceThreshold;
            set => tapDistanceThreshold = value;
        }

        [SerializeField]
        [Tooltip("Event triggered on tap")]
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

        /// <inheritdoc />
        public HPUIHoverUpdateEvent HoverUpdateEvent { get => hoverUpdateEvent; set => hoverUpdateEvent = value; }

        [SerializeField]
        [Tooltip("Interaction hover radius.")]
        private float interactionHoverRadius = 0.015f;

        [SerializeField]
        [Tooltip("Interaction select radius.")]
        private float interactionSelectionRadius = 0.015f;

        /// <summary>
        /// Interaction selection radius.
        /// </summary>
        public float InteractionSelectionRadius
        {
            get => interactionSelectionRadius;
            set => interactionSelectionRadius = value;
        }

        [SerializeField]
        [Tooltip("If true, select only happens for the target with highest priority.")]
        private bool selectOnlyPriorityTarget = true;

        /// <summary>
        /// If true, select only happens for the target with the highest priority.
        /// </summary>
        public bool SelectOnlyPriorityTarget { get => selectOnlyPriorityTarget; set => selectOnlyPriorityTarget = value; }

        // QueryTriggerInteraction.Ignore
        [SerializeField]
        [Tooltip("Physics layer mask used for limiting poke sphere overlap.")]
        private LayerMask physicsLayer = Physics.AllLayers;

        /// <summary>
        /// Physics layer mask used for limiting poke sphere overlap.
        /// </summary>
        public LayerMask PhysicsLayer { get => physicsLayer; set => physicsLayer = value; }

        [SerializeField]
        [Tooltip("Determines whether triggers should be collided with.")]
        private QueryTriggerInteraction physicsTriggerInteraction = QueryTriggerInteraction.Ignore;

        /// <summary>
        /// Determines whether triggers should be collided with.
        /// </summary>
        public QueryTriggerInteraction PhysicsTriggerInteraction { get => physicsTriggerInteraction; set => physicsTriggerInteraction = value; }

        [SerializeField] private int startIndex = 0;
        [SerializeField] private int endIndex = 2680;

        [SerializeField]
        [Tooltip("Show sphere rays used for interaction selections.")]
        private bool showDebugRayVisual = true;

        /// <summary>
        /// Show sphere rays used for interaction selections.
        /// </summary>
        public bool ShowDebugRayVisual { get => showDebugRayVisual; set => showDebugRayVisual = value; }

        [SerializeField]
        [Tooltip("(optional) XR Origin transform. If not set, will attempt to find XROrigin and use its transform.")]
        private Transform xrOriginTransform;

        /// <summary>
        /// XR Origin transform. If not set, will attempt to find XROrigin and use its transform.
        /// </summary>
        public Transform XROriginTransform { get => xrOriginTransform; set => xrOriginTransform = value; }

        private Dictionary<IHPUIInteractable, InteractionInfo> validTargets = new Dictionary<IHPUIInteractable, InteractionInfo>();
        private PhysicsScene physicsScene;
        private Mesh bakedMesh;
        private Vector3[] vertices, normals;
        private RaycastHit[] raycastHits;
        private bool[] raycastHitResults;
        private int vertexCount;
        private Transform thumbTransform;

        protected override void Awake()
        {
            base.Awake();
            keepSelectedTargetValid = true;
            physicsScene = gameObject.scene.GetPhysicsScene();

            if (XROriginTransform == null)
            {
                XROriginTransform = FindObjectOfType<XROrigin>()?.transform;
                if (XROriginTransform == null)
                {
                    Debug.LogError($"XR Origin not found! Manually set value for XROriginTransform");
                }
            }

            if(thumbCapMesh==null)
            {
                thumbCapMesh = GetComponent<SkinnedMeshRenderer>();
                if (thumbCapMesh == null)
                {
                    Debug.LogError("Can't find thumbcap mesh!");
                }
            }
        }

        protected override void Start()
        {
            base.Start();
            Init();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

        protected void Update()
        {
            FireRayCasts();
        }

        protected void Init()
        {
            bakedMesh = new Mesh();
            vertexCount = thumbCapMesh.sharedMesh.vertexCount;
            raycastHits = new RaycastHit[vertexCount];
            raycastHitResults = new bool[vertexCount];
            vertices = new Vector3[vertexCount];
            normals = new Vector3[vertexCount];
            thumbTransform = thumbCapMesh.transform;
        }

        protected void FireRayCasts()
        {
            thumbCapMesh.BakeMesh(bakedMesh);
            vertices = bakedMesh.vertices;
            normals = bakedMesh.normals;

            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 position = thumbTransform.TransformPoint(vertices[i]);
                Vector3 direction = thumbTransform.TransformDirection(normals[i]);

                Ray ray = new Ray(position, direction);
                if (Physics.Raycast(ray, out RaycastHit hitData, InteractionSelectionRadius))
                {
                    raycastHitResults[i] = true;
                    raycastHits[i] = hitData;
                    if(showDebugRayVisual) Debug.DrawRay(position,direction.normalized * InteractionSelectionRadius, Color.green);
                }
                else
                {
                    raycastHitResults[i] = false;
                    raycastHits[i] = default;
                    if(showDebugRayVisual) Debug.DrawRay(position,direction.normalized * InteractionSelectionRadius, Color.red);
                }
            }
            ProcessRaycastHitData();
        }

        protected void ProcessRaycastHitData()
        {
            //TODO: Your thumbcap logic here
        }

        #region XRI Manager Functions

        /// <inheritdoc />
        public override void PreprocessInteractor(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.PreprocessInteractor(updatePhase);
            //TODO: Implement interactor processing logic here
        }

        /// <inheritdoc />
        public override void GetValidTargets(List<IXRInteractable> targets)
        {
            base.GetValidTargets(targets);
            targets.Clear();

            //TODO: Change this logic here as well?
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

        /// <inheritdoc />
        public override bool CanSelect(IXRSelectInteractable interactable)
        {
            bool canSelect = validTargets.TryGetValue(interactable as IHPUIInteractable, out InteractionInfo info) &&
                             info.distance < interactionSelectionRadius &&
                             ProcessSelectFilters(interactable);
            return canSelect && (!SelectOnlyPriorityTarget);
        }

        #endregion


        #region IHPUIInteractor interface

        /// <inheritdoc />
        public void OnTap(HPUITapEventArgs args)
        {
            tapEvent?.Invoke(args);
        }

        /// <inheritdoc />
        public void OnGesture(HPUIGestureEventArgs args)
        {
            gestureEvent?.Invoke(args);
        }

        /// <inheritdoc />
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

        struct InteractionInfo
        {
            public float distance;
            public Vector3 point;
            public Collider collider;

            public InteractionInfo(float distance, Vector3 point, Collider collider) : this()
            {
                this.distance = distance;
                this.point = point;
                this.collider = collider;
            }
        }
    }
}