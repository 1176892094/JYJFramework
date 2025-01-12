// *********************************************************************************
// # Project: Forest
// # Unity: 6000.3.5f1
// # Author: 云谷千羽
// # Version: 1.0.0
// # History: 2025-01-12 15:01:52
// # Recently: 2025-01-12 15:01:52
// # Copyright: 2024, 云谷千羽
// # Description: This is an automatically generated comment.
// *********************************************************************************

using System;
using System.Collections.Generic;
using JFramework.Net;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;

namespace JFramework.Editor
{
    using FieldDefinitionPair = KeyValuePair<List<FieldDefinition>, Dictionary<FieldDefinition, FieldDefinition>>;

    internal class SyncVarProcess
    {
        private readonly Logger logger;
        private readonly Module module;
        private readonly SyncVarAccess access;
        private readonly AssemblyDefinition assembly;

        public SyncVarProcess(AssemblyDefinition assembly, SyncVarAccess access, Module module, Logger logger)
        {
            this.logger = logger;
            this.access = access;
            this.module = module;
            this.assembly = assembly;
        }

        /// <summary>
        /// 从挂钩方法中生成新的方法
        /// </summary>
        /// <param name="syncVar"></param>
        /// <param name="worker"></param>
        /// <param name="hookMethod"></param>
        public void GenerateNewActionFromHookMethod(FieldDefinition syncVar, ILProcessor worker, MethodDefinition hookMethod)
        {
            worker.Emit(hookMethod.IsStatic ? OpCodes.Ldnull : OpCodes.Ldarg_0);
            MethodReference hookMethodRef;
            if (hookMethod.DeclaringType.HasGenericParameters)
            {
                var param = new TypeReference[hookMethod.DeclaringType.GenericParameters.Count];
                for (int i = 0; i < param.Length; i++)
                {
                    param[i] = hookMethod.DeclaringType.GenericParameters[i];
                }

                var instanceType = hookMethod.DeclaringType.MakeGenericInstanceType(param);
                hookMethodRef = hookMethod.MakeHostInstanceGeneric(hookMethod.Module, instanceType);
            }
            else
            {
                hookMethodRef = hookMethod;
            }

            if (hookMethod.IsVirtual)
            {
                worker.Emit(OpCodes.Dup);
                worker.Emit(OpCodes.Ldvirtftn, hookMethodRef);
            }
            else
            {
                worker.Emit(OpCodes.Ldftn, hookMethodRef);
            }

            var actionRef = assembly.MainModule.ImportReference(typeof(Action<,>));
            var genericInstance = actionRef.MakeGenericInstanceType(syncVar.FieldType, syncVar.FieldType);
            worker.Emit(OpCodes.Newobj, module.HookMethodRef.MakeHostInstanceGeneric(assembly.MainModule, genericInstance));
        }

        /// <summary>
        /// 获取挂钩方法
        /// </summary>
        /// <param name="td"></param>
        /// <param name="syncVar"></param>
        /// <param name="failed"></param>
        /// <returns></returns>
        public MethodDefinition GetHookMethod(TypeDefinition td, FieldDefinition syncVar, ref bool failed)
        {
            var attribute = syncVar.GetCustomAttribute<SyncVarAttribute>();
            var hookMethod = attribute.GetField<string>();
            if (hookMethod != null)
            {
                return FindHookMethod(td, syncVar, hookMethod, ref failed);
            }

            return null;
        }

        /// <summary>
        /// 寻找挂钩方法
        /// </summary>
        /// <param name="td"></param>
        /// <param name="syncVar"></param>
        /// <param name="hookMethod"></param>
        /// <param name="failed"></param>
        /// <returns></returns>
        private MethodDefinition FindHookMethod(TypeDefinition td, FieldDefinition syncVar, string hookMethod, ref bool failed)
        {
            var methods = td.GetMethods(hookMethod);

            var fixMethods = new List<MethodDefinition>();
            foreach (var method in methods)
            {
                if (method.Parameters.Count == 2)
                {
                    fixMethods.Add(method);
                }
            }

            if (fixMethods.Count == 0)
            {
                logger.Error($"无法注册 {syncVar.Name} 请修改为 {HookMethod(hookMethod, syncVar.FieldType)}", syncVar);
                failed = true;
                return null;
            }

            foreach (var method in fixMethods)
            {
                if (MatchesParameters(syncVar, method))
                {
                    return method;
                }
            }

            logger.Error($"参数类型错误 {syncVar.Name} 请修改为 {HookMethod(hookMethod, syncVar.FieldType)}", syncVar);
            failed = true;
            return null;
        }

        /// <summary>
        /// 挂钩方法的模版
        /// </summary>
        /// <param name="name"></param>
        /// <param name="valueType"></param>
        /// <returns></returns>
        private static string HookMethod(string name, TypeReference valueType) => $"void {name}({valueType} oldValue, {valueType} newValue)";

        /// <summary>
        /// 参数配对
        /// </summary>
        /// <param name="syncVar"></param>
        /// <param name="md"></param>
        /// <returns></returns>
        private static bool MatchesParameters(FieldDefinition syncVar, MethodDefinition md)
        {
            return md.Parameters[0].ParameterType.FullName == syncVar.FieldType.FullName && md.Parameters[1].ParameterType.FullName == syncVar.FieldType.FullName;
        }

        /// <summary>
        /// 处理每个NetworkBehaviour的SyncVar
        /// </summary>
        /// <param name="td"></param>
        /// <param name="failed"></param>
        /// <returns></returns>
        public FieldDefinitionPair ProcessSyncVars(TypeDefinition td, ref bool failed)
        {
            var syncVars = new List<FieldDefinition>();
            var syncVarIds = new Dictionary<FieldDefinition, FieldDefinition>();
            int dirtyBits = access.GetSyncVar(td.BaseType.FullName);

            foreach (var fd in td.Fields)
            {
                if (!fd.HasCustomAttribute<SyncVarAttribute>())
                {
                    continue;
                }

                if ((fd.Attributes & FieldAttributes.Static) != 0)
                {
                    logger.Error($"{fd.Name} 不能是静态字段。", fd);
                    failed = true;
                    continue;
                }

                if (fd.FieldType.IsGenericParameter)
                {
                    logger.Error($"{fd.Name} 不能用泛型参数。", fd);
                    failed = true;
                    continue;
                }

                if (fd.FieldType.IsArray)
                {
                    logger.Error($"{fd.Name} 不能使用数组。", fd);
                    failed = true;
                    continue;
                }

                syncVars.Add(fd);

                ProcessSyncVar(td, fd, syncVarIds, 1L << dirtyBits, ref failed);
                dirtyBits += 1;

                if (dirtyBits > Const.SYNC_LIMIT)
                {
                    logger.Error($"{td.Name} 网络变量数量大于 {Const.SYNC_LIMIT}。", td);
                    failed = true;
                }
            }

            foreach (var fd in syncVarIds.Values)
            {
                td.Fields.Add(fd);
            }

            int parentSyncVarCount = access.GetSyncVar(td.BaseType.FullName);
            access.SetSyncVar(td.FullName, parentSyncVarCount + syncVars.Count);
            return new FieldDefinitionPair(syncVars, syncVarIds);
        }

        /// <summary>
        /// 处理SyncVar
        /// </summary>
        /// <param name="td"></param>
        /// <param name="fd"></param>
        /// <param name="syncVarIds"></param>
        /// <param name="dirtyBits"></param>
        /// <param name="failed"></param>
        private void ProcessSyncVar(TypeDefinition td, FieldDefinition fd, Dictionary<FieldDefinition, FieldDefinition> syncVarIds, long dirtyBits, ref bool failed)
        {
            FieldDefinition objectId = null;
            if (fd.FieldType.IsDerivedFrom<NetworkBehaviour>() || fd.FieldType.Is<NetworkBehaviour>())
            {
                objectId = new FieldDefinition($"{fd.Name}Id", FieldAttributes.Family, module.Import<NetworkVariable>())
                {
                    DeclaringType = td
                };
                syncVarIds[fd] = objectId;
            }
            else if (fd.FieldType.IsNetworkObject())
            {
                objectId = new FieldDefinition($"{fd.Name}Id", FieldAttributes.Family, module.Import<uint>())
                {
                    DeclaringType = td
                };
                syncVarIds[fd] = objectId;
            }

            var get = GenerateSyncVarGetter(fd, fd.Name, objectId);
            var set = GenerateSyncVarSetter(td, fd, fd.Name, dirtyBits, objectId, ref failed);

            var pd = new PropertyDefinition($"Network{fd.Name}", PropertyAttributes.None, fd.FieldType)
            {
                GetMethod = get,
                SetMethod = set
            };

            td.Methods.Add(get);
            td.Methods.Add(set);
            td.Properties.Add(pd);

            access.setter[fd] = set;

            if (fd.FieldType.IsNetworkObject())
            {
                access.getter[fd] = get;
            }
        }

        /// <summary>
        /// 生成SyncVer的Getter
        /// </summary>
        /// <param name="fd"></param>
        /// <param name="originalName"></param>
        /// <param name="netFieldId"></param>
        /// <returns></returns>
        private MethodDefinition GenerateSyncVarGetter(FieldDefinition fd, string originalName, FieldDefinition netFieldId)
        {
            var get = new MethodDefinition($"get_Network{originalName}", Const.VAR_ATTRS, fd.FieldType);

            var worker = get.Body.GetILProcessor();

            var fr = fd.DeclaringType.HasGenericParameters ? fd.MakeHostInstanceGeneric() : fd;

            FieldReference netIdFieldReference = null;
            if (netFieldId != null)
            {
                netIdFieldReference = netFieldId.DeclaringType.HasGenericParameters ? netFieldId.MakeHostInstanceGeneric() : netFieldId;
            }

            if (fd.FieldType.Is<GameObject>())
            {
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldfld, netIdFieldReference);
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, fr);
                worker.Emit(OpCodes.Call, module.getSyncVarGameObject);
                worker.Emit(OpCodes.Ret);
            }
            else if (fd.FieldType.Is<NetworkObject>())
            {
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldfld, netIdFieldReference);
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, fr);
                worker.Emit(OpCodes.Call, module.getSyncVarNetworkObject);
                worker.Emit(OpCodes.Ret);
            }
            else if (fd.FieldType.IsDerivedFrom<NetworkBehaviour>() || fd.FieldType.Is<NetworkBehaviour>())
            {
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldfld, netIdFieldReference);
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, fr);
                var getFunc = module.getSyncVarNetworkBehaviour.MakeGenericInstanceType(assembly.MainModule, fd.FieldType);
                worker.Emit(OpCodes.Call, getFunc);
                worker.Emit(OpCodes.Ret);
            }
            else
            {
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldfld, fr);
                worker.Emit(OpCodes.Ret);
            }

            get.Body.Variables.Add(new VariableDefinition(fd.FieldType));
            get.Body.InitLocals = true;
            get.SemanticsAttributes = MethodSemanticsAttributes.Getter;
            return get;
        }

        /// <summary>
        /// 生成SyncVar的Setter
        /// </summary>
        /// <param name="td"></param>
        /// <param name="fd"></param>
        /// <param name="originalName"></param>
        /// <param name="dirtyBit"></param>
        /// <param name="netFieldId"></param>
        /// <param name="failed"></param>
        /// <returns></returns>
        private MethodDefinition GenerateSyncVarSetter(TypeDefinition td, FieldDefinition fd, string originalName, long dirtyBit, FieldDefinition netFieldId, ref bool failed)
        {
            var set = new MethodDefinition($"set_Network{originalName}", Const.VAR_ATTRS, module.Import(typeof(void)));

            var worker = set.Body.GetILProcessor();
            var fr = fd.DeclaringType.HasGenericParameters ? fd.MakeHostInstanceGeneric() : fd;

            FieldReference netIdFieldReference = null;
            if (netFieldId != null)
            {
                netIdFieldReference = netFieldId.DeclaringType.HasGenericParameters ? netFieldId.MakeHostInstanceGeneric() : netFieldId;
            }

            var endOfMethod = worker.Create(OpCodes.Nop);

            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldarg_1);
            worker.Emit(OpCodes.Ldarg_0);
            worker.Emit(OpCodes.Ldflda, fr);
            worker.Emit(OpCodes.Ldc_I8, dirtyBit);

            var hookMethod = GetHookMethod(td, fd, ref failed);
            if (hookMethod != null)
            {
                GenerateNewActionFromHookMethod(fd, worker, hookMethod);
            }
            else
            {
                worker.Emit(OpCodes.Ldnull);
            }

            if (fd.FieldType.Is<GameObject>())
            {
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, netIdFieldReference);
                worker.Emit(OpCodes.Call, module.syncVarSetterGameObject);
            }
            else if (fd.FieldType.Is<NetworkObject>())
            {
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, netIdFieldReference);
                worker.Emit(OpCodes.Call, module.syncVarSetterNetworkObject);
            }
            else if (fd.FieldType.IsDerivedFrom<NetworkBehaviour>() || fd.FieldType.Is<NetworkBehaviour>())
            {
                worker.Emit(OpCodes.Ldarg_0);
                worker.Emit(OpCodes.Ldflda, netIdFieldReference);
                var getFunc = module.syncVarSetterNetworkBehaviour.MakeGenericInstanceType(assembly.MainModule, fd.FieldType);
                worker.Emit(OpCodes.Call, getFunc);
            }
            else
            {
                var generic = module.syncVarSetterGeneral.MakeGenericInstanceType(assembly.MainModule, fd.FieldType);
                worker.Emit(OpCodes.Call, generic);
            }

            worker.Append(endOfMethod);

            worker.Emit(OpCodes.Ret);

            set.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.In, fd.FieldType));
            set.SemanticsAttributes = MethodSemanticsAttributes.Setter;

            return set;
        }
    }
}