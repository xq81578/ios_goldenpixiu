using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CriminalMakers.GameEventHub.Utilities
{
    public static class StateMachineBehaviourHelper
    {
        /// <summary>
        /// Finds the animation clip associated with a specific StateMachineBehaviour in the given Animator.
        /// </summary>
        /// <param name="animator">The Animator containing the state machine where the behaviour is located.</param>
        /// <param name="behavior">The StateMachineBehaviour to search for within the Animator.</param>
        /// <returns>The AnimationClip linked to the provided StateMachineBehaviour, or null if no matching clip is found.</returns>
        public static AnimationClip FindClipForBehavior(Animator animator, StateMachineBehaviour behavior)
        {
            var controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null) return null;

            foreach (var layer in controller.layers)
            {
                foreach (var state in layer.stateMachine.states)
                {
                    if (state.state.behaviours.Length == 0) continue;

                    foreach (var behaviour in state.state.behaviours)
                    {
                        if (AreStateBehavioursEqual(behaviour, behavior))
                        {
                            return state.state.motion as AnimationClip;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the animation clip linked to a given StateMachineBehaviour by searching through the scene's active Animators.
        /// </summary>
        /// <param name="behavior">The StateMachineBehaviour to locate within the Animator hierarchy.</param>
        /// <returns>The AnimationClip associated with the specified StateMachineBehaviour, or null if no matching clip is found.</returns>
        public static AnimationClip FindClipForBehavior(StateMachineBehaviour behavior)
        {
            // Go up the Unity object hierarchy to locate the Animator
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Animator[] animators = root.GetComponentsInChildren<Animator>();
                foreach (var animator in animators)
                {
                    // Access the AnimatorController
                    var controller = animator.runtimeAnimatorController as AnimatorController;
                    if (controller == null) continue;

                    foreach (var layer in controller.layers)
                    {
                        foreach (var state in layer.stateMachine.states)
                        {
                            if (state.state.behaviours.Length == 0) continue;

                            foreach (var behaviour in state.state.behaviours)
                            {
                                if (AreStateBehavioursEqual(behaviour, behavior))
                                {
                                    return state.state.motion as AnimationClip;
                                }
                            }
                        }
                    }
                }
            }

            return null; // Return null if no Animator found
        }

        /// <summary>
        /// Compares two StateMachineBehaviour instances to determine if they are equal.
        /// </summary>
        /// <param name="behaviour">The first StateMachineBehaviour to compare.</param>
        /// <param name="stateBehaviour">The second object, expected to be a StateMachineBehaviour, to compare with the first.</param>
        /// <returns>True if both StateMachineBehaviour instances have the same Instance ID; otherwise, false.</returns>
        private static bool AreStateBehavioursEqual(StateMachineBehaviour behaviour, object stateBehaviour)
        {
            if (behaviour == null || stateBehaviour == null) return false;

            // Compare instance IDs if both objects exist
            if (behaviour.GetInstanceID() == ((StateMachineBehaviour)stateBehaviour).GetInstanceID())
            {
                return true;
            }

            return false;
        }
    }
}