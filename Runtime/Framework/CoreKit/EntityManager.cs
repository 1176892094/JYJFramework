// *********************************************************************************
// # Project: Test
// # Unity: 2022.3.5f1c1
// # Author: Charlotte
// # Version: 1.0.0
// # History: 2024-02-04  14:32
// # Copyright: 2024, Charlotte
// # Description: This is an automatically generated comment.
// *********************************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using JFramework.Interface;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JFramework.Core
{
    internal static class EntityManager
    {
        public static readonly Dictionary<IEntity, Dictionary<Type, IComponent>> entities = new();
        public static IEntity instance;

        public static IComponent FindComponent(IEntity entity, Type type)
        {
            if (!GlobalManager.Instance) return null;
            if (!entities.TryGetValue(entity, out var components))
            {
                components = new Dictionary<Type, IComponent>();
                entities.Add(entity, components);
            }

            if (!components.TryGetValue(type, out var component))
            {
                instance = entity;
                component = (IComponent)ScriptableObject.CreateInstance(type);
                ((ScriptableObject)component).name = type.Name;
                components.Add(type, component);
                component.OnAwake(instance);
            }

            return entities[entity][type];
        }

        public static void Destroy(IEntity entity)
        {
            if (!GlobalManager.Instance) return;
            if (entities.TryGetValue(entity, out var components))
            {
                foreach (var component in components.Values)
                {
                    Object.Destroy((ScriptableObject)component);
                }

                components.Clear();
                entities.Remove(entity);
            }
        }

        public static void UnRegister()
        {
            var copies = entities.Keys.ToList();
            foreach (var entity in copies)
            {
                Destroy(entity);
            }

            entities.Clear();
            instance = null;
        }
    }
}