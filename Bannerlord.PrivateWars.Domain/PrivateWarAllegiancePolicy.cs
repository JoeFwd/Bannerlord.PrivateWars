namespace Bannerlord.PrivateWars.Domain
{
    /// <summary>Which war a belligerent lord prosecutes: its realm's, or its private feud.</summary>
    public enum PrivateWarTheatre
    {
        Kingdom,
        Private
    }

    /// <summary>
    /// What weighs on a belligerent clan leader's choice between the crown's war and the feud. Stakes
    /// are strategic (fiefs, being the principal), opinions are personal (relations), traits use the
    /// vanilla -2..2 scale.
    /// </summary>
    public sealed record PrivateWarAllegianceInputs(
        bool IsPrincipal,
        bool IsRuler,
        bool RealmAtWar,
        bool OwnFiefThreatenedByRealmEnemy,
        bool OwnFiefThreatenedByFeudEnemy,
        bool OwnFiefHeldByFeudEnemy,
        int RelationWithRuler,
        int RelationWithPrincipal,
        int Honor,
        int Calculating,
        PrivateWarTheatre? Current);

    /// <summary>
    /// Decides, once a day, whether a belligerent lord answers the crown or pursues its feud. With no
    /// realm war and no realm enemy at its gates there is nothing for the crown to call on, so the feud
    /// wins outright. Otherwise each theatre scores its stakes and opinions: Calculating lords weigh
    /// the stakes more and their feelings less, honourable lords feel the duty to the crown, and the
    /// current theatre carries a bonus so a lord does not flip on every small change.
    /// </summary>
    public class PrivateWarAllegiancePolicy
    {
        private const float HysteresisBonus = 0.75f;
        private const float RelationScale = 50f;

        public PrivateWarTheatre Choose(PrivateWarAllegianceInputs inputs)
        {
            if (!inputs.RealmAtWar && !inputs.OwnFiefThreatenedByRealmEnemy) return PrivateWarTheatre.Private;

            float stakeWeight = 1f + 0.25f * inputs.Calculating;
            float opinionWeight = 1f - 0.25f * inputs.Calculating;

            float privateStake = 1f
                                 + (inputs.IsPrincipal ? 2f : 0f)
                                 + (inputs.OwnFiefHeldByFeudEnemy ? 1.5f : 0f)
                                 + (inputs.OwnFiefThreatenedByFeudEnemy ? 2f : 0f);
            float kingdomStake = (inputs.RealmAtWar ? 1f : 0f)
                                 + (inputs.IsRuler ? 2f : 0f)
                                 + (inputs.OwnFiefThreatenedByRealmEnemy ? 2f : 0f);

            float privateOpinion = inputs.IsPrincipal ? 0f : inputs.RelationWithPrincipal / RelationScale;
            float kingdomOpinion = (inputs.IsRuler ? 0f : inputs.RelationWithRuler / RelationScale)
                                   + 0.5f * inputs.Honor;

            float privateScore = privateStake * stakeWeight + privateOpinion * opinionWeight
                                 + (inputs.Current == PrivateWarTheatre.Private ? HysteresisBonus : 0f);
            float kingdomScore = kingdomStake * stakeWeight + kingdomOpinion * opinionWeight
                                 + (inputs.Current == PrivateWarTheatre.Kingdom ? HysteresisBonus : 0f);

            return kingdomScore > privateScore ? PrivateWarTheatre.Kingdom : PrivateWarTheatre.Private;
        }
    }
}
