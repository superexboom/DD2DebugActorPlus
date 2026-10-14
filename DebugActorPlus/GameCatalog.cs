using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Actor.Queries;
using Assets.Code.Buff;
using Assets.Code.Dot;
using Assets.Code.Game;
using Assets.Code.Item;
using Assets.Code.Library;
using Assets.Code.Quirk;
using Assets.Code.Token;
using Assets.Code.Utils;
using UnityEngine;

namespace DD2DebugActorPlus
{
    internal enum EntryKind
    {
        Token,
        Buff,
        Dot,
        Quirk,
        Item,
    }

    internal sealed class ActorChoice
    {
        public ActorChoice(uint guid, string name, string dataId, int teamIndex, bool isHero)
        {
            Guid = guid;
            Name = name;
            DataId = dataId;
            TeamIndex = teamIndex;
            IsHero = isHero;
        }

        public uint Guid { get; }
        public string Name { get; }
        public string DataId { get; }
        public int TeamIndex { get; }
        public bool IsHero { get; }
    }

    internal sealed class CatalogEntry
    {
        private readonly PinyinSearch.PreparedSearch _search;

        public CatalogEntry(EntryKind kind, string id, string name, string detail, string category, object value)
        {
            Kind = kind;
            Id = id ?? "";
            Name = string.IsNullOrWhiteSpace(name) ? Id : name;
            Detail = detail ?? "";
            Category = category ?? "";
            Value = value;
            _search = PinyinSearch.Prepare(Name, Id, GameText.Plain(Detail));
        }

        public EntryKind Kind { get; }
        public string Id { get; }
        public string Name { get; }
        public string Detail { get; }
        public string Category { get; }
        public object Value { get; }

        public bool Matches(string query)
        {
            return _search == null || _search.Matches(query);
        }

        public Sprite Icon => GameData.IconFor(Kind, Value);
    }

    /// <summary>Everything the editor lists: actors in the current scene and the game's definition libraries.</summary>
    internal sealed class GameData
    {
        internal const string CategoryAll = "";
        internal const string CategoryPositive = "positive";
        internal const string CategoryNegative = "negative";
        internal const string CategoryOther = "other";
        internal const string CategoryDisease = "disease";
        internal const string CategoryCombatItem = "combat";
        internal const string CategoryTrinket = "trinket";

        public readonly List<ActorChoice> Actors = new List<ActorChoice>();
        public readonly List<CatalogEntry> Tokens = new List<CatalogEntry>();
        public readonly List<CatalogEntry> Buffs = new List<CatalogEntry>();
        public readonly List<CatalogEntry> Dots = new List<CatalogEntry>();
        public readonly List<CatalogEntry> Quirks = new List<CatalogEntry>();
        public readonly List<CatalogEntry> Items = new List<CatalogEntry>();
        public readonly List<string> DotTypes = new List<string>();

        public bool InCombat { get; private set; }

        /// <summary>Incremented whenever the definition libraries are reloaded.</summary>
        public int Version { get; private set; }

        public List<CatalogEntry> For(EntryKind kind)
        {
            switch (kind)
            {
                case EntryKind.Token: return Tokens;
                case EntryKind.Buff: return Buffs;
                case EntryKind.Dot: return Dots;
                case EntryKind.Quirk: return Quirks;
                default: return Items;
            }
        }

        public void Reload()
        {
            ReloadActors();
            Tokens.Clear();
            Buffs.Clear();
            Dots.Clear();
            Quirks.Clear();
            Items.Clear();
            DotTypes.Clear();

            LoadLibrary<TokenDefinition>(Tokens, EntryKind.Token, def => def.Id, GameText.TokenName, GameText.TokenDetail,
                def => def.IsPositive ? CategoryPositive : def.IsNegative ? CategoryNegative : CategoryOther);
            LoadLibrary<BuffDefinition>(Buffs, EntryKind.Buff, def => def.Id, GameText.BuffName, GameText.BuffDetail, def => CategoryAll);
            LoadLibrary<DotDefinition>(Dots, EntryKind.Dot, def => def.Id, GameText.DotName, GameText.DotDetail, GameText.DotType);
            LoadLibrary<QuirkDefinition>(Quirks, EntryKind.Quirk, def => def.Id, GameText.QuirkName, GameText.QuirkDetail,
                def => def.IsDisease || def.IsCurse ? CategoryDisease : def.IsNegative ? CategoryNegative : CategoryPositive);
            LoadItems();

            foreach (string type in Dots.Select(entry => entry.Category).Where(type => !string.IsNullOrWhiteSpace(type)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                DotTypes.Add(type);
            }

            DotTypes.Sort(StringComparer.OrdinalIgnoreCase);
            Version++;
        }

        /// <summary>
        /// Library sizes. The libraries are empty at the main menu and fill once a run loads, so a
        /// changed signature means the cached catalogs are stale.
        /// </summary>
        public static string LibrarySignature()
        {
            return Count<TokenDefinition>() + "/" + Count<BuffDefinition>() + "/" + Count<DotDefinition>() + "/" +
                Count<QuirkDefinition>() + "/" + Count<ItemDefinition>();
        }

        private static int Count<T>() where T : class, ILibraryElement<string>
        {
            try
            {
                return SingletonMonoBehaviour<Library<string, T>>.HasInstance(false)
                    ? SingletonMonoBehaviour<Library<string, T>>.Instance.GetNumberOfLibraryElements()
                    : -1;
            }
            catch
            {
                return -1;
            }
        }

        public void ReloadActors()
        {
            Actors.Clear();
            HashSet<uint> seen = new HashSet<uint>();
            InCombat = false;
            if (SingletonMonoBehaviour<Library<uint, ActorInstance>>.HasInstance(false))
            {
                for (int team = 0; team < 2; team++)
                {
                    QueryTeamActors query = QueryTeamActors.Trigger(team, true);
                    if (query?.m_TeamActorGuids == null) continue;
                    foreach (uint actorGuid in query.m_TeamActorGuids)
                    {
                        if (AddActor(actorGuid, team, team == 0, seen)) InCombat = true;
                    }
                }
            }

            if (Actors.Count == 0 && Singleton<GameTypeMgr>.HasInstance())
            {
                IReadOnlyList<ActorInstance> party = Singleton<GameTypeMgr>.Instance.RosterManager?.GetPartyActors();
                if (party != null)
                {
                    // Out of combat only the party exists, and its members are heroes whatever team index they report.
                    foreach (ActorInstance actor in party)
                    {
                        if (actor != null) AddActor(actor.ActorGuid, actor.TeamIndex, true, seen);
                    }
                }
            }
        }

        /// <summary>Cheap fingerprint of the actor list, used to notice spawns, deaths and scene changes.</summary>
        public static string ActorFingerprint()
        {
            try
            {
                List<uint> guids = new List<uint>();
                if (SingletonMonoBehaviour<Library<uint, ActorInstance>>.HasInstance(false))
                {
                    for (int team = 0; team < 2; team++)
                    {
                        QueryTeamActors query = QueryTeamActors.Trigger(team, true);
                        if (query?.m_TeamActorGuids != null) guids.AddRange(query.m_TeamActorGuids);
                    }
                }

                if (guids.Count == 0 && Singleton<GameTypeMgr>.HasInstance())
                {
                    IReadOnlyList<ActorInstance> party = Singleton<GameTypeMgr>.Instance.RosterManager?.GetPartyActors();
                    if (party != null) guids.AddRange(party.Where(actor => actor != null).Select(actor => actor.ActorGuid));
                }

                return string.Join(",", guids.Select(guid => guid.ToString()).ToArray());
            }
            catch
            {
                return "";
            }
        }

        private bool AddActor(uint actorGuid, int teamIndex, bool isHero, HashSet<uint> seen)
        {
            if (actorGuid == 0U || !seen.Add(actorGuid)) return false;
            ActorInstance actor = GetActor(actorGuid);
            if (actor == null) return false;
            Actors.Add(new ActorChoice(actorGuid, GameText.ActorName(actor), GameText.Plain(actor.ActorDataId), teamIndex, isHero));
            return true;
        }

        public static ActorInstance GetActor(uint actorGuid)
        {
            if (actorGuid == 0U || !SingletonMonoBehaviour<Library<uint, ActorInstance>>.HasInstance(false)) return null;
            try
            {
                return SingletonMonoBehaviour<Library<uint, ActorInstance>>.Instance.GetLibraryElement(actorGuid);
            }
            catch
            {
                return null;
            }
        }

        private static void LoadLibrary<T>(
            List<CatalogEntry> target,
            EntryKind kind,
            Func<T, string> idFunc,
            Func<T, string> nameFunc,
            Func<T, string> detailFunc,
            Func<T, string> categoryFunc) where T : class, ILibraryElement<string>
        {
            if (!SingletonMonoBehaviour<Library<string, T>>.HasInstance(false)) return;

            Library<string, T> library = SingletonMonoBehaviour<Library<string, T>>.Instance;
            int count = library.GetNumberOfLibraryElements();
            for (int i = 0; i < count; i++)
            {
                T value = library.GetLibraryElementAtIndex(i);
                if (value == null) continue;
                try
                {
                    string id = GameText.Plain(idFunc(value));
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    target.Add(new CatalogEntry(kind, id, nameFunc(value), detailFunc(value), categoryFunc(value), value));
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogDebug($"Skipped {typeof(T).Name} #{i}: {ex.Message}");
                }
            }

            Sort(target);
        }

        private void LoadItems()
        {
            if (!SingletonMonoBehaviour<Library<string, ItemDefinition>>.HasInstance(false)) return;
            Library<string, ItemDefinition> itemLibrary = SingletonMonoBehaviour<Library<string, ItemDefinition>>.Instance;
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            LibraryItem libraryItem = LibraryItem.LibraryItemInstance;
            if (libraryItem?.AllItemSkillIds != null)
            {
                foreach (string itemId in libraryItem.AllItemSkillIds)
                {
                    ItemDefinition item = itemLibrary.GetLibraryElement(itemId);
                    if (item == null || !seen.Add(item.m_id)) continue;
                    Items.Add(new CatalogEntry(EntryKind.Item, item.m_id, GameText.ItemName(item), GameText.ItemDetail(item), CategoryCombatItem, item));
                }
            }

            int count = itemLibrary.GetNumberOfLibraryElements();
            for (int i = 0; i < count; i++)
            {
                ItemDefinition item = itemLibrary.GetLibraryElementAtIndex(i);
                if (item == null || item.m_type != ItemType.TRINKET || !seen.Add(item.m_id)) continue;
                Items.Add(new CatalogEntry(EntryKind.Item, item.m_id, GameText.ItemName(item), GameText.ItemDetail(item), CategoryTrinket, item));
            }

            Sort(Items);
        }

        private static void Sort(List<CatalogEntry> entries)
        {
            entries.Sort((a, b) =>
            {
                int nameCompare = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return nameCompare != 0 ? nameCompare : string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
            });
        }

        public static Sprite IconFor(EntryKind kind, object value)
        {
            try
            {
                switch (kind)
                {
                    case EntryKind.Token:
                        return GameIcons.Token((value as TokenDefinition)?.Id);
                    case EntryKind.Buff:
                        return GameIcons.Buff(value as BuffDefinition);
                    case EntryKind.Dot:
                        return GameIcons.Dot(GameText.DotType(value as DotDefinition));
                    case EntryKind.Quirk:
                        return GameIcons.Quirk(value as QuirkDefinition);
                    case EntryKind.Item:
                        return GameIcons.Item(value as ItemDefinition);
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
