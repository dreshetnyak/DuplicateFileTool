namespace DuplicateFileTool;

public sealed class RecycleFailurePromptViewModel(string filePath, string reason)
{
    public string FilePath { get; } = filePath;
    public string Reason { get; } = reason;
    public bool ApplyToAll { get; set; }

    //Closing the window without pressing a button (Esc or the title bar X) must never continue the run
    internal RecycleFailureDecision Decision { get; set; } = RecycleFailureDecision.Cancel;
}
