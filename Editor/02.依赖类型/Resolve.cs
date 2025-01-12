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
using Mono.Cecil;

namespace JFramework.Editor
{
    internal static class Resolve
    {
        public static bool IsEditor(AssemblyDefinition ad)
        {
            foreach (var ar in ad.MainModule.AssemblyReferences)
            {
                if (ar.Name.StartsWith(nameof(UnityEditor)))
                {
                    return true;
                }
            }

            return false;
        }

        public static MethodReference GetProperty(TypeReference tr, AssemblyDefinition ad, string name)
        {
            foreach (var pd in tr.Resolve().Properties)
            {
                if (pd.Name == name)
                {
                    var mr = ad.MainModule.ImportReference(pd.GetMethod);
                    return mr;
                }
            }

            return null;
        }

        public static MethodDefinition GetConstructor(TypeReference tr)
        {
            foreach (var md in tr.Resolve().Methods)
            {
                if (md.Name == Const.CTOR && md.Resolve().IsPublic && md.Parameters.Count == 0)
                {
                    return md;
                }
            }

            return null;
        }

        public static MethodReference GetMethod(TypeReference tr, AssemblyDefinition ad, Logger log, string name, ref bool failed)
        {
            if (tr == null)
            {
                log.Error($"没有无法解析方法: {name}");
                failed = true;
                return null;
            }

            var mr = GetMethod(tr, ad, log, method => method.Name == name, ref failed);
            if (mr == null)
            {
                log.Error($"在类型 {tr.Name} 中没有找到名称 {name} 的方法", tr);
                failed = true;
            }

            return mr;
        }

        public static MethodReference GetMethod(TypeReference tr, AssemblyDefinition ad, Logger log, Func<MethodDefinition, bool> func, ref bool failed)
        {
            foreach (var md in tr.Resolve().Methods)
            {
                if (func.Invoke(md))
                {
                    return ad.MainModule.ImportReference(md);
                }
            }

            log.Error($"在类型 {tr.Name} 中没有找到方法", tr);
            failed = true;
            return null;
        }

        public static MethodReference GetMethodInParent(TypeReference tr, AssemblyDefinition ad, string name)
        {
            while (true)
            {
                if (tr == null)
                {
                    return null;
                }

                foreach (var md in tr.Resolve().Methods)
                {
                    if (md.Name == name)
                    {
                        MethodReference mr = md;
                        if (tr.IsGenericInstance)
                        {
                            mr = mr.MakeHostInstanceGeneric(tr.Module, (GenericInstanceType)tr);
                        }

                        return ad.MainModule.ImportReference(mr);
                    }
                }

                tr = tr.Resolve().BaseType.ApplyGenericParameters(tr);
            }
        }
    }
}