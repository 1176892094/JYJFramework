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

using System.Reflection;
using Mono.Cecil;

namespace JFramework.Editor
{
    /// <summary>
    /// 反射导入器提供程序
    /// </summary>
    internal class ReflectionProvider : IReflectionImporterProvider
    {
        public IReflectionImporter GetReflectionImporter(ModuleDefinition module) => new ReflectionImporter(module);
    }

    /// <summary>
    /// 默认的反射导入器
    /// </summary>
    internal class ReflectionImporter : DefaultReflectionImporter
    {
        private const string SystemPrivateCoreLib = "System.Private.CoreLib";
        private readonly AssemblyNameReference fixedCoreLib;

        public ReflectionImporter(ModuleDefinition module) : base(module)
        {
            AssemblyNameReference first = null;
            foreach (var assembly in module.AssemblyReferences)
            {
                if (Equals(assembly.Name))
                {
                    first = assembly;
                    break;
                }
            }

            fixedCoreLib = first;
        }

        public override AssemblyNameReference ImportReference(AssemblyName name)
        {
            if (name.Name == SystemPrivateCoreLib && fixedCoreLib != null)
            {
                return fixedCoreLib;
            }

            return base.ImportReference(name);
        }

        private static bool Equals(string name)
        {
            return name is "mscorlib" or "netstandard" or SystemPrivateCoreLib;
        }
    }
}