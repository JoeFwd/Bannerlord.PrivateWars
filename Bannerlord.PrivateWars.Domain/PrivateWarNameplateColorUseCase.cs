using System;
using System.Globalization;

namespace Bannerlord.PrivateWars.Domain
{
    // Resolves the ARGB-packed tint for private-war nameplates.  Owns ALL colour validation and the
    // default: it reads a raw configured value from the provider, validates it, and falls back to
    // vivid orange whenever the value is missing, malformed, or fully transparent.  The infra
    // provider only supplies the raw string — it never validates or defaults.
    public class PrivateWarNameplateColorUseCase : IPrivateWarNameplateColorUseCase
    {
        // Vivid orange, fully opaque (packed ARGB 0xAARRGGBB).
        public const uint DefaultPrivateWarEnemyArgb = 0xFFFF8C00u;

        // BlueViolet, fully opaque (packed ARGB 0xAARRGGBB).
        public const uint DefaultPrivateWarAllyArgb = 0xFF8A2BE2u;

        // Bannerlord vanilla settlement capsule colors, packed ARGB 0xAARRGGBB.
        public const uint VanillaNeutralSettlementArgb = 0xFF000000u;
        public const uint VanillaSameFactionSettlementArgb = 0xFF245E05u;
        public const uint VanillaEnemySettlementArgb = 0xFF870707u;
        public const uint VanillaAllianceSettlementArgb = 0xFF2986CCu;

        private readonly IPrivateWarNameplateColorProvider _provider;

        public PrivateWarNameplateColorUseCase(IPrivateWarNameplateColorProvider provider)
        {
            _provider = provider;
        }

        public uint GetPrivateWarEnemyArgbColor()
        {
            if (!TryParseArgb(_provider.GetConfiguredPrivateWarEnemyColorArgb(), out uint argb))
            {
                return DefaultPrivateWarEnemyArgb;
            }

            // A fully transparent tint would make the nameplate invisible; treat it as invalid.
            if ((argb & 0xFF000000u) == 0u)
            {
                return DefaultPrivateWarEnemyArgb;
            }

            return argb;
        }

        public uint GetPrivateWarAllyArgbColor()
        {
            if (!TryParseArgb(_provider.GetConfiguredPrivateWarAllyColorArgb(), out uint argb))
            {
                return DefaultPrivateWarAllyArgb;
            }

            // A fully transparent tint would make the nameplate invisible; treat it as invalid.
            if ((argb & 0xFF000000u) == 0u)
            {
                return DefaultPrivateWarAllyArgb;
            }

            return argb;
        }

        public uint GetSettlementCapsuleArgbColor(
            SettlementNameplateRelation vanillaRelation,
            bool isPrivateWarEnemy,
            bool isPrivateWarAlly)
        {
            if (isPrivateWarEnemy)
            {
                return GetPrivateWarEnemyArgbColor();
            }

            if (isPrivateWarAlly)
            {
                return GetPrivateWarAllyArgbColor();
            }

            switch (vanillaRelation)
            {
                case SettlementNameplateRelation.SameFaction:
                    return VanillaSameFactionSettlementArgb;
                case SettlementNameplateRelation.Enemy:
                    return VanillaEnemySettlementArgb;
                case SettlementNameplateRelation.Alliance:
                    return VanillaAllianceSettlementArgb;
                default:
                    return VanillaNeutralSettlementArgb;
            }
        }

        // Accepts 8 hex digits (AARRGGBB) or 6 (RRGGBB, assumed fully opaque), with an optional
        // leading '#'.  Anything else is rejected so the caller falls back to the default.
        private static bool TryParseArgb(string? value, out uint argb)
        {
            argb = 0u;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string hex = value!.Trim();
            if (hex.StartsWith("#", StringComparison.Ordinal))
            {
                hex = hex.Substring(1);
            }

            if (hex.Length == 6)
            {
                hex = "FF" + hex; // no alpha supplied → opaque
            }
            else if (hex.Length != 8)
            {
                return false;
            }

            return uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out argb);
        }
    }
}
