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
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace JFramework.Editor
{
    [Serializable]
    internal class NetworkProcessor : ILPostProcessor
    {
        private readonly Log logger = new Log();

        public override ILPostProcessor GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            return compiledAssembly.Name == Const.ASSEMBLY || FindAssembly(compiledAssembly);
        }

        private static bool FindAssembly(ICompiledAssembly compiledAssembly)
        {
            foreach (var path in compiledAssembly.References)
            {
                if (Path.GetFileNameWithoutExtension(path) == Const.ASSEMBLY)
                {
                    return true;
                }
            }

            return false;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            using var ar = new AssemblyResolver(compiledAssembly, logger);
            using var ss = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData);
            var rp = new ReaderParameters
            {
                SymbolStream = ss,
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                AssemblyResolver = ar,
                ReflectionImporterProvider = new ReflectionProvider(),
                ReadingMode = ReadingMode.Immediate
            };
            var pd = compiledAssembly.InMemoryAssembly.PeData;
            using var ms = new MemoryStream(pd);
            using var ad = AssemblyDefinition.ReadAssembly(ms, rp);
            ar.SetAssemblyDefinitionForCompiledAssembly(ad);
            var process = new Process(logger);
            if (!process.Execute(ad, ar, out var change) || !change)
            {
                return new ILPostProcessResult(compiledAssembly.InMemoryAssembly, logger.logs);
            }

            var mm = ad.MainModule;
            foreach (var nr in mm.AssemblyReferences)
            {
                if (nr.Name == ad.Name.Name)
                {
                    AssemblyNameReference an = null;
                    foreach (var assembly in mm.AssemblyReferences)
                    {
                        if (assembly.Name == ad.Name.Name)
                        {
                            an = assembly;
                            break;
                        }
                    }

                    mm.AssemblyReferences.Remove(an);
                    break;
                }
            }

            using var pe = new MemoryStream();
            using var pdb = new MemoryStream();

            var wp = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(),
                SymbolStream = pdb,
                WriteSymbols = true
            };


            ad.Write(pe, wp);
            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), logger.logs);
        }
    }
}