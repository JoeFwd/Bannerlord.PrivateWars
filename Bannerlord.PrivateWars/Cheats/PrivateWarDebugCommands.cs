using System.Collections.Generic;
using System.Linq;
using Bannerlord.PrivateWars.Domain;
using Bannerlord.PrivateWars.Domain.Port;
using Bannerlord.PrivateWars.DI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.PrivateWars.Cheats
{
    /// <summary>
    /// TEMPORARY debug console command for playtesting the private-war battle path. There is no
    /// in-game declaration path yet (the real one is a later phase), so this registers an active war
    /// directly via <see cref="DeclarePrivateWarUseCase"/> from two same-kingdom clan ids. Remove once
    /// a real declaration trigger exists.
    /// <para>Usage: <c>campaign.declare_private_war &lt;attackerClanId&gt; &lt;defenderClanId&gt; [goalSettlementId]</c></para>
    /// </summary>
    public static class PrivateWarDebugCommands
    {
        [CommandLineFunctionality.CommandLineArgumentFunction("declare_private_war", "campaign")]
        public static string DeclarePrivateWar(List<string> args)
        {
            if (args.Count < 2)
                return "Usage: campaign.declare_private_war <attackerClanId> <defenderClanId> [goalSettlementId]";

            if (!PrivateWarsServices.IsInitialised || PrivateWarsServices.PrivateWars is null)
                return "FeudalServices is not initialised - load a campaign first.";

            var attacker = FindClan(args[0]);
            if (attacker is null) return $"No clan with id '{args[0]}'.\n" + ListClans();

            var defender = FindClan(args[1]);
            if (defender is null) return $"No clan with id '{args[1]}'.\n" + ListClans();

            if (attacker == defender) return "Attacker and defender must differ.";

            // The whole point of a private war: same MapFaction (kingdom). Warn but allow.
            string note = attacker.MapFaction == defender.MapFaction
                ? ""
                : " (WARNING: clans are NOT in the same MapFaction - this is an ordinary war, not a private one)";

            Settlement? goal = args.Count >= 3
                ? Settlement.Find(args[2])
                : defender.Settlements.FirstOrDefault(s => s.IsFortification);
            if (goal is null)
                return args.Count >= 3
                    ? $"No settlement with id '{args[2]}'.\n" + ListNearestFortifications(defender)
                    : $"Defender clan '{defender.StringId}' holds no fortification to use as a goal; pass goalSettlementId explicitly.\n" + ListNearestFortifications(defender);

            if (PrivateWarsServices.PrivateWars is not IPrivateWarSidesProvider sides)
                return "Private-war sides provider unavailable.";

            var fiefSnapshot = Settlement.All
                .Where(s => (s.IsTown || s.IsCastle) && s.OwnerClan != null)
                .ToDictionary(s => s.StringId, s => s.OwnerClan.StringId);

            var useCase = new DeclarePrivateWarUseCase(PrivateWarsServices.PrivateWars, sides);
            var war = useCase.Execute(
                attacker.StringId,
                defender.StringId,
                new DebugCasusBelli(),
                goal.StringId,
                fiefSnapshot,
                (float)CampaignTime.Now.ToDays,
                new string[0],
                new string[0]);

            if (war is null)
                return "Declaration rejected (landless defender, or an identical active war already exists).";

            return $"Private war declared: {attacker.StringId} vs {defender.StringId}, goal '{goal.StringId}'{note}. Id={war.Id}";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("list_private_wars", "campaign")]
        public static string ListPrivateWars(List<string> args)
        {
            if (!PrivateWarsServices.IsInitialised || PrivateWarsServices.PrivateWars is null)
                return "FeudalServices is not initialised - load a campaign first.";

            var wars = PrivateWarsServices.PrivateWars.GetAll();
            if (wars.Count == 0) return "No private wars.";

            var lines = wars.Select(w =>
                $"  {w.AttackerPrincipalClanId} vs {w.DefenderPrincipalClanId}  goal={w.MainGoalSettlementId}  " +
                $"score={w.Score:0.#}  battle={w.BattleScore:0.#}  status={w.Status}  (id={w.Id})");
            return $"Private wars ({wars.Count}):\n" + string.Join("\n", lines);
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("capture_settlement", "campaign")]
        public static string CaptureSettlement(List<string> args)
        {
            if (args.Count < 2)
                return "Usage: campaign.capture_settlement <settlementId> <newOwnerClanId>";

            var settlement = Settlement.Find(args[0]);
            if (settlement is null) return $"No settlement with id '{args[0]}'.";

            var clan = FindClan(args[1]);
            if (clan is null) return $"No clan with id '{args[1]}'.\n" + ListClans();

            var hero = clan.Leader ?? clan.Heroes.FirstOrDefault();
            if (hero is null) return $"Clan '{clan.StringId}' has no hero to take ownership.";

            ChangeOwnerOfSettlementAction.ApplyByDefault(hero, settlement);
            return $"'{settlement.StringId}' is now owned by {clan.StringId}.";
        }

        // TEMP: deterministically create a same-kingdom private-war captive. The vanilla
        // campaign.add_prisoner_to_party cheat refuses non-warring MapFactions, so it cannot
        // produce a same-kingdom captive; this calls TakePrisonerAction.Apply directly (no war check).
        [CommandLineFunctionality.CommandLineArgumentFunction("capture_lord", "campaign")]
        public static string CaptureLord(List<string> args)
        {
            if (args.Count < 2)
                return "Usage: campaign.capture_lord <captiveClanId> <captorClanId>";

            var captiveClan = FindClan(args[0]);
            if (captiveClan is null) return $"No clan with id '{args[0]}'.\n" + ListClans();

            var captorClan = FindClan(args[1]);
            if (captorClan is null) return $"No clan with id '{args[1]}'.\n" + ListClans();

            var captive = captiveClan.Heroes.FirstOrDefault(h =>
                h.IsAlive && h.IsLord && !h.IsPrisoner && h != Hero.MainHero);
            if (captive is null)
                return $"Clan '{captiveClan.StringId}' has no free, living lord to capture.";

            var captorParty = captorClan.Leader?.PartyBelongedTo
                              ?? captorClan.WarPartyComponents.FirstOrDefault()?.MobileParty;
            if (captorParty is null)
                return $"Captor clan '{captorClan.StringId}' has no mobile party to hold a prisoner.";

            TakePrisonerAction.Apply(captorParty.Party, captive);

            return $"Captured {captive.StringId} ({captive.Name}) of {captiveClan.StringId} into " +
                   $"{captorClan.StringId}'s party '{captorParty.StringId}'. IsPrisoner={captive.IsPrisoner}.";
        }

        // TEMP: make the player (Hero.MainHero) a prisoner of the given captor clan's leader party,
        // bypassing the vanilla faction-war check. Mirrors capture_lord but targets the main hero.
        // Used to stage the player-captivity behavior for manual testing: declare a private war
        // first, then capture_player the rival clan, advance time, and confirm the public behavior
        // replacement preserves the vanilla captivity flow while the private war remains active.
        [CommandLineFunctionality.CommandLineArgumentFunction("capture_player", "campaign")]
        public static string CapturePlayer(List<string> args)
        {
            if (args.Count < 1)
                return "Usage: campaign.capture_player <captorClanId>";

            var captorClan = FindClan(args[0]);
            if (captorClan is null) return $"No clan with id '{args[0]}'.\n" + ListClans();

            var captorParty = captorClan.Leader?.PartyBelongedTo
                              ?? captorClan.WarPartyComponents.FirstOrDefault()?.MobileParty;
            if (captorParty is null)
                return $"Captor clan '{captorClan.StringId}' has no mobile party to hold a prisoner.";

            if (Hero.MainHero.IsPrisoner)
                return $"Player is already a prisoner of {(Hero.MainHero.PartyBelongedToAsPrisoner?.MobileParty?.ActualClan?.StringId ?? "?")}. " +
                       "Release first or load a clean save.";

            TakePrisonerAction.Apply(captorParty.Party, Hero.MainHero);

            return $"Player ({Hero.MainHero.StringId}) captured into {captorClan.StringId}'s party " +
                   $"'{captorParty.StringId}'. IsPrisoner={Hero.MainHero.IsPrisoner}.";
        }

        // TEMP: report whether a hero is currently held prisoner, and by whom. Used to assert the
        // private-war retention/release behavior before and after a release trigger.
        [CommandLineFunctionality.CommandLineArgumentFunction("is_prisoner", "campaign")]
        public static string IsPrisonerCmd(List<string> args)
        {
            if (args.Count < 1)
                return "Usage: campaign.is_prisoner <heroStringId>";

            var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == args[0]);
            if (hero is null) return $"No alive hero with id '{args[0]}'.";

            if (!hero.IsPrisoner)
                return $"{hero.StringId} ({hero.Name}) is NOT a prisoner.";

            var captorParty = hero.PartyBelongedToAsPrisoner;
            var captorClan = captorParty?.MobileParty?.ActualClan ?? captorParty?.Settlement?.OwnerClan;
            return $"{hero.StringId} ({hero.Name}) IS a prisoner of " +
                   $"{(captorClan != null ? captorClan.StringId : "?")}.";
        }

        // TEMP: fire the real OnMakePeace prisoner sweep over a captor clan's parties. There is no
        // vanilla make_peace cheat, so this declares (if needed) then immediately makes peace between
        // the captor's MapFaction and another kingdom - exercising the ReleasedAfterPeace hazard the
        // private-war retention veto guards against.
        [CommandLineFunctionality.CommandLineArgumentFunction("force_peace", "campaign")]
        public static string ForcePeace(List<string> args)
        {
            if (args.Count < 1)
                return "Usage: campaign.force_peace <captorClanId>";

            var captorClan = FindClan(args[0]);
            if (captorClan is null) return $"No clan with id '{args[0]}'.\n" + ListClans();

            var captorFaction = captorClan.MapFaction;
            var opponent = Kingdom.All.FirstOrDefault(k => k != captorFaction && !k.IsEliminated);
            if (opponent is null) return "No other kingdom found to make peace with.";

            if (!captorFaction.IsAtWarWith(opponent))
                DeclareWarAction.ApplyByDefault(captorFaction, opponent);

            MakePeaceAction.Apply(captorFaction, opponent);

            return $"Forced war+peace between {captorFaction.Name} and {opponent.Name} - " +
                   $"the OnMakePeace prisoner sweep has run over {captorFaction.Name}'s clans.";
        }

        // TEMP: force an immediate field battle between two clans' leader parties via
        // StartBattleAction.ApplyStartBattle (no war-status check), so the MapEventEnded battle-score
        // wiring can be exercised deterministically. The engine simulates the AI-vs-AI battle to
        // conclusion over the next ticks; advance time, then read battle=... in list_private_wars.
        [CommandLineFunctionality.CommandLineArgumentFunction("force_battle", "campaign")]
        public static string ForceBattle(List<string> args)
        {
            if (args.Count < 2)
                return "Usage: campaign.force_battle <attackerClanId> <defenderClanId>";

            var attackerClan = FindClan(args[0]);
            if (attackerClan is null) return $"No clan with id '{args[0]}'.\n" + ListClans();

            var defenderClan = FindClan(args[1]);
            if (defenderClan is null) return $"No clan with id '{args[1]}'.\n" + ListClans();

            var attackerParty = attackerClan.Leader?.PartyBelongedTo
                                ?? attackerClan.WarPartyComponents.FirstOrDefault()?.MobileParty;
            var defenderParty = defenderClan.Leader?.PartyBelongedTo
                                ?? defenderClan.WarPartyComponents.FirstOrDefault()?.MobileParty;
            if (attackerParty is null) return $"Attacker clan '{attackerClan.StringId}' has no mobile party.";
            if (defenderParty is null) return $"Defender clan '{defenderClan.StringId}' has no mobile party.";
            if (defenderParty.MapEvent != null)
                return $"Defender party '{defenderParty.StringId}' is already in a map event.";

            StartBattleAction.ApplyStartBattle(attackerParty, defenderParty);

            return $"Started field battle: {attackerParty.StringId} vs {defenderParty.StringId}. " +
                   $"MapEvent={(defenderParty.MapEvent != null ? "created" : "NOT created")}. " +
                   "Advance time for the AI simulation to resolve, then read battle=... in list_private_wars.";
        }

        // TEMP: move a clan between kingdoms (or out to independence) to exercise the
        // OnClanChangedKingdom integrity handler. "none"/"leave" makes the clan independent; otherwise
        // it joins the named kingdom. A private war whose two principals end up in different kingdoms
        // (no shared MapFaction) auto-concludes.
        [CommandLineFunctionality.CommandLineArgumentFunction("change_kingdom", "campaign")]
        public static string ChangeKingdomCmd(List<string> args)
        {
            if (args.Count < 2)
                return "Usage: campaign.change_kingdom <clanId> <kingdomId|none>";

            var clan = FindClan(args[0]);
            if (clan is null) return $"No clan with id '{args[0]}'.\n" + ListClans();

            if (args[1] is "none" or "leave")
            {
                ChangeKingdomAction.ApplyByLeaveKingdom(clan);
                return $"{clan.StringId} left its kingdom; now MapFaction={clan.MapFaction?.StringId ?? "?"}.";
            }

            var kingdom = Kingdom.All.FirstOrDefault(k => k.StringId == args[1]);
            if (kingdom is null)
                return $"No kingdom with id '{args[1]}'. Kingdoms: " +
                       string.Join(", ", Kingdom.All.Where(k => !k.IsEliminated).Select(k => k.StringId));

            ChangeKingdomAction.ApplyByJoinToKingdom(clan, kingdom);
            return $"{clan.StringId} joined {kingdom.StringId}; now MapFaction={clan.MapFaction?.StringId ?? "?"}.";
        }

        // TEMP: deterministically stage a same-kingdom siege so the capture loop
        // (IsPreparationComplete -> TryCaptureCompletedSieges) can be verified without waiting on AI
        // pathing. Teleports the besieger clan's leader party to the goal's gate and orders a besiege;
        // the engine starts the siege camp on the next tick and the private-war hold-score keeps it
        // camped. Capture fires on PREPARATION complete (a timer), not assault, so a lone party suffices.
        // Advance ~1 day, then read list_private_wars / settlement owner to confirm the flip.
        [CommandLineFunctionality.CommandLineArgumentFunction("force_besiege", "campaign")]
        public static string ForceBesiege(List<string> args)
        {
            if (args.Count < 2)
                return "Usage: campaign.force_besiege <besiegerClanId> <settlementId>";

            var besiegerClan = FindClan(args[0]);
            if (besiegerClan is null) return $"No clan with id '{args[0]}'.\n" + ListClans();

            var settlement = Settlement.Find(args[1]);
            if (settlement is null) return $"No settlement with id '{args[1]}'.";
            if (!settlement.IsFortification)
                return $"'{settlement.StringId}' is not a town or castle.";

            var party = besiegerClan.Leader?.PartyBelongedTo
                        ?? besiegerClan.WarPartyComponents.FirstOrDefault()?.MobileParty;
            if (party is null) return $"Besieger clan '{besiegerClan.StringId}' has no mobile party.";
            if (party.MapEvent != null)
                return $"Besieger party '{party.StringId}' is already in a map event.";

            party.Position = settlement.GatePosition;
            party.SetMoveBesiegeSettlement(settlement, MobileParty.NavigationType.Default);

            return $"Teleported {party.StringId} ({besiegerClan.StringId}) to {settlement.StringId}'s gate " +
                   "and ordered a besiege. Advance time ~1 day; capture fires on preparation complete.";
        }

        // TEMP: jump an active siege's preparation straight to complete so the capture trigger
        // (TryCaptureCompletedSieges, which fires on IsPreparationComplete) can be observed within one
        // hourly tick. Preparation construction speed scales with the BESIEGER's man-day power (not the
        // garrison), so a lone party would otherwise take many in-game days; this isolates the capture
        // path from vanilla construction speed. Run after force_besiege has formed the camp; advance ~1
        // hour, then read settlement owner / list_private_wars to confirm the ownership flip.
        [CommandLineFunctionality.CommandLineArgumentFunction("complete_siege_prep", "campaign")]
        public static string CompleteSiegePrep(List<string> args)
        {
            if (args.Count < 1)
                return "Usage: campaign.complete_siege_prep <settlementId>";

            var settlement = Settlement.Find(args[0]);
            if (settlement is null) return $"No settlement with id '{args[0]}'.";

            var siege = settlement.SiegeEvent;
            if (siege is null) return $"'{settlement.StringId}' is not under siege.";

            var preparations = siege.GetSiegeEventSide(BattleSideEnum.Attacker)?.SiegeEngines?.SiegePreparations;
            if (preparations is null) return $"'{settlement.StringId}' siege has no attacker preparations to complete.";

            preparations.SetProgress(1f);

            var besieger = siege.BesiegerCamp?.LeaderParty?.ActualClan;
            return $"Set {settlement.StringId} siege preparation to complete (besieger {besieger?.StringId ?? "?"}). " +
                   "Advance ~1 hour; the capture trigger fires on the next hourly tick.";
        }

        // TEMP: directly start a siege event on a settlement using the besieger clan's leader party,
        // bypassing the vanilla IsAtWarWith gate that blocks same-kingdom sieges. Use after
        // declaring a private war; follow with complete_siege_prep then advance ~1 hour.
        [CommandLineFunctionality.CommandLineArgumentFunction("create_siege", "campaign")]
        public static string CreateSiege(List<string> args)
        {
            if (args.Count < 2)
                return "Usage: campaign.create_siege <besiegerClanId> <settlementId>";

            var besiegerClan = FindClan(args[0]);
            if (besiegerClan is null) return $"No clan with id '{args[0]}'.\n" + ListClans();

            var settlement = Settlement.Find(args[1]);
            if (settlement is null) return $"No settlement with id '{args[1]}'.";
            if (!settlement.IsFortification)
                return $"'{settlement.StringId}' is not a town or castle.";

            if (settlement.SiegeEvent != null)
                return $"'{settlement.StringId}' is already under siege by {settlement.SiegeEvent.BesiegerCamp?.LeaderParty?.ActualClan?.StringId ?? "?"}";

            var party = besiegerClan.Leader?.PartyBelongedTo
                        ?? besiegerClan.WarPartyComponents.FirstOrDefault()?.MobileParty;
            if (party is null) return $"Besieger clan '{besiegerClan.StringId}' has no mobile party.";

            TaleWorlds.CampaignSystem.Campaign.Current.SiegeEventManager.StartSiegeEvent(settlement, party);

            var besieger = settlement.SiegeEvent?.BesiegerCamp?.LeaderParty?.ActualClan;
            return $"Created siege event on '{settlement.StringId}' (besieger: {besieger?.StringId ?? "?"}). " +
                   "Run complete_siege_prep then advance ~1 hour for the capture trigger to fire.";
        }

        private static Clan? FindClan(string stringId)
            => TaleWorlds.CampaignSystem.Campaign.Current?.Clans.FirstOrDefault(c => c.StringId == stringId);

        private static string ListClans()
        {
            var clans = TaleWorlds.CampaignSystem.Campaign.Current?.Clans;
            if (clans is null) return "";

            var lines = clans
                .Where(c => !c.IsEliminated)
                .OrderBy(c => c.StringId)
                .Select(c => $"  {c.StringId}  ({c.Name}) [{(c.Kingdom != null ? c.Kingdom.Name.ToString() : "no kingdom")}]");
            return "Available clans:\n" + string.Join("\n", lines);
        }

        private static string ListNearestFortifications(Clan reference)
        {
            var settlements = TaleWorlds.CampaignSystem.Campaign.Current?.Settlements;
            if (settlements is null) return "";

            var refSettlement = reference.FactionMidSettlement ?? reference.Settlements.FirstOrDefault();
            var forts = settlements.Where(s => s.IsTown || s.IsCastle);
            if (refSettlement != null)
            {
                var refPos = refSettlement.GetPosition2D;
                forts = forts.OrderBy(s => s.GetPosition2D.DistanceSquared(refPos));
            }

            var lines = forts
                .Take(5)
                .Select(s => $"  {s.StringId}  ({s.Name}) {(s.IsTown ? "town" : "castle")} - owner {(s.OwnerClan != null ? s.OwnerClan.StringId : "?")}");
            return $"Nearest towns/castles to {reference.StringId}:\n" + string.Join("\n", lines);
        }

        private sealed class DebugCasusBelli : ICasusBelli
        {
            public string Type => "debug";
            public string TitleId => "debug";
        }
    }
}
