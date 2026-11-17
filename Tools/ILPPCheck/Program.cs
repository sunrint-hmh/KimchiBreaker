using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace KimchiBreaker.Tools
{
    /// <summary>
    /// 사용법: ILPPCheck &lt;응답 파일&gt;
    /// 응답 파일 형식(한 줄에 하나):
    ///   processor=후처리기 DLL 경로 (여러 개 가능)
    ///   target=후처리할 어셈블리 DLL 경로 (같은 폴더에 PDB 가 있어야 함)
    ///   ref=참조 DLL 경로 (여러 개)
    ///   define=전처리기 기호 (여러 개)
    ///   output=후처리 결과를 저장할 DLL 경로(선택)
    /// </summary>
    static class Program
    {
        sealed class CompiledAssembly : ICompiledAssembly
        {
            public InMemoryAssembly InMemoryAssembly { get; set; }

            public string Name { get; set; }

            public string[] References { get; set; }

            public string[] Defines { get; set; }
        }

        static int Main(string[] args)
        {
            if (args.Length != 1 || !File.Exists(args[0]))
            {
                Console.Error.WriteLine("usage: ILPPCheck <response-file>");
                return 2;
            }

            var processors = new List<string>();
            var references = new List<string>();
            var defines = new List<string>();
            string target = null;
            string output = null;
            foreach (string line in File.ReadAllLines(args[0]))
            {
                int split = line.IndexOf('=');
                if (split <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, split);
                string value = line.Substring(split + 1);
                switch (key)
                {
                    case "processor": processors.Add(value); break;
                    case "target": target = value; break;
                    case "ref": references.Add(value); break;
                    case "define": defines.Add(value); break;
                    case "output": output = value; break;
                }
            }

            var searchDirectories = references.Select(Path.GetDirectoryName)
                .Concat(processors.Select(Path.GetDirectoryName))
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct()
                .ToList();
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                foreach (string directory in searchDirectories)
                {
                    string candidate = Path.Combine(directory, name.Name + ".dll");
                    if (File.Exists(candidate))
                    {
                        return context.LoadFromAssemblyPath(candidate);
                    }
                }

                return null;
            };

            string pdbPath = Path.ChangeExtension(target, ".pdb");
            var compiled = new CompiledAssembly
            {
                Name = Path.GetFileNameWithoutExtension(target),
                InMemoryAssembly = new InMemoryAssembly(File.ReadAllBytes(target), File.Exists(pdbPath) ? File.ReadAllBytes(pdbPath) : null),
                References = references.ToArray(),
                Defines = defines.ToArray(),
            };

            var postProcessors = new List<ILPostProcessor>();
            foreach (string path in processors)
            {
                Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(t => t != null).ToArray();
                    foreach (Exception loaderException in exception.LoaderExceptions.Take(5))
                    {
                        Console.Error.WriteLine("타입 로드 경고: " + loaderException?.Message);
                    }
                }

                foreach (Type type in types.Where(t => typeof(ILPostProcessor).IsAssignableFrom(t) && !t.IsAbstract).OrderBy(t => t.FullName, StringComparer.Ordinal))
                {
                    var instance = (ILPostProcessor)Activator.CreateInstance(type, true);
                    postProcessors.Add(instance.GetInstance());
                }
            }

            if (postProcessors.Count == 0)
            {
                Console.Error.WriteLine("후처리기를 찾지 못했습니다.");
                return 2;
            }

            int errors = 0;
            int warnings = 0;
            foreach (ILPostProcessor processor in postProcessors)
            {
                string processorName = processor.GetType().Name;
                if (!processor.WillProcess(compiled))
                {
                    Console.WriteLine($"[건너뜀] {processorName}");
                    continue;
                }

                ILPostProcessResult result;
                try
                {
                    result = processor.Process(compiled);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[예외] {processorName}: {exception}");
                    errors++;
                    continue;
                }

                List<DiagnosticMessage> diagnostics = result?.Diagnostics ?? new List<DiagnosticMessage>();
                foreach (DiagnosticMessage message in diagnostics)
                {
                    bool isError = message.DiagnosticType == DiagnosticType.Error;
                    if (isError)
                    {
                        errors++;
                    }
                    else
                    {
                        warnings++;
                    }

                    string location = string.IsNullOrEmpty(message.File) ? string.Empty : $"{message.File}({message.Line},{message.Column}): ";
                    Console.WriteLine($"    {(isError ? "error" : "warning")}: {location}{message.MessageData}");
                }

                bool changed = result?.InMemoryAssembly != null;
                Console.WriteLine($"[{(diagnostics.Any(d => d.DiagnosticType == DiagnosticType.Error) ? "실패" : "성공")}] {processorName} (코드 {(changed ? "생성됨" : "변경 없음")}, 진단 {diagnostics.Count}개)");
                if (changed)
                {
                    compiled = new CompiledAssembly
                    {
                        Name = compiled.Name,
                        InMemoryAssembly = result.InMemoryAssembly,
                        References = compiled.References,
                        Defines = compiled.Defines,
                    };
                }
            }

            if (!string.IsNullOrEmpty(output))
            {
                File.WriteAllBytes(output, compiled.InMemoryAssembly.PeData);
                if (compiled.InMemoryAssembly.PdbData != null)
                {
                    File.WriteAllBytes(Path.ChangeExtension(output, ".pdb"), compiled.InMemoryAssembly.PdbData);
                }
            }

            Console.WriteLine($"IL 후처리 결과: 오류 {errors}개, 경고 {warnings}개");
            return errors > 0 ? 1 : 0;
        }
    }
}
