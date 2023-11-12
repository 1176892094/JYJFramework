// *********************************************************************************
// # Project: JFramework
// # Unity: 2022.3.5f1c1
// # Author: Charlotte
// # Version: 1.0.0
// # History: 2023-10-24  23:39
// # Copyright: 2023, Charlotte
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
    using Components = Dictionary<Type, ScriptableObject>;

    /// <summary>
    /// 控制器管理器
    /// </summary>
    internal static class ControllerManager
    {
        /// <summary>
        /// 全局控制器容器
        /// </summary>
        public static readonly Dictionary<IEntity, Components> characters = new Dictionary<IEntity, Components>();

        /// <summary>
        /// 注册并返回控制器
        /// </summary>
        /// <param name="character"></param>
        /// <param name="action"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static void Register<T>(IEntity character, Action<T> action) where T : ScriptableObject, IController
        {
            if (!characters.TryGetValue(character, out Components components))
            {
                components = new Components();
                characters.Add(character, components);
            }

            if (!components.ContainsKey(typeof(T)))
            {
                var component = ScriptableObject.CreateInstance<T>();
                components.Add(typeof(T), component);
                component.Register(character);
                action?.Invoke(component);
            }
        }

        /// <summary>
        /// 获取控制器
        /// </summary>
        /// <param name="character"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Get<T>(IEntity character) where T : ScriptableObject, IController
        {
            if (!characters.TryGetValue(character, out Components components))
            {
                return default;
            }

            if (!components.ContainsKey(typeof(T)))
            {
                return default;
            }

            return (T)characters[character][typeof(T)];
        }

        /// <summary>
        /// 销毁指定控制器
        /// </summary>
        /// <param name="character"></param>
        public static void UnRegister(IEntity character)
        {
            if (characters.TryGetValue(character, out Components components))
            {
                foreach (var component in components.Values)
                {
                    Object.Destroy(component);
                }

                components.Clear();
                characters.Remove(character);
            }
        }

        /// <summary>
        /// 销毁所有控制器
        /// </summary>
        internal static void Clear()
        {
            var copies = characters.Keys.ToList();
            foreach (var character in copies)
            {
                character.UnRegister();
            }

            characters.Clear();
        }
    }
}