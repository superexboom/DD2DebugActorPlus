using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Code.Actor;
using Assets.Code.Affinity;
using Assets.Code.Affinity.Events;
using Assets.Code.Game;
using Assets.Code.Library;
using Assets.Code.Rules;
using Assets.Code.Source;
using Assets.Code.Utils;

namespace DD2DebugActorPlus
{
    /// <summary>
    /// One hero pair. The game stores relationships per unordered pair: a single affinity
    /// ("leaning") value and a single relationship shared by both heroes.
    /// </summary>
    internal sealed class RelationshipPair
    {
        public AffinityConnection Connection;
        public uint GuidA;
        public uint GuidB;
        public ActorInstance ActorA;
        public ActorInstance ActorB;
        public string NameA = "";
        public string NameB = "";
        public bool InvolvesFocus;
        public int Leaning;
        public int Min;
        public int Max;
        public AffinityLeaningLevelDefinition Level;
        public AffinityRelationshipDefinition Current;
        public AffinityRelationshipDefinition Pending;
        public bool HasDuration;
        public int DurationRemaining;

        public string Key => Math.Min(GuidA, GuidB) + "-" + Math.Max(GuidA, GuidB);
    }

    /// <summary>One-click relationship preset, ordered from the most negative to the most positive.</summary>
    internal sealed class RelationshipPreset
    {
        public AffinityRelationshipDefinition Definition;
        public string Id = "";
        public string Name = "";
        public bool IsPositive;
        public bool IsNeutral;
        public int TargetLeaning;
        /// <summary>-1 (most negative) .. 0 (neutral) .. +1 (most positive); used for colouring.</summary>
        public float Polarity;
    }

    internal sealed class LeaningLevel
    {
        public string Id = "";
        public string Name = "";
        public int Min;
        public int Max;
        public float NegativeChance;
        public float PositiveChance;
    }

    internal static class AffinityService
    {
        private const string TagPositive = "positive";
        private const string TagNegative = "negative";

        // How extreme the vanilla relationships are; mods without an entry are placed by their roll chance.
        private static readonly Dictionary<string, float> Extremity = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            { "suspicious", 1f },
            { "envious", 2f },
            { "resentful", 3f },
            { "hateful", 4f },
            { "hopeful", 1f },
            { "respectful", 2f },
            { "amorous", 3f },
        };

        internal static AffinityManager Manager
        {
            get
            {
                try
                {
                    return Singleton<GameTypeMgr>.HasInstance() ? Singleton<GameTypeMgr>.Instance.AffinityManager : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        internal static AffinityRules Rules
        {
            get
            {
                try
                {
                    return SingletonMonoBehaviour<RulesManager>.HasInstance(false) ? RulesManager.GetRules<AffinityRules>() : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        internal static int LeaningMin => SafeRule(rules => rules.m_LeaningMin, 0, 0, 100);
        internal static int LeaningMax => SafeRule(rules => rules.m_LeaningMax, 20, 1, 100);
        internal static int LeaningStart => SafeRule(rules => rules.m_LeaningStart, 9, 0, 100);

        private static int SafeRule(Func<AffinityRules, int> read, int fallback, int min, int max)
        {
            AffinityRules rules = Rules;
            if (rules == null) return fallback;
            int value = read(rules);
            return value < min || value > max ? fallback : value;
        }

        /// <summary>Active pairs (both heroes in the party). Pairs with <paramref name="focusGuid"/> come first, focus hero on the left.</summary>
        internal static List<RelationshipPair> GetPairs(uint focusGuid)
        {
            List<RelationshipPair> result = new List<RelationshipPair>();
            AffinityManager manager = Manager;
            if (manager == null) return result;

            List<uint> partyOrder = new List<uint>();
            try
            {
                IReadOnlyList<ActorInstance> party = Singleton<GameTypeMgr>.Instance.RosterManager?.GetPartyActors();
                if (party != null) partyOrder.AddRange(party.Where(actor => actor != null).Select(actor => actor.ActorGuid));
            }
            catch
            {
            }

            int count = manager.GetNumberOfConnections();
            for (int i = 0; i < count; i++)
            {
                AffinityConnection connection = manager.GetConnectionAtIndex(i);
                if (connection == null || connection.ActorGuids == null || connection.ActorGuids.Count < 2) continue;
                bool active;
                try
                {
                    active = connection.IsActive;
                }
                catch
                {
                    active = false;
                }

                if (!active) continue;

                uint a = connection.ActorGuids[0];
                uint b = connection.ActorGuids[1];
                bool involvesFocus = focusGuid != 0U && (a == focusGuid || b == focusGuid);
                if (involvesFocus && b == focusGuid)
                {
                    uint swap = a;
                    a = b;
                    b = swap;
                }
                else if (!involvesFocus && Index(partyOrder, b) < Index(partyOrder, a))
                {
                    uint swap = a;
                    a = b;
                    b = swap;
                }

                RelationshipPair pair = new RelationshipPair
                {
                    Connection = connection,
                    GuidA = a,
                    GuidB = b,
                    InvolvesFocus = involvesFocus,
                };
                Fill(pair);
                result.Add(pair);
            }

            result.Sort((x, y) =>
            {
                if (x.InvolvesFocus != y.InvolvesFocus) return x.InvolvesFocus ? -1 : 1;
                int ax = Index(partyOrder, x.GuidA), ay = Index(partyOrder, y.GuidA);
                if (ax != ay) return ax.CompareTo(ay);
                return Index(partyOrder, x.GuidB).CompareTo(Index(partyOrder, y.GuidB));
            });
            return result;
        }

        private static int Index(List<uint> order, uint guid)
        {
            int index = order.IndexOf(guid);
            return index < 0 ? int.MaxValue : index;
        }

        internal static void Fill(RelationshipPair pair)
        {
            AffinityConnection connection = pair.Connection;
            pair.ActorA = GameData.GetActor(pair.GuidA);
            pair.ActorB = GameData.GetActor(pair.GuidB);
            pair.NameA = GameText.ActorName(pair.ActorA);
            pair.NameB = GameText.ActorName(pair.ActorB);
            pair.Leaning = connection.GetLeaning();
            pair.Min = SafeConnectionValue(connection.GetLeaningMin, LeaningMin);
            pair.Max = SafeConnectionValue(connection.GetLeaningMax, LeaningMax);
            try
            {
                pair.Level = connection.GetAffinityLeaningLevel();
            }
            catch
            {
                pair.Level = null;
            }

            pair.Current = connection.GetCurrentRelationship();
            pair.Pending = connection.GetPendingRelationship();
            pair.HasDuration = connection.GetHasRelationshipDuration();
            try
            {
                pair.DurationRemaining = pair.HasDuration ? connection.GetDisplayedRelationshipDurationRemaining() : 0;
            }
            catch
            {
                pair.DurationRemaining = 0;
            }
        }

        private static int SafeConnectionValue(Func<int> read, int fallback)
        {
            try
            {
                int value = read();
                return value < -1000 || value > 1000 ? fallback : value;
            }
            catch
            {
                return fallback;
            }
        }

        internal static string Fingerprint(List<RelationshipPair> pairs)
        {
            if (pairs == null) return "";
            return string.Join(";", pairs.Select(pair =>
                pair.Key + ":" + pair.Connection.GetLeaning() + ":" +
                (pair.Connection.GetCurrentRelationship()?.m_Id ?? "-") + ":" +
                (pair.Connection.GetPendingRelationship()?.m_Id ?? "-")).ToArray());
        }

        internal static List<LeaningLevel> Levels()
        {
            List<LeaningLevel> levels = new List<LeaningLevel>();
            try
            {
                if (SingletonMonoBehaviour<Library<string, AffinityLeaningLevelDefinition>>.HasInstance(false))
                {
                    Library<string, AffinityLeaningLevelDefinition> library = SingletonMonoBehaviour<Library<string, AffinityLeaningLevelDefinition>>.Instance;
                    int count = library.GetNumberOfLibraryElements();
                    for (int i = 0; i < count; i++)
                    {
                        AffinityLeaningLevelDefinition definition = library.GetLibraryElementAtIndex(i);
                        if (definition == null) continue;
                        float negative = 0f, positive = 0f;
                        definition.RelationshipTagChances?.TryGetValue(TagNegative, out negative);
                        definition.RelationshipTagChances?.TryGetValue(TagPositive, out positive);
                        levels.Add(new LeaningLevel
                        {
                            Id = definition.m_Id,
                            Name = LevelName(definition.m_Id),
                            Min = definition.m_LeaningMin,
                            Max = definition.m_LeaningMax,
                            NegativeChance = negative,
                            PositiveChance = positive,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug("Affinity levels unavailable: " + ex.Message);
            }

            levels.Sort((a, b) => a.Min.CompareTo(b.Min));
            return levels;
        }

        internal static LeaningLevel LevelFor(List<LeaningLevel> levels, int leaning)
        {
            return levels?.FirstOrDefault(level => leaning >= level.Min && leaning <= level.Max);
        }

        internal static string LevelName(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId)) return "";
            return GameText.Localized(levelId, "affinity_leaning_level_" + levelId + "_simple", "affinity_leaning_level_" + levelId);
        }

        internal static string RelationshipName(AffinityRelationshipDefinition relationship)
        {
            if (relationship == null) return "";
            return GameText.Localized(relationship.m_Id, relationship.m_Id, "relationship_" + relationship.m_Id, "affinity_relationship_" + relationship.m_Id);
        }

        internal static bool IsPositive(AffinityRelationshipDefinition relationship)
        {
            return relationship?.m_Tags != null && relationship.m_Tags.Any(tag => string.Equals(tag, TagPositive, StringComparison.OrdinalIgnoreCase));
        }

        internal static List<RelationshipPreset> Presets()
        {
            List<AffinityRelationshipDefinition> negatives = new List<AffinityRelationshipDefinition>();
            List<AffinityRelationshipDefinition> positives = new List<AffinityRelationshipDefinition>();
            try
            {
                if (SingletonMonoBehaviour<Library<string, AffinityRelationshipDefinition>>.HasInstance(false))
                {
                    Library<string, AffinityRelationshipDefinition> library = SingletonMonoBehaviour<Library<string, AffinityRelationshipDefinition>>.Instance;
                    int count = library.GetNumberOfLibraryElements();
                    for (int i = 0; i < count; i++)
                    {
                        AffinityRelationshipDefinition definition = library.GetLibraryElementAtIndex(i);
                        if (definition == null || string.IsNullOrWhiteSpace(definition.m_Id)) continue;
                        if (IsPositive(definition)) positives.Add(definition);
                        else negatives.Add(definition);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug("Relationship presets unavailable: " + ex.Message);
            }

            negatives.Sort((a, b) =>
            {
                int compare = ExtremityOf(b).CompareTo(ExtremityOf(a));
                return compare != 0 ? compare : string.Compare(a.m_Id, b.m_Id, StringComparison.OrdinalIgnoreCase);
            });
            positives.Sort((a, b) =>
            {
                int compare = ExtremityOf(a).CompareTo(ExtremityOf(b));
                return compare != 0 ? compare : string.Compare(a.m_Id, b.m_Id, StringComparison.OrdinalIgnoreCase);
            });

            GetPresetRanges(out int negLow, out int negHigh, out int posLow, out int posHigh);
            List<RelationshipPreset> presets = new List<RelationshipPreset>();
            for (int i = 0; i < negatives.Count; i++)
            {
                presets.Add(new RelationshipPreset
                {
                    Definition = negatives[i],
                    Id = negatives[i].m_Id,
                    Name = RelationshipName(negatives[i]),
                    TargetLeaning = Spread(negLow, negHigh, i, negatives.Count),
                    Polarity = -1f + (negatives.Count <= 1 ? 0f : i * 0.75f / (negatives.Count - 1)),
                });
            }

            presets.Add(new RelationshipPreset
            {
                IsNeutral = true,
                Id = "",
                Name = L.T("Neutral", "中立"),
                TargetLeaning = Clamp(LeaningStart),
            });

            for (int i = 0; i < positives.Count; i++)
            {
                presets.Add(new RelationshipPreset
                {
                    Definition = positives[i],
                    Id = positives[i].m_Id,
                    Name = RelationshipName(positives[i]),
                    IsPositive = true,
                    TargetLeaning = Spread(posLow, posHigh, i, positives.Count),
                    Polarity = 0.25f + (positives.Count <= 1 ? 0.75f : i * 0.75f / (positives.Count - 1)),
                });
            }

            return presets;
        }

        private static float ExtremityOf(AffinityRelationshipDefinition definition)
        {
            if (Extremity.TryGetValue(definition.m_Id, out float value)) return value;
            return definition.m_Chance <= 1f ? 3.5f : 1.5f;
        }

        private static int Spread(int low, int high, int index, int count)
        {
            if (count <= 1) return Clamp(low);
            float t = index / (float)(count - 1);
            return Clamp((int)Math.Round(low + (high - low) * t, MidpointRounding.AwayFromZero));
        }

        private static int Clamp(int value)
        {
            return Math.Max(LeaningMin, Math.Min(LeaningMax, value));
        }

        /// <summary>Negative presets spread over the "only negative relationships can form" levels, positive ones over the positive levels.</summary>
        private static void GetPresetRanges(out int negLow, out int negHigh, out int posLow, out int posHigh)
        {
            int min = LeaningMin, max = LeaningMax;
            negLow = min;
            posHigh = max;
            List<LeaningLevel> levels = Levels();
            List<LeaningLevel> negative = levels.Where(level => level.NegativeChance > 0f && level.PositiveChance <= 0f).ToList();
            List<LeaningLevel> positive = levels.Where(level => level.PositiveChance > 0f && level.NegativeChance <= 0f).ToList();
            negHigh = negative.Count > 0 ? negative.Max(level => level.Max) : min + (max - min) * 2 / 5;
            posLow = positive.Count > 0 ? positive.Min(level => level.Min) : min + (max - min) * 7 / 10;
            if (negHigh < negLow) negHigh = negLow;
            if (posLow > posHigh) posLow = posHigh;
        }

        private static readonly FieldInfo LeaningField = typeof(AffinityConnection).GetField("m_Leaning", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Sets the pair's affinity through the game's API. Other mods can veto changes there (the
        /// cheat panel's "lock relationships" blocks every decrease); an explicit debug edit then
        /// writes the value directly and raises the same change event. Returns true in that case.
        /// </summary>
        private static bool WriteLeaning(AffinityConnection connection, int target)
        {
            connection.SetLeaning(target, null, SourceType.DEBUG);
            int actual = connection.GetLeaning();
            if (actual == target || LeaningField == null) return false;
            LeaningField.SetValue(connection, target);
            EventAffinityConnectionLeaningChange.Trigger(connection, target - actual, actual, null, null, SourceType.DEBUG);
            return true;
        }

        internal static bool SetLeaning(RelationshipPair pair, int value)
        {
            return WriteLeaning(pair.Connection, Math.Max(pair.Min, Math.Min(pair.Max, value)));
        }

        internal static bool Apply(RelationshipPair pair, RelationshipPreset preset, bool adjustLeaning)
        {
            AffinityConnection connection = pair.Connection;
            bool bypassed = false;
            if (adjustLeaning)
            {
                bypassed = WriteLeaning(connection, Math.Max(pair.Min, Math.Min(pair.Max, preset.TargetLeaning)));
            }

            if (preset.IsNeutral || preset.Definition == null)
            {
                connection.ResetCurrentRelationship();
                return bypassed;
            }

            if (connection.GetHasRelationship())
            {
                connection.ResetCurrentRelationship();
            }

            // Same path the game uses at the inn: calculated -> pending -> applied, so both heroes
            // get the relationship's act-outs, external buffs and skill modifiers.
            connection.SetCalculatedRelationship(preset.Definition);
            connection.SetCalculatedRelationshipToPending();
            connection.ApplyPendingRelationship();
            return bypassed;
        }

        internal static void Clear(RelationshipPair pair)
        {
            pair.Connection.ResetCurrentRelationship();
        }
    }
}
