public static class GameAbortState
{
    // Global flag used to prevent emergency-aborted runs from being saved.
    public static bool CurrentRunAborted { get; private set; } = false;

    public static void MarkRunAborted()
    {
        CurrentRunAborted = true;
    }

    public static void ResetForNewRun()
    {
        CurrentRunAborted = false;
    }
}
