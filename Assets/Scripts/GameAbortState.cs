public static class GameAbortState
{
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