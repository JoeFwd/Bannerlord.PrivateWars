namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>
    /// Press an existing claim on a title: the claimant attacks the title-holder to take the dignity.
    /// </summary>
    public sealed record ClaimCasusBelli(string TitleId) : ICasusBelli
    {
        public const string ClaimType = "claim";

        public string Type => ClaimType;
    }
}
