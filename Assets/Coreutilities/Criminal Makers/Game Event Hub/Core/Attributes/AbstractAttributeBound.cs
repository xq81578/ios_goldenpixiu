using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CriminalMakers.GameEventHub
{
    /// <summary>
    /// An abstract base class for binding methods and fields annotated with a specific attribute.
    ///
    /// Use this for services classes that need to act as glue between different parts of the game.
    /// <br/><br/>
    /// Some examples:
    ///<br/><br/>
    /// - InputService that binds methods to input actions.<br/>
    /// - EventService that binds methods to events that can be reported elsewhere.<br/>
    /// </summary>
    /// <typeparam name="TAttribute">The type of attribute that will be used for binding.</typeparam>
    public abstract class AbstractAttributeBound<TAttribute> : MonoBehaviour where TAttribute : Attribute
    {
        /// <summary>
        /// Registry that holds bindings for methods and fields, keyed by action/event names.
        /// </summary>
        protected readonly Dictionary<string, List<BindingInfo>> BindingRegistry = new Dictionary<string, List<BindingInfo>>();

        /// <summary>
        /// Represents information about a binding, including the member, its attribute, and the subscriber instance.
        ///
        /// This defines what Invoke and FeedFieldValue should do for the specific member type. 
        /// </summary>
        public abstract class BindingInfo
        {
            /// <summary>
            /// Key to identify the binding. Is used in the dictionary to store the binding (same key can have multiple bindings). 
            /// </summary>
            public string BindingKey { get; protected set; }


            /// <summary>
            /// Gets the member info associated with this binding.
            ///
            /// This memberInfo can be a MethodInfo or a FieldInfo.
            /// </summary>
            public MemberInfo MemberInfo { get; }

            /// <summary>
            /// Gets the subscriber object associated with this binding.
            /// </summary>
            public object Subscriber { get; }

            protected BindingInfo(MemberInfo memberInfo, object subscriber)
            {
                MemberInfo = memberInfo;
                Subscriber = subscriber;
            }

            public virtual void Invoke(object[] args = null)
            {
            }
        }

        /// <summary>
        /// Binds methods and fields decorated with the specified attribute on the given subscriber object.
        /// </summary>
        /// <param name="subscriber">The subscriber object containing the methods and fields to bind.</param>
        protected virtual void _Bind(object subscriber)
        {
            var members = subscriber.GetType().GetMembers(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            foreach (var member in members)
            {
                try
                {
                    var attributes = member.GetCustomAttributes(typeof(TAttribute), true);

                    if (attributes.Length <= 0) continue;

                    foreach (var attribute in attributes.Cast<TAttribute>())
                    {
                        if (member is not MethodInfo method) continue; // Ignore fields

                        var bindingInfo = CreateBindingInfo(attribute, method, subscriber);
                        if (bindingInfo == null || string.IsNullOrEmpty(bindingInfo.BindingKey))
                        {
                            continue;
                        }

                        if (!IsAlreadyBound(bindingInfo))
                        {
                            AddBindingInfo(bindingInfo, subscriber);
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"Method {method.Name} is already bound to {subscriber.GetType().Name}. Skipping.");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error binding member {member.Name} on {subscriber.GetType().Name}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Unbinds all methods associated with the given subscriber object.
        /// </summary>
        /// <param name="subscriber">The subscriber object to unbind.</param>
        protected virtual bool _Unbind(object subscriber)
        {
            var wasUnbound = false;
            try
            {
                foreach (var key in BindingRegistry.Keys.ToList())
                {
                    int count = BindingRegistry[key].Count;
                    BindingRegistry[key] = BindingRegistry[key].Where(b => b.Subscriber != subscriber).ToList();

                    if (BindingRegistry[key].Count < count)
                    {
                        wasUnbound = true;
                    }
                    
                    if (BindingRegistry[key].Count == 0)
                    {
                        BindingRegistry.Remove(key);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error unbinding subscriber {subscriber.GetType().Name}: {e.Message}");
            }
            
            return wasUnbound;
        }


        /// <summary>
        /// Adds a binding information entry to the registry.
        ///
        /// It just checks if the action name already exists in the registry and adds the binding info to the list.
        /// </summary>
        /// <param name="bindingInfo">The binding information to add.</param>
        /// <param name="subscriber">The subscriber object associated with the binding.</param>
        protected void AddBindingInfo(BindingInfo bindingInfo, object subscriber)
        {
            if (string.IsNullOrEmpty(bindingInfo.BindingKey))
            {
                Debug.LogError(
                    $"Binding ID is null or empty for {bindingInfo.MemberInfo.Name} on {subscriber.GetType().Name}. Check your constructor to ensure an ID is issued.");
                return;
            }

            if (BindingRegistry.ContainsKey(bindingInfo.BindingKey))
            {
                BindingRegistry[bindingInfo.BindingKey].Add(bindingInfo);
            }
            else
            {
                BindingRegistry[bindingInfo.BindingKey] = new List<BindingInfo> { bindingInfo };
            }
        }

        /// <summary>
        /// Removes binding information from the internal registry based on the provided binding information.
        /// </summary>
        /// <param name="bindingInfo">The binding information to be removed from the registry.</param>
        protected void RemoveBindingInfo(BindingInfo bindingInfo)
        {
            if (string.IsNullOrEmpty(bindingInfo.BindingKey))
            {
                Debug.LogError(
                    $"Binding ID is null or empty for {bindingInfo.MemberInfo.Name}");
                return;
            }

            if (BindingRegistry.TryGetValue(bindingInfo.BindingKey, out var bindings))
            {
                bindings.Remove(bindingInfo);
            }

            if (bindings != null && bindings.Count == 0)
            {
                BindingRegistry.Remove(bindingInfo.BindingKey);
            }
        }

        private bool IsAlreadyBound(BindingInfo bindingInfo)
        {
            if (string.IsNullOrEmpty(bindingInfo.BindingKey))
            {
                Debug.LogError(
                    $"Binding ID is null or empty for {bindingInfo.MemberInfo.Name}");
                return false;
            }

            if (BindingRegistry.TryGetValue(bindingInfo.BindingKey, out var bindings))
            {
                return bindings.Any(b =>
                    b.MemberInfo == bindingInfo.MemberInfo && b.Subscriber == bindingInfo.Subscriber);
            }

            return false;
        }

        /// <summary>
        /// Creates binding information for a given attribute, method, and subscriber.
        /// </summary>
        /// <param name="attribute">The attribute associated with the binding.</param>
        /// <param name="memberInfo">The method info associated with the binding.</param>
        /// <param name="subscriber">The subscriber object associated with the binding.</param>
        /// <returns>A new instance of <see cref="BindingInfo"/> for the specified parameters.</returns>
        protected abstract BindingInfo CreateBindingInfo(TAttribute attribute, MethodInfo memberInfo,
            object subscriber);
    }
}