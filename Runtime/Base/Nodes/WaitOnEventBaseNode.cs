using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Xprees.EditorTools.Attributes.ReadOnly;
using Xprees.Events.ScriptableObjects.Base;
using Xprees.Graph.Core.Attributes;
using Xprees.Variables.Reference.Primitive;

namespace Xprees.Graph.Core.Base.Nodes
{
    [NodeTint("#68427B")]
    [NodeResizableWidth(300)]
    public abstract class WaitOnEventBaseNode : WaitBaseNode
    {
        [Header("Settings")]
        [Tooltip("If true, the node will accept events. If false, the node will ignore events.")]
        public BoolReference acceptingEvents = new(true);

        [Tooltip("If true, node will ignore all events before it was triggered. "
                 + "Otherwise can skip waiting if the event already happened before it was triggered.")]
        public BoolReference triggerActivated = new(false);

        [Header("Internal info")]
        [ReadOnly] [SerializeField] protected bool isEventRaised = false;

        private CancellationTokenSource _cts;

        protected private void OnEventRaised()
        {
            if (!IsActive) return; // Don't trigger inactive node -> in inactive graph
            if (!acceptingEvents) return; // Don't trigger node if user doesn't want to accept events

            isEventRaised = true;
        }

        protected override async UniTask Wait(CancellationToken cancellationToken = default)
        {
            if (triggerActivated) ResetState(); // Reset state to ignore any events before the node is triggered
            await UniTask.WaitUntil(CanMoveOn, cancellationToken: cancellationToken);
            ResetState(); // Reset state after the event is raised to allow the node to be reused
        }

        /// Override this method to add custom logic to add condition when the node can move on
        protected virtual bool CanMoveOn() => isEventRaised;

        #region Housekeeping

        protected override void Init()
        {
            base.Init();
            SetupEvents();
            ResetState();
        }

        protected virtual void OnDisable() => CleanupEvents();

        /// Use this method to set up any event subscriptions or listeners (called in Init aka onEnable)
        protected virtual void SetupEvents()
        {
            // Subscribe to events here if needed
        }

        /// Use this method to clean up any event subscriptions or listeners (usually called onDisable)
        protected virtual void CleanupEvents()
        {
            // Cleanup event listeners here if needed
        }

        public override void BackupStartState()
        {
            base.BackupStartState();
            acceptingEvents?.BackupStartState();
            triggerActivated?.BackupStartState();
        }

        public override void ResetState()
        {
            base.ResetState();
            isEventRaised = false;
            acceptingEvents?.ResetState();
            triggerActivated?.ResetState();
        }

        #endregion

    }

    public abstract class WaitOnEventBaseNode<T> : WaitOnEventBaseNode
    {
        [Header("Listens to")]
        public EventChannelBaseSO<T> eventChannel;

        protected T eventData;

        protected override void SetupEvents()
        {
            base.SetupEvents();
            if (eventChannel) eventChannel.onEventRaised += OnEventRaised;
        }

        protected override void CleanupEvents()
        {
            base.CleanupEvents();
            if (eventChannel) eventChannel.onEventRaised -= OnEventRaised;
        }

        protected virtual void OnEventRaised(T data)
        {
            eventData = data;
            OnEventRaised();
        }

        public override void ResetState()
        {
            base.ResetState();
            eventChannel?.ResetState();
            eventData = default;
        }
    }

    public abstract class WaitOnEventBaseNode<T1, T2> : WaitOnEventBaseNode
    {
        [Header("Listens to")]
        public EventChannelBaseSO<T1, T2> eventChannel;

        protected Tuple<T1, T2> eventData;

        protected override void SetupEvents()
        {
            base.SetupEvents();
            if (eventChannel) eventChannel.onEventRaised += OnEventRaised;
        }

        protected override void CleanupEvents()
        {
            base.CleanupEvents();
            if (eventChannel) eventChannel.onEventRaised -= OnEventRaised;
        }

        protected virtual void OnEventRaised(T1 first, T2 second)
        {
            eventData = new Tuple<T1, T2>(first, second);
            OnEventRaised();
        }

        public override void ResetState()
        {
            base.ResetState();
            eventChannel?.ResetState();
            eventData = default;
        }
    }

    public abstract class WaitOnEventBaseNode<T1, T2, T3> : WaitOnEventBaseNode
    {
        [Header("Listens to")]
        public EventChannelBaseSO<T1, T2, T3> eventChannel;

        protected Tuple<T1, T2, T3> eventData;

        protected override void SetupEvents()
        {
            base.SetupEvents();
            if (eventChannel) eventChannel.onEventRaised += OnEventRaised;
        }

        protected override void CleanupEvents()
        {
            base.CleanupEvents();
            if (eventChannel) eventChannel.onEventRaised -= OnEventRaised;
        }

        protected virtual void OnEventRaised(T1 first, T2 second, T3 third)
        {
            eventData = new Tuple<T1, T2, T3>(first, second, third);
            OnEventRaised();
        }

        public override void ResetState()
        {
            base.ResetState();
            eventChannel?.ResetState();
            eventData = default;
        }
    }
}