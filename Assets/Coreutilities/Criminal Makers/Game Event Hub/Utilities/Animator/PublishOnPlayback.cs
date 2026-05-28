using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CriminalMakers.GameEventHub.Utilities
{
    public class PublishOnPlayback : StateMachineBehaviour
    {
        private const float PlaybackTimeThreshold = 0.05f;

        public List<PlaybackEvent> playbackEvents = new List<PlaybackEvent>();
        
        private int _completedLoopCount;
        private readonly Dictionary<GameEventSO, FieldInfo[]> _cachedFieldInfo = new Dictionary<GameEventSO, FieldInfo[]>();

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            _completedLoopCount = 0;
            CacheEventFields();
            playbackEvents.ForEach(playbackEvent => playbackEvent.lastTriggeredLoop = -1);
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // Calculate the current normalized time within one cycle (0 to 1)
            float currentAnimationTime = stateInfo.normalizedTime % 1;

            // Update the loop count when the animation finishes one cycle
            if (currentAnimationTime < PlaybackTimeThreshold && (int)stateInfo.normalizedTime > _completedLoopCount)
            {
                _completedLoopCount = (int)stateInfo.normalizedTime;
            }


            foreach (var playbackEvent in playbackEvents)
            {
                if (playbackEvent.lastTriggeredLoop == _completedLoopCount) continue;

                // Trigger event if the current time is close to playbackTime
                if (Mathf.Abs(currentAnimationTime - playbackEvent.playbackTime) <= PlaybackTimeThreshold)
                {
                    PublishEventWithProperties(animator, playbackEvent);
                }
            }
        }

        /// <summary>
        /// Caches the field information of GameEventSO instances associated with PlaybackEvents.
        /// This method iterates through all PlaybackEvents and checks if their associated GameEventSO
        /// instances have already been cached. If not, it uses reflection to retrieve fields of type
        /// AnimatorControllerParameter[] and stores them in a dictionary for future use.
        /// </summary>
        private void CacheEventFields()
        {
            foreach (var playbackEvent in playbackEvents)
            {
                if (playbackEvent.associatedEvent != null && !_cachedFieldInfo.ContainsKey(playbackEvent.associatedEvent))
                {
                    var fields = GetFieldsOfType<Animator>(playbackEvent.associatedEvent.GameEvent);
                    _cachedFieldInfo[playbackEvent.associatedEvent] = fields;
                }
            }
        }

        /// <summary>
        /// Publishes the associated event of a <see cref="PlaybackEvent"/> with its properties.
        /// This method ensures that the event's properties are set before invoking its publication.
        /// Additionally, it updates the loop count of the playback event to prevent multiple
        /// triggers within the same cycle.
        /// </summary>
        /// <param name="animator">The Animator instance driving the playback and associated parameters.</param>
        /// <param name="playbackEvent">The playback event containing the event to be published and its trigger time.</param>
        private void PublishEventWithProperties(Animator animator, PlaybackEvent playbackEvent)
        {
            if (playbackEvent.associatedEvent == null)
            {
                Debug.LogWarning($"Playback event at time {playbackEvent.playbackTime} has no associated GameEventSO. Skipping", this);
                return;
            }
            
            // If the event has AnimatorControllerParameter[] fields, set the fields using Animator.parameters
            SetEventParameters(animator, playbackEvent);

            // Trigger the event
            playbackEvent.associatedEvent.FeedEventProperties();
            playbackEvent.associatedEvent.Publish(animator);


            // Update the last triggered loop
            playbackEvent.lastTriggeredLoop = _completedLoopCount;
        }

        /// <summary>
        /// Sets the parameters of a PlaybackEvent's associated GameEventSO using the Animator's parameters.
        /// This method retrieves pre-cached field metadata for the GameEventSO instance and sets the
        /// values of those fields to AnimatorControllerParameter[] from the Animator.
        /// </summary>
        /// <param name="animator">The Animator instance whose parameters will be used to set the GameEventSO fields.</param>
        /// <param name="playbackEvent">The PlaybackEvent instance containing the associated GameEventSO to update.</param>
        private void SetEventParameters(Animator animator, PlaybackEvent playbackEvent)
        {
            if (!_cachedFieldInfo.TryGetValue(playbackEvent.associatedEvent, out var parameterFields)) return;
            
            foreach (var parameterField in parameterFields)
            {
                parameterField.SetValue(playbackEvent.associatedEvent.GameEvent, animator);
            }
        }

        /// <summary>
        /// Retrieves all fields of a specified type from an object using reflection.
        /// The method searches through the fields of the object's class, including private,
        /// public, static, and instance fields, that match the specified type.
        /// </summary>
        /// <typeparam name="TFieldType">The type of fields to be retrieved.</typeparam>
        /// <param name="gameEvent">The object whose fields are to be searched.</param>
        /// <returns>An array of FieldInfo objects representing the fields of the specified type.</returns>
        private FieldInfo[] GetFieldsOfType<TFieldType>(object gameEvent)
        {
            // Use reflection to find all fields
            return gameEvent.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                                 BindingFlags.Static)
                .Where(field => field.FieldType == typeof(TFieldType))
                .ToArray();
        }
    }
}