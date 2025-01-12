// *********************************************************************************
// # Project: JFramework
// # Unity: 6000.3.5f1
// # Author: 云谷千羽
// # Version: 1.0.0
// # History: 2024-12-19 03:12:36
// # Recently: 2024-12-22 20:12:33
// # Copyright: 2024, 云谷千羽
// # Description: This is an automatically generated comment.
// *********************************************************************************

using System;
using System.Collections.Generic;
using JFramework.Net;
using Mono.Cecil;
using UnityEngine;

namespace JFramework.Editor
{
    internal class Process
    {
        private bool failed;
        private Module module;
        private Writer writer;
        private Reader reader;
        private SyncVarAccess access;
        private TypeDefinition process;
        private AssemblyDefinition assembly;
        private readonly Logger logger;

        public Process(Logger logger)
        {
            this.logger = logger;
        }

        public bool Execute(AssemblyDefinition assembly, IAssemblyResolver resolver, out bool change)
        {
            failed = false;
            change = false;
            try
            {
                this.assembly = assembly;

                foreach (var type in assembly.MainModule.GetTypes())
                {
                    if (type.Namespace == Const.GEN_TYPE && type.Name == Const.GEN_NAME)
                    {
                        return true;
                    }
                }

                access = new SyncVarAccess();
                module = new Module(assembly, logger, ref failed);
                process = new TypeDefinition(Const.GEN_TYPE, Const.GEN_NAME, Const.GEN_ATTRS, module.Import<object>());
                writer = new Writer(assembly, module, process, logger);
                reader = new Reader(assembly, module, process, logger);
                change = RuntimeAttribute.Process(assembly, resolver, logger, writer, reader, ref failed);
                
                var mainModule = assembly.MainModule;
                
                change |= ProcessModule(mainModule);
                if (failed)
                {
                    return false;
                }
                
                if (change)
                {
                    SyncVarReplace.Process(mainModule, access);
                    mainModule.Types.Add(process);
                    RuntimeAttribute.RuntimeInitializeOnLoad(assembly, module, writer, reader, process);
                }

                return true;
            }
            catch (Exception e)
            {
                failed = true;
                logger.Error(e.ToString());
                return false;
            }
        }

        /// <summary>
        /// 处理 NetworkBehaviour
        /// </summary>
        /// <param name="td"></param>
        /// <param name="failed"></param>
        /// <returns></returns>
        private bool ProcessNetworkBehavior(TypeDefinition td, ref bool failed)
        {
            if (!td.IsClass) return false;
            if (!td.IsDerivedFrom<NetworkBehaviour>())
            {
                if (td.IsDerivedFrom<MonoBehaviour>())
                {
                    MonoBehaviourProcess.Process(td, logger, ref failed);
                }

                return false;
            }
            var behaviours = new List<TypeDefinition>();

            TypeDefinition parent = td;
            while (parent != null)
            {
                if (parent.Is<NetworkBehaviour>())
                {
                    break;
                }

                try
                {
                    behaviours.Insert(0, parent);
                    parent = parent.BaseType.Resolve();
                }
                catch (AssemblyResolutionException)
                {
                    break;
                }
            }

            bool changed = false;
            foreach (TypeDefinition behaviour in behaviours)
            {
                changed |= new NetworkBehaviourProcess(assembly, access, module, writer, reader, logger, behaviour).Process(ref failed);
            }

            return changed;
        }

        /// <summary>
        /// 处理功能
        /// </summary>
        /// <param name="md"></param>
        /// <returns></returns>
        private bool ProcessModule(ModuleDefinition md)
        {
            bool result = false;
            foreach (var td in md.Types)
            {
                if (td.IsClass && td.BaseType.IsResolve())
                {
                    result |= ProcessNetworkBehavior(td, ref failed);
                }
            }

            return result;
        }

        /// <summary>
        /// 处理方法中的参数
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="md"></param>
        /// <returns></returns>
        public static string GenerateMethodName(string prefix, MethodDefinition md)
        {
            prefix = md.Name + prefix;
            // return md.Parameters.Aggregate(prefix, (s, parameter) => s + "_" + NetworkManager.GetStableId(parameter.ParameterType.Name));
            return prefix;
        }
    }
}