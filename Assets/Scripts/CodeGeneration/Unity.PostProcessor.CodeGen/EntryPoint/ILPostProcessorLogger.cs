using System.Collections.Generic;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace CodeGeneration.Unity.PostProcessor.CodeGen.EntryPoint
{
    public class ILPostProcessorLogger : ILogger
    {
        // can't Debug.Log in ILPostProcessor. need to add to this list.
        public List<DiagnosticMessage> Logs = new List<DiagnosticMessage>();

        private void Add(string message, DiagnosticType logType)
        {
            Logs.Add(new DiagnosticMessage
            {
                // TODO add file etc. for double click opening later?
                DiagnosticType = logType, // doesn't have .Log
                File = null,
                Line = 0,
                Column = 0,
                MessageData = message
            });
        }

        public void LogWarning(string tag, string message)
        {
            Add($"[{tag}] {message}", DiagnosticType.Warning);
        }

        public void LogError(string tag, string message)
        {
            Add($"[{tag}] {message}", DiagnosticType.Error);  
        } 
    }
}
