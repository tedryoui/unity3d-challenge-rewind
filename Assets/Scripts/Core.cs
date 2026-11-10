using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DefaultNamespace
{
    [AutoStaticsCleanup]
    public static partial class Core
    {
        private static Dictionary<string, Action<Object>> _onObjectInstantiate;

        [OnEnteringPlayMode]
        public static void OnCreate()
        {
            _onObjectInstantiate = new Dictionary<string, Action<Object>>();
        }

        public static void TrackOnObjectInstantiate(string identity, Action<Object> onObjectInstantiate)
        {
            if (_onObjectInstantiate is null)
                return;

            if (_onObjectInstantiate.ContainsKey(identity))
                return;
            
            _onObjectInstantiate.Add(identity, onObjectInstantiate);
        }

        public static void UnTrackOnObjectInstantiate(string identity, Action<Object> onObjectInstantiate)
        {
            if (_onObjectInstantiate is null)
                return;
            
            if (_onObjectInstantiate.ContainsKey(identity))
                _onObjectInstantiate.Remove(identity);
        }
        
        public static T Instantiate<T>(T original) where T : Object
        {
            T instance = Object.Instantiate(original);
            ObObjectInstantiate(instance);
            return instance;
        }

        public static T Instantiate<T>(T original, Transform parent) where T : Object
        {
            T instance = Object.Instantiate(original, parent);
            ObObjectInstantiate(instance);
            return instance;
        }

        public static T Instantiate<T>(T original, Transform parent, bool instantiateInWorldSpace) where T : Object
        {
            T instance = Object.Instantiate(original, parent, instantiateInWorldSpace);
            ObObjectInstantiate(instance);
            return instance;
        }

        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object
        {
            T instance = Object.Instantiate(original, position, rotation);
            ObObjectInstantiate(instance);
            return instance;
        }

        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent) where T : Object
        {
            T instance = Object.Instantiate(original, position, rotation, parent);
            ObObjectInstantiate(instance);
            return instance;
        }

        private static void ObObjectInstantiate<T>(T instance) where T : Object
        {
            foreach (var onObjectInstantiate in _onObjectInstantiate)
                onObjectInstantiate.Value?.Invoke(instance);
        }
    }
}