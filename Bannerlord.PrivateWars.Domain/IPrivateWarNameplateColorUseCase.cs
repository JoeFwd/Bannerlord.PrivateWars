namespace Bannerlord.PrivateWars.Domain
{
    public enum SettlementNameplateRelation
    {
        Neutral,
        SameFaction,
        Enemy,
        Alliance
    }

    // Returns the ARGB-packed color to use when tinting a party or settlement nameplate that
    // belongs to a private-war belligerent.  The value is pure data (an unsigned integer); the
    // Integration layer is responsible for converting it to a TaleWorlds Color string.
    public interface IPrivateWarNameplateColorUseCase
    {
        // Tint for a nameplate belonging to a private-war ENEMY of the player (default vivid orange).
        uint GetPrivateWarEnemyArgbColor();

        // Tint for a nameplate belonging to a private-war ALLY of the player — a distinct clan pulled
        // onto the player's own side of an active private war (default purple/BlueViolet).
        uint GetPrivateWarAllyArgbColor();

        // Settlement capsule tint. Private-war enemy/same-side colors override vanilla relation
        // colors; otherwise this returns Bannerlord's vanilla settlement capsule color for the
        // semantic relation supplied by the Integration adapter.
        uint GetSettlementCapsuleArgbColor(
            SettlementNameplateRelation vanillaRelation,
            bool isPrivateWarEnemy,
            bool isPrivateWarAlly);
    }
}
