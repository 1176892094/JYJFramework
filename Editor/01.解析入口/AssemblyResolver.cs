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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Mono.Cecil;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace JFramework.Editor
{
    internal sealed class AssemblyResolver : IAssemblyResolver
    {
        private readonly ConcurrentDictionary<string, AssemblyDefinition> assemblyCache = new();
        private readonly ConcurrentDictionary<string, string> fileNameCache = new();
        private readonly string[] assemblyReferences;
        private readonly ICompiledAssembly compiledAssembly;
        private readonly Logger logger;
        private AssemblyDefinition selfAssembly;

        public AssemblyResolver(ICompiledAssembly compiledAssembly, Logger logger)
        {
            this.compiledAssembly = compiledAssembly;
            assemblyReferences = compiledAssembly.References;
            this.logger = logger;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            return Resolve(name, new ReaderParameters(ReadingMode.Deferred));
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            if (name.Name == compiledAssembly.Name)
            {
                return selfAssembly;
            }

            if (!fileNameCache.TryGetValue(name.Name, out var fileName))
            {
                fileName = FindFile(name.Name);
                fileNameCache.TryAdd(name.Name, fileName);
            }

            if (fileName == null)
            {
                logger.Warn($"无法找到文件: {name}");
                return null;
            }

            var lastWriteTime = File.GetLastWriteTime(fileName);
            var cacheKey = fileName + lastWriteTime;
            if (assemblyCache.TryGetValue(cacheKey, out var result))
            {
                return result;
            }

            parameters.AssemblyResolver = this;
            var ms = MemoryStreamFor(fileName);

            var pdb = fileName + ".pdb";
            if (File.Exists(pdb))
            {
                parameters.SymbolStream = MemoryStreamFor(pdb);
            }

            var assemblyDefinition = AssemblyDefinition.ReadAssembly(ms, parameters);
            assemblyCache.TryAdd(cacheKey, assemblyDefinition);
            return assemblyDefinition;
        }

        private string FindFile(string name)
        {
            foreach (var r in assemblyReferences)
            {
                if (Path.GetFileNameWithoutExtension(r) == name)
                {
                    return r;
                }
            }

            var dllName = name + ".dll";

            var set = new HashSet<string>();
            foreach (var s in assemblyReferences)
            {
                var parentDir = Path.GetDirectoryName(s);
                if (set.Add(parentDir))
                {
                    if (parentDir != null)
                    {
                        var candidate = Path.Combine(parentDir, dllName);
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }

            return null;
        }

        private static MemoryStream MemoryStreamFor(string fileName)
        {
            return Retry(10, TimeSpan.FromSeconds(1), () =>
            {
                byte[] byteArray;
                using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byteArray = new byte[fs.Length];
                    var readLength = fs.Read(byteArray, 0, (int)fs.Length);
                    if (readLength != fs.Length)
                    {
                        throw new InvalidOperationException("文件读取长度不完整。");
                    }
                }

                return new MemoryStream(byteArray);
            });
        }

        private static MemoryStream Retry(int retryCount, TimeSpan waitTime, Func<MemoryStream> func)
        {
            try
            {
                return func();
            }
            catch (IOException)
            {
                if (retryCount == 0)
                {
                    throw;
                }

                Console.WriteLine($"捕获IO异常，尝试{retryCount}更多次。");
                Thread.Sleep(waitTime);
                return Retry(retryCount - 1, waitTime, func);
            }
        }

        public void SetAssemblyDefinitionForCompiledAssembly(AssemblyDefinition assemblyDefinition)
        {
            selfAssembly = assemblyDefinition;
        }
    }
}