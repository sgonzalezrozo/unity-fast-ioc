#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FastIoC.Core;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace FastIoC.Editor.Generation
{
    /// <summary>
    /// Breaks the loop where generated code stops the project from compiling, while code generation can only run
    /// after a successful compile.
    ///
    /// When every error of a compile is in generated files, the generated sources are removed so the project compiles
    /// again, and the hash of the removed code is remembered with the errors. Generation then refuses to write that
    /// same code again, so it can't loop. Anything that produces different code, such as a fixed installer or a new
    /// Fast IoC version, writes normally and clears the record. Builds stop while the record exists.
    ///
    /// If any error is outside the generated files, nothing is removed: errors in generated code can be a side effect
    /// of the project's own errors (a type that no longer compiles), and the generated code is fine once they're fixed.
    /// Batch mode is left alone: CI should fail on the compile error.
    /// </summary>
    [InitializeOnLoad]
    internal static class GeneratedCodeRecovery
    {
        private const string FAILED_HASH_KEY = "FastIoC_FailedGeneratedCodeHash";
        private const string FAILED_ERRORS_KEY = "FastIoC_FailedGeneratedCodeErrors";
        private const int MAX_REPORTED_ERRORS = 10;

        // EditorPrefs is shared between projects, so the keys include this project's path.
        private static string FailedHashKey => $"{FAILED_HASH_KEY}_{Application.dataPath}";
        private static string FailedErrorsKey => $"{FAILED_ERRORS_KEY}_{Application.dataPath}";

        private static readonly List<CompilerMessage> errorsThisCompile = new();
        private static bool recoveryScheduled;

        static GeneratedCodeRecovery()
        {
            if (Application.isBatchMode)
                return;

            CompilationPipeline.compilationStarted += _ => errorsThisCompile.Clear();
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += _ => OnCompilationFinished();
        }

        /// <summary>True while code that failed to compile is on record and hasn't been replaced.</summary>
        internal static bool HasFailedCode => !string.IsNullOrEmpty(EditorPrefs.GetString(FailedHashKey, ""));

        /// <summary>The compile errors of the code on record, one per line.</summary>
        internal static List<string> FailedCodeErrors
        {
            get
            {
                string joined = EditorPrefs.GetString(FailedErrorsKey, "");
                return string.IsNullOrEmpty(joined) ? new List<string>() : new List<string>(joined.Split('\n'));
            }
        }

        /// <summary>True when <paramref name="codeHash"/> is the code that failed to compile.</summary>
        internal static bool IsKnownToFail(string codeHash) =>
            !string.IsNullOrEmpty(codeHash) && codeHash == EditorPrefs.GetString(FailedHashKey, "");

        /// <summary>Forgets the failed code. Called when generation writes different code.</summary>
        internal static void Clear()
        {
            EditorPrefs.DeleteKey(FailedHashKey);
            EditorPrefs.DeleteKey(FailedErrorsKey);
        }

        /// <summary>
        /// Hash of the generated sources, in the order <see cref="GenerationCoordinator"/> writes them.
        /// Recovery reads the files back in the same order, so both sides hash identical input.
        /// </summary>
        internal static string ComputeCodeHash(IEnumerable<string> sources)
        {
            StringBuilder sb = new();
            foreach (string source in sources)
                sb.Append(source ?? string.Empty).Append('\0');

            using SHA256 sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        /// <summary>
        /// Returns "file(line): message" for each compile error, when every error is inside <paramref name="generatedPath"/>.
        /// Returns an empty list when there are no errors, or when any error is elsewhere.
        /// </summary>
        internal static List<string> FindRecoverableErrors(IEnumerable<CompilerMessage> messages, string generatedPath)
        {
            string folder = generatedPath.Replace('\\', '/').TrimEnd('/') + "/";
            List<string> errors = new();
            foreach (CompilerMessage message in messages)
            {
                if (message.type != CompilerMessageType.Error)
                    continue;

                string file = (message.file ?? string.Empty).Replace('\\', '/');
                bool inGeneratedCode = file.StartsWith(folder, StringComparison.Ordinal) || file.Contains("/" + folder);
                if (!inGeneratedCode)
                    return new List<string>();

                errors.Add($"{file}({message.line}): {message.message}");
            }

            return errors;
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            foreach (CompilerMessage message in messages)
            {
                if (message.type == CompilerMessageType.Error)
                    errorsThisCompile.Add(message);
            }
        }

        private static void OnCompilationFinished()
        {
            if (recoveryScheduled)
                return;

            List<string> errors = FindRecoverableErrors(errorsThisCompile, FastIoCConstants.GENERATED_PATH);
            errorsThisCompile.Clear();
            if (errors.Count == 0)
                return;

            recoveryScheduled = true;
            EditorApplication.delayCall += () => Recover(errors);
        }

        private static void Recover(List<string> errors)
        {
            try
            {
                string generatedPath = FastIoCConstants.GENERATED_PATH;

                List<string> sources = new();
                foreach (string fileName in GenerationCoordinator.GeneratedSourceFiles)
                {
                    string path = Path.Combine(generatedPath, fileName);
                    sources.Add(File.Exists(path) ? File.ReadAllText(path) : string.Empty);
                }

                List<string> reported = errors.Count > MAX_REPORTED_ERRORS ? errors.GetRange(0, MAX_REPORTED_ERRORS) : errors;
                EditorPrefs.SetString(FailedHashKey, ComputeCodeHash(sources));
                EditorPrefs.SetString(FailedErrorsKey, string.Join("\n", reported));

                // Only the sources go: GenerationStatus.json stays, and reports what happened.
                foreach (string path in Directory.GetFiles(generatedPath, "*.cs"))
                    AssetDatabase.DeleteAsset(path.Replace('\\', '/'));

                const string reason = "Generated code didn't compile, so Fast IoC removed it to let the project compile. " +
                                      "Generation won't write the same code again: fix the cause in errorDetails, and it regenerates.";
                GenerationStatusWriter.WriteFailureStatus(reason, reported);
                Debug.LogError($"[FastIoC] {reason}\n  • " + string.Join("\n  • ", reported));

                AssetDatabase.Refresh();
            }
            finally
            {
                recoveryScheduled = false;
            }
        }
    }
}
#endif
