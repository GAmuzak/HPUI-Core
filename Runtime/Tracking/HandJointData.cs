using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace ubco.ovi.HPUI.Core
{
    /// <summary>
    /// Utility class to expose joint data accessible.
    /// </summary>
    public class HandJointData: MonoBehaviour
    {
        private static HandJointData instance;

        public static HandJointData Instance {
            get {
                if (instance == null)
                {
                    HandJointData obj = FindObjectOfType<HandJointData>();
                    if (obj == null)
                    {
                        throw new InvalidOperationException("There are no valid `HandJointData` setup in the scene!");
                    }
                    instance = obj;
                }
                return instance;
            }
            set => instance = value;
        }

        private XRHandSubsystem handSubsystem;
        private List<JointDataEventHandler> jointDataEvents;
        private bool initialized;

        #region Unity events
        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        protected void Start()
        {
            if (!initialized)
            {
                Initialize();
            }
        }

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        protected void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Debug.LogError("HandJointDta.Instance already set. Destorying this object.");
                Destroy(this);
            }
        }

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        protected void OnDisable()
        {
            if (handSubsystem != null)
            {
                handSubsystem.trackingAcquired -= OnTrackingAcquired;
                handSubsystem.trackingLost -= OnTrackingLost;
                handSubsystem.updatedHands -= OnUpdatedHands;
                handSubsystem = null;
            }
        }

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        protected void Update()
        {
            if (handSubsystem != null && handSubsystem.running)
            {
                return;
            }
            
            List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(handSubsystems);
            bool foundRunningHandSubsystem = false;
            for (var i = 0; i < handSubsystems.Count; ++i)
            {
                XRHandSubsystem handSubsystem = handSubsystems[i];
                if (handSubsystem.running)
                {
                    UnsubscribeHandSubsystem();
                    this.handSubsystem = handSubsystem;
                    foundRunningHandSubsystem = true;
                    break;
                }
            }

            if (!foundRunningHandSubsystem)
            {
                return;
            }
            
            SubscribeHandSubsystem();
        }
        #endregion

        #region event handling
        private void Initialize()
        {
            // Initializing all the hand joint events
            jointDataEvents = new List<JointDataEventHandler>();

            for (int i = XRHandJointIDUtility.ToIndex(XRHandJointID.BeginMarker); i < XRHandJointIDUtility.ToIndex(XRHandJointID.EndMarker); i++)
            {
                jointDataEvents.Add(new JointDataEventHandler(XRHandJointIDUtility.FromIndex(i), Handedness.Left));
                jointDataEvents.Add(new JointDataEventHandler(XRHandJointIDUtility.FromIndex(i), Handedness.Right));
            }

            initialized = true;
        }

        /// <summary>
        /// Subscribe to events on the <see cref="XRHandSubsystem"/>
        /// </summary>
        private void SubscribeHandSubsystem()
        {
            if (handSubsystem == null)
                return;

            handSubsystem.trackingAcquired += OnTrackingAcquired;
            handSubsystem.trackingLost += OnTrackingLost;
            handSubsystem.updatedHands += OnUpdatedHands;
        }

        /// <summary>
        /// Unsubscribe from events on the <see cref="XRHandSubsystem"/>
        /// </summary>
        private void UnsubscribeHandSubsystem()
        {
            if (handSubsystem == null)
                return;

            handSubsystem.trackingAcquired -= OnTrackingAcquired;
            handSubsystem.trackingLost -= OnTrackingLost;
            handSubsystem.updatedHands -= OnUpdatedHands;
        }

        /// <summary>
        /// Event callback for <see cref="XRHandSubsystem.updatedHands"/>
        /// </summary>
        private void OnUpdatedHands(XRHandSubsystem subsystem,
                                    XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
                                    XRHandSubsystem.UpdateType updateType)
        {
            switch (updateType)
            {
                case XRHandSubsystem.UpdateType.Dynamic:
                    // Update game logic that uses hand data
                    break;
                case XRHandSubsystem.UpdateType.BeforeRender:
                    foreach (JointDataEventHandler handler in jointDataEvents)
                    {
                        if (handler.handedness == Handedness.Left)
                        {
                            handler.ProcessEvent(subsystem.leftHand);
                        }
                        else if (handler.handedness == Handedness.Right)
                        {
                            handler.ProcessEvent(subsystem.rightHand);
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Event callback for <see cref="XRHandSubsystem.trackingAcquired"/>
        /// </summary>
        private void OnTrackingAcquired(XRHand hand)
        {
        }

        /// <summary>
        /// Event callback for <see cref="XRHandSubsystem.trackingLost"/>
        /// </summary>
        private void OnTrackingLost(XRHand hand)
        {
        }
        #endregion

        #region public interface
        /// <summary>
        /// Subscribe to a joint on a hand
        /// </summary>
        public void SubscribeToJointDataEvent(Handedness handedness, XRHandJointID jointID, EventHandler<JointDataEventArgs> callback)
        {
            if (!initialized)
            {
                Initialize();
            }

            foreach(JointDataEventHandler handler in jointDataEvents)
            {
                if (handler.handedness == handedness && handler.jointID == jointID)
                {
                    handler.jointDataEventHandler += callback;
                }
            }
        }

        /// <summary>
        /// Unsubscribe to a joint on a hand
        /// </summary>
        public void UnsubscribeToJointDataEvent(Handedness handedness, XRHandJointID joinID, EventHandler<JointDataEventArgs> callback)
        {
            if (!initialized)
            {
                Initialize();
            }

            foreach(JointDataEventHandler handler in jointDataEvents)
            {
                if (handler.handedness == handedness && handler.jointID == joinID)
                {
                    handler.jointDataEventHandler += callback;
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// Event args for joint data
    /// </summary>
    public class JointDataEventArgs: EventArgs
    {
        public Pose pose;
        public Handedness handedness;
        public XRHandJointID jointID;

        public JointDataEventArgs(Pose pose, Handedness handedness, XRHandJointID jointID)
        {
            this.pose = pose;
            this.handedness = handedness;
            this.jointID = jointID;
        }
    }

    /// <summary>
    /// Helper to push the pose data for a joint
    /// </summary>
    internal class JointDataEventHandler
    {
        public XRHandJointID jointID;
        public Handedness handedness;
        public EventHandler<JointDataEventArgs> jointDataEventHandler;

        public JointDataEventHandler(XRHandJointID jointID, Handedness handedness)
        {
            this.jointID = jointID;
            this.handedness = handedness;
        }

        public void ProcessEvent(XRHand hand)
        {
            EventHandler<JointDataEventArgs> handler = jointDataEventHandler;
            if (handler != null)
            {
                XRHandJoint joint = hand.GetJoint(jointID);
                
                if (joint.TryGetPose(out Pose resultingPose))
                {
                    handler(this, new JointDataEventArgs(resultingPose, handedness, jointID));
                }
            }
        }
    }
}