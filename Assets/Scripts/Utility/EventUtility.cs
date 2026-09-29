using System;
using System.Collections.Generic;
using ThreeSouls.Interface;
using UnityEngine;

namespace ThreeSouls.Utility
{
    public static class EventUtility
    {
        private static readonly Dictionary<Type, List<IEventListenerBase>> EventListenerContainer = new();

        public static void AddListener<T>(IEventListener<T> listener) where T : IEventValue
        {
            var eventType = typeof(T);

            if (EventListenerContainer.TryGetValue(eventType, out var listeners) == false)
            {
                listeners = new List<IEventListenerBase>();
                EventListenerContainer.Add(eventType, listeners);
            }

            if (listeners.Contains(listener))
            {
                return;
            }

            listeners.Add(listener);
            listeners.Sort((a, b) => ((IEventListener<T>)a).CallbackOrder.CompareTo(((IEventListener<T>)b).CallbackOrder));
        }

        public static void RemoveListener<T>(IEventListener<T> listener) where T : IEventValue
        {
            var eventType = typeof(T);

            if (EventListenerContainer.ContainsKey(eventType) == false)
            {
                return;
            }

            var list = EventListenerContainer[eventType];

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != listener)
                {
                    continue;
                }

                list.Remove(list[i]);

                if (list.Count == 0)
                {
                    EventListenerContainer.Remove(eventType);
                }

                return;
            }
        }

        public static void AllRemoveListener<T>() where T : IEventListenerBase
        {
            var eventType = typeof(T);

            if (EventListenerContainer.ContainsKey(eventType) == false)
            {
                return;
            }

            EventListenerContainer[eventType].Clear();
            EventListenerContainer.Remove(eventType);
        }

        public static void TriggerEvent<TSender, TEvent>(TSender sender, TEvent eventValue)
          where TSender : class
          where TEvent : IEventValue
        {
            if (EventListenerContainer.TryGetValue(typeof(TEvent), out var listeners) == false)
            {
                return;
            }

            for (var i = 0; i < listeners.Count; i++)
            {
                ((IEventListener<TEvent>)listeners[i]).OnEvent(sender, eventValue);
            }
        }

        public static class EventRegister
        {
            public static void AddListener<T>(IEventListener<T> listener) where T : IEventValue
            {
                EventUtility.AddListener(listener);
            }

            public static void RemoveListener<T>(IEventListener<T> listener) where T : IEventValue
            {
                EventUtility.RemoveListener(listener);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            EventListenerContainer.Clear();
        }
    }
}
