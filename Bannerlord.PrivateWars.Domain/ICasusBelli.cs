namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// A justification for pressing a private war. Extensible framework (design §16); v1 ships the
    /// single <see cref="ClaimCasusBelli"/>. Type is the persisted discriminator; TitleId is the
    /// dignity the war contests.
    /// </summary>
    public interface ICasusBelli
    {
        string Type { get; }
        string TitleId { get; }
    }
}
