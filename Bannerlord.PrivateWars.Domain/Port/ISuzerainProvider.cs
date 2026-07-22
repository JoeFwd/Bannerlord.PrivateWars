namespace Bannerlord.PrivateWars.Domain.Port
{
    /// <summary>
    /// Supplies the feudal suzerain link used to resolve war sides by walking up the chain
    /// to the nearest belligerent principal (call-to-arms, design §18.A). Returns null for a clan
    /// with no suzerain (top of its tree).
    /// </summary>
    public interface ISuzerainProvider
    {
        string? GetSuzerain(string clanId);
    }
}
