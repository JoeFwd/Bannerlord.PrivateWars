using System.Collections.Generic;
using System.Globalization;
using Bannerlord.PrivateWars.Domain.Model;

namespace Bannerlord.PrivateWars.Spi
{
    /**
     * <summary>
     *  Converts <see cref="PrivateWar"/> records to and from flat, pipe-delimited strings that
     *  Bannerlord's <c>IDataStore.SyncData</c> persists natively, keeping the domain record free
     *  of <c>SaveableField</c> attributes. Mirrors <c>TitleStateSerialiser</c>.
     * </summary>
     * <remarks>
     *  Line format (15 fields joined with '|'):
     *  Id|Attacker|Defender|CasusBelliType|TitleId|MainGoalSettlementId|BattleScore|Score|StartDay|Status|OriginalFiefOwners|GoalLastTakenDay|AttackerSupporters|DefenderSupporters|ForeignSupporters
     *  <para>
     *  Fields are only ever appended, so older 11- to 14-field saves still load: GoalLastTakenDay
     *  defaults to StartDay and the supporter rosters, foreign supporters included, default to empty.
     *  </para>
     *  <para>
     *  OriginalFiefOwners is a sub-delimited map: entries separated by ';', settlementId and
     *  ownerClanId separated by ':' (e.g. <c>town_a:clan_1;castle_b:clan_2</c>); empty when no
     *  fiefs were captured. None of '|', ';' or ':' appear in Bannerlord StringIds or the
     *  configuration-defined title ids. Malformed entries are skipped rather than corrupting
     *  the load.
     *  </para>
     *  <para>
     *  AttackerSupporters and DefenderSupporters are ';'-separated clan id lists, empty when a side
     *  drew no support beyond its own vassal subtree. ForeignSupporters, in the same format, is the
     *  subset of both rosters that fights without its vassals.
     *  </para>
     * </remarks>
     */
    public static class PrivateWarStateSerialiser
    {
        private const char Delimiter = '|';
        private const char MapEntryDelimiter = ';';
        private const char MapPairDelimiter = ':';

        private static readonly string[] EmptyClanIds = new string[0];

        public static List<string> SerialiseWars(IReadOnlyList<PrivateWar> wars)
        {
            var serialised = new List<string>(wars.Count);
            foreach (var war in wars)
            {
                serialised.Add(string.Join(
                    Delimiter.ToString(),
                    war.Id,
                    war.AttackerPrincipalClanId,
                    war.DefenderPrincipalClanId,
                    war.CasusBelliType,
                    war.TitleId,
                    war.MainGoalSettlementId,
                    war.BattleScore.ToString("R", CultureInfo.InvariantCulture),
                    war.Score.ToString("R", CultureInfo.InvariantCulture),
                    war.StartDay.ToString("R", CultureInfo.InvariantCulture),
                    war.Status.ToString(),
                    SerialiseFiefOwners(war.OriginalFiefOwners),
                    war.GoalLastTakenDay.ToString("R", CultureInfo.InvariantCulture),
                    SerialiseClanIds(war.AttackerSupporters),
                    SerialiseClanIds(war.DefenderSupporters),
                    SerialiseClanIds(war.ForeignSupporters)));
            }

            return serialised;
        }

        public static List<PrivateWar> DeserialiseWars(List<string> serialisedWars)
        {
            var wars = new List<PrivateWar>(serialisedWars.Count);
            foreach (string line in serialisedWars)
            {
                if (line is null) continue;

                string[] fields = line.Split(Delimiter);
                if (fields.Length < 11 || fields.Length > 15) continue;
                if (!TryParseEnum(fields[9], out PrivateWarStatus status)) continue;
                if (!float.TryParse(fields[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float battleScore)) continue;
                if (!float.TryParse(fields[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float score)) continue;
                if (!float.TryParse(fields[8], NumberStyles.Float, CultureInfo.InvariantCulture, out float startDay)) continue;

                // Appended field; older saves omit it, so default the fatigue epoch to the war's start day.
                float goalLastTakenDay = startDay;
                if (fields.Length > 11
                    && float.TryParse(fields[11], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedGoalLastTakenDay))
                {
                    goalLastTakenDay = parsedGoalLastTakenDay;
                }

                wars.Add(new PrivateWar(
                    fields[0],
                    fields[1],
                    fields[2],
                    fields.Length > 12 ? DeserialiseClanIds(fields[12]) : EmptyClanIds,
                    fields.Length > 13 ? DeserialiseClanIds(fields[13]) : EmptyClanIds,
                    fields[3],
                    fields[4],
                    fields[5],
                    DeserialiseFiefOwners(fields[10]),
                    battleScore,
                    score,
                    startDay,
                    goalLastTakenDay,
                    status)
                {
                    ForeignSupporters = fields.Length > 14 ? DeserialiseClanIds(fields[14]) : EmptyClanIds
                });
            }

            return wars;
        }

        private static string SerialiseClanIds(IReadOnlyCollection<string> clanIds)
        {
            return string.Join(MapEntryDelimiter.ToString(), clanIds);
        }

        private static string[] DeserialiseClanIds(string field)
        {
            return field.Length == 0 ? EmptyClanIds : field.Split(MapEntryDelimiter);
        }

        private static string SerialiseFiefOwners(IReadOnlyDictionary<string, string> fiefOwners)
        {
            var entries = new List<string>(fiefOwners.Count);
            foreach (var pair in fiefOwners)
                entries.Add(pair.Key + MapPairDelimiter + pair.Value);

            return string.Join(MapEntryDelimiter.ToString(), entries);
        }

        private static Dictionary<string, string> DeserialiseFiefOwners(string field)
        {
            var fiefOwners = new Dictionary<string, string>();
            if (field.Length == 0) return fiefOwners;

            foreach (string entry in field.Split(MapEntryDelimiter))
            {
                string[] pair = entry.Split(MapPairDelimiter);
                if (pair.Length != 2) continue;
                fiefOwners[pair[0]] = pair[1];
            }

            return fiefOwners;
        }

        private static bool TryParseEnum<TEnum>(string value, out TEnum result) where TEnum : struct
        {
            return System.Enum.TryParse(value, out result) && System.Enum.IsDefined(typeof(TEnum), result);
        }
    }
}
