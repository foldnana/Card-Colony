using System;

namespace CryingSnow.StackCraft
{
    public readonly struct NarrativeCommandResult
    {
        public NarrativeCommandResult(bool success, string error)
        {
            Success = success;
            Error = error ?? string.Empty;
        }

        public bool Success { get; }
        public string Error { get; }
    }

    public interface INarrativeCommandOperation
    {
        bool IsCompleted { get; }
        NarrativeCommandResult Result { get; }
        event Action<NarrativeCommandResult> Completed;
        void CompleteImmediately();
        void Cancel(string reason);
    }

    public interface INarrativeCommandExecutor : IDisposable
    {
        INarrativeCommandOperation Execute(
            NarrativeCommandDefinition command,
            bool completeImmediately);
    }

    public interface INarrativePresentationService : IDisposable
    {
        INarrativeCommandOperation Execute(
            NarrativeCommandDefinition command,
            NarrativeActorHandle actor,
            bool completeImmediately);
    }

    public sealed class ImmediateNarrativeCommandOperation :
        INarrativeCommandOperation
    {
        public ImmediateNarrativeCommandOperation(
            bool success = true,
            string error = "")
        {
            Result = new NarrativeCommandResult(success, error);
        }

        public bool IsCompleted => true;
        public NarrativeCommandResult Result { get; }
        public event Action<NarrativeCommandResult> Completed
        {
            add { }
            remove { }
        }
        public void CompleteImmediately() { }
        public void Cancel(string reason) { }
    }
}
