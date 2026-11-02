using System;
using System.Collections.Generic;
using System.Text;

namespace Tools.FastIoC.Analyzers
{
    public class ServiceLocatorBuildValidator
    {
        public class ValidationResult
        {
            public bool IsValid { get; set; } = true;
            public List<ValidationError> Errors { get; set; } = new();
            public string GetErrors()
            {
                StringBuilder errorLines = new StringBuilder();
                foreach (ValidationError error in Errors)
                    errorLines.AppendLine(error.ToString());
                return errorLines.ToString();
            }
        }

        public class ValidationError
        {
            public string Code { get; set; } = "";
            public string Message { get; set; } = "";
            public string FileName { get; set; } = "";
            public int? LineNumber { get; set; }

            public override string ToString()
            {
                string location = !string.IsNullOrEmpty(FileName)
                    ? LineNumber.HasValue
                        ? $"{FileName}({LineNumber})"
                        : FileName
                    : "";

                return $"{location}: error {Code}: {Message}";
            }
        }

        public ValidationResult ValidateFile(string sourceCode, string fileName = "")
        {
            ValidationResult result = new();

            if (fileName.Contains("Generated", StringComparison.OrdinalIgnoreCase))
                return result;

            if (fileName.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("test", StringComparison.OrdinalIgnoreCase))
                return result;

            if (ContainsDirectResolveCalls(sourceCode))
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Code = "FASTIOC001",
                    Message = "Cannot call ContextRuntime.Resolve() directly. Use internal fields or constructor injection instead.",
                    FileName = fileName,
                });
            }

            if (ContainsServiceLocatorPattern(sourceCode))
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Code = "FASTIOC002",
                    Message = "ServiceLocator wrapper classes are forbidden. Use internal fields or constructor parameters.",
                    FileName = fileName,
                });
            }

            if (ContainsReflectionTypeLookup(sourceCode))
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Code = "FASTIOC003",
                    Message = "Cannot use reflection to discover TypeIds. This indicates Service Locator anti-pattern.",
                    FileName = fileName,
                });
            }

            return result;
        }

        public string AnalyzeCode(string sourceCode)
        {
            StringBuilder analysis = new();
            analysis.AppendLine("[FastIoC Build Analysis]");
            analysis.AppendLine($"Has direct Resolve calls: {ContainsDirectResolveCalls(sourceCode)}");
            analysis.AppendLine($"Has ServiceLocator pattern: {ContainsServiceLocatorPattern(sourceCode)}");
            analysis.AppendLine($"Has reflection lookup: {ContainsReflectionTypeLookup(sourceCode)}");
            return analysis.ToString();
        }

        private static bool ContainsDirectResolveCalls(string code)
        {
            return code.Contains("runtime.Resolve", StringComparison.Ordinal) ||
                   code.Contains("Runtime.Resolve", StringComparison.Ordinal) ||
                   code.Contains("ContextRuntime.Resolve", StringComparison.Ordinal);
        }

        private static bool ContainsServiceLocatorPattern(string code)
        {
            return code.Contains("public static class ServiceLocator", StringComparison.Ordinal) ||
                   code.Contains("public class ServiceLocator", StringComparison.Ordinal) ||
                   code.Contains("public static T Get<T>", StringComparison.Ordinal) ||
                   code.Contains("public static T Resolve<T>", StringComparison.Ordinal);
        }

        private static bool ContainsReflectionTypeLookup(string code)
        {
            bool hasGeneratedTypeIds = code.Contains("GeneratedTypeIds", StringComparison.Ordinal);
            bool hasReflection = code.Contains("GetField", StringComparison.Ordinal) ||
                                 code.Contains("GetProperty", StringComparison.Ordinal) ||
                                 code.Contains("GetValue", StringComparison.Ordinal) ||
                                 code.Contains("Invoke(null", StringComparison.Ordinal);

            return hasGeneratedTypeIds && hasReflection;
        }
    }
}

