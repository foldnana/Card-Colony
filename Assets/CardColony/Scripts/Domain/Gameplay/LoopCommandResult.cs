namespace CardColony.Gameplay
{
    public readonly struct LoopCommandResult
    {
        public bool Succeeded { get; }
        public string Message { get; }

        private LoopCommandResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public static LoopCommandResult Success(string message)
        {
            return new LoopCommandResult(true, message);
        }

        public static LoopCommandResult Failure(string message)
        {
            return new LoopCommandResult(false, message);
        }
    }
}
