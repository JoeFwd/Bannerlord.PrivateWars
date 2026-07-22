namespace Bannerlord.PrivateWars.Domain.Model
{
    /// <summary>
    /// Which side of a private war a clan belongs to, resolved dynamically by walking up
    /// the suzerain chain to the nearest belligerent principal (call-to-arms, design §18.A).
    /// </summary>
    public enum WarSide
    {
        Attacker,
        Defender
    }
}
