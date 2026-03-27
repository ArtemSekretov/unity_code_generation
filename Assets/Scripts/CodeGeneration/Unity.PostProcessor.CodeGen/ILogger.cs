namespace CodeGeneration.Unity.PostProcessor.CodeGen
{
    public interface ILogger
    {
        void LogWarning(string tag, string message);
        void LogError(string tag, string message);
    }
}