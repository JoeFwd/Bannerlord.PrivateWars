namespace Bannerlord.PrivateWars.Domain.Model
{
    /// <summary>
    /// The recomputed score and, if a victory threshold (±100) was crossed, the terminal outcome.
    /// Outcome is null while the war is still running. The campaign layer persists the new score.
    /// </summary>
    public record TickResult(
        float Score,
        PrivateWarOutcome? Outcome);
}
