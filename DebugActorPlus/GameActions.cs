using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using Assets.Code.Buff;
using Assets.Code.Dot;
using Assets.Code.Item;
using Assets.Code.Quirk;
using Assets.Code.Source;
using Assets.Code.Token;
using Assets.Code.Trinket;
using Assets.Code.UI.Events;
using UnityEngine;

namespace DD2DebugActorPlus
{
    /// <summary>Something the selected actor currently has (left column of a tab).</summary>
    internal sealed class OwnedEntry
    {
        public EntryKind Kind;
        public string Id = "";
        public string Name = "";
        public string Detail = "";
        public string Subtitle = "";
        public string Group = "";
        public int Count = 1;
        public object Value;
        public Sprite Icon;
        public bool IsEmptySlot;
        public Action Remove;
    }

    internal sealed class AddOptions
    {
        public bool LockedTeamPosition;
        public bool DotCrit;
    }

    /// <summary>Game mutations, all routed through the game's own debug events or public APIs.</summary>
    internal static class GameActions
    {
        internal static void Damage(ActorInstance actor, int amount)
        {
            actor.ApplyHealthDamage(amount, false, false, actor, DeathType.DEBUG, SourceType.DEBUG, null, false);
        }

        internal static void Heal(ActorInstance actor, int amount)
        {
            actor.ApplyHealthHeal(amount, false, SourceType.DEBUG, false);
        }

        internal static void SetHealth(ActorInstance actor, int target)
        {
            int current = Mathf.RoundToInt(actor.DisplayedHp);
            target = Mathf.Clamp(target, 0, Mathf.RoundToInt(actor.DisplayedHpMax));
            if (target > current) Heal(actor, target - current);
            else if (target < current) Damage(actor, current - target);
        }

        internal static void AddStress(ActorInstance actor, int amount)
        {
            actor.ApplyStressDamage(amount, false, SourceType.DEBUG, null, 0U);
        }

        internal static void ReduceStress(ActorInstance actor, int amount)
        {
            actor.ApplyStressHeal(amount, SourceType.DEBUG);
        }

        internal static void SetStress(ActorInstance actor, int target)
        {
            int current = Mathf.RoundToInt(actor.Stress);
            target = Mathf.Clamp(target, 0, Mathf.RoundToInt(actor.StressMax));
            if (target > current) AddStress(actor, target - current);
            else if (target < current) ReduceStress(actor, current - target);
        }

        internal static void Add(ActorInstance actor, CatalogEntry entry, AddOptions options)
        {
            if (actor == null || entry == null) throw new InvalidOperationException(L.T("No actor selected.", "未选择角色。"));
            options = options ?? new AddOptions();
            switch (entry.Kind)
            {
                case EntryKind.Token:
                    EventDebugActorAddToken.Trigger(actor.ActorGuid, (TokenDefinition)entry.Value, options.LockedTeamPosition, actor.ActorGuid);
                    break;
                case EntryKind.Buff:
                    EventDebugActorAddBuff.Trigger(actor.ActorGuid, (BuffDefinition)entry.Value, options.LockedTeamPosition);
                    break;
                case EntryKind.Dot:
                    EventDebugActorAddDot.Trigger(actor.ActorGuid, (DotDefinition)entry.Value, options.LockedTeamPosition, actor.ActorGuid, options.DotCrit);
                    break;
                case EntryKind.Quirk:
                    EventDebugActorAddQuirk.Trigger(actor.ActorGuid, (QuirkDefinition)entry.Value);
                    break;
                case EntryKind.Item:
                    AddItem(actor, (ItemDefinition)entry.Value, entry.Category == GameData.CategoryTrinket);
                    break;
            }
        }

        internal static void RemoveAll(ActorInstance actor, CatalogEntry entry)
        {
            if (actor == null || entry == null) return;
            switch (entry.Kind)
            {
                case EntryKind.Token:
                    EventDebugActorRemoveToken.Trigger(actor.ActorGuid, (TokenDefinition)entry.Value);
                    break;
                case EntryKind.Buff:
                    EventDebugActorRemoveBuff.Trigger(actor.ActorGuid, (BuffDefinition)entry.Value);
                    break;
                case EntryKind.Dot:
                    EventDebugActorRemoveDot.Trigger(actor.ActorGuid, (DotDefinition)entry.Value);
                    break;
                case EntryKind.Quirk:
                    EventDebugActorRemoveQuirk.Trigger(actor.ActorGuid, (QuirkDefinition)entry.Value);
                    break;
                case EntryKind.Item:
                    RemoveItem(actor, (ItemDefinition)entry.Value);
                    break;
            }
        }

        private static void AddItem(ActorInstance actor, ItemDefinition item, bool trinket)
        {
            if (item == null) return;
            if (!trinket)
            {
                EventDebugActorAddCombatItem.Trigger(actor.ActorGuid, item);
                return;
            }

            ItemInventory inventory = actor.GetTrinketInventory();
            if (inventory == null)
            {
                throw new InvalidOperationException(L.T("This actor has no trinket slots.", "该角色没有饰品栏。"));
            }

            if (inventory is TrinketItemInventory trinketInventory && !trinketInventory.GetCanEquip(item, false, true))
            {
                throw new InvalidOperationException(L.T("This actor cannot equip that trinket.", "该角色不满足这个饰品的装备条件。"));
            }

            int leftover = inventory.AddItems(item, 1, false);
            if (leftover > 0)
            {
                throw new InvalidOperationException(L.T("Trinket slots are full.", "饰品栏已满。"));
            }
        }

        private static void RemoveItem(ActorInstance actor, ItemDefinition item)
        {
            foreach (ItemInventory inventory in new[] { actor.GetCombatSkillInventory(), actor.GetTrinketInventory() })
            {
                if (inventory == null) continue;
                IReadOnlyList<IReadOnlyItemInstance> items = inventory.GetItems();
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i]?.GetItemDefinition() == item)
                    {
                        inventory.RemoveItemAt(i, false);
                        return;
                    }
                }
            }
        }

        internal static bool Has(ActorInstance actor, CatalogEntry entry)
        {
            if (actor == null || entry == null) return false;
            try
            {
                switch (entry.Kind)
                {
                    case EntryKind.Token:
                        return actor.TokenContainer?.GetInstances().Any(instance => instance?.Definition == entry.Value) == true;
                    case EntryKind.Buff:
                        return actor.BuffContainer?.GetInstances().Any(instance => instance?.Definition == entry.Value) == true;
                    case EntryKind.Dot:
                        return actor.DotContainer?.GetInstances().Any(instance => instance?.Definition == entry.Value) == true;
                    case EntryKind.Quirk:
                        return actor.QuirkContainer?.GetInstances().Any(instance => instance?.Definition == entry.Value) == true;
                    case EntryKind.Item:
                        return new[] { actor.GetCombatSkillInventory(), actor.GetTrinketInventory() }
                            .Where(inventory => inventory != null)
                            .Any(inventory => inventory.GetItems().Any(item => item?.GetItemDefinition() == entry.Value));
                }
            }
            catch
            {
            }

            return false;
        }

        internal static List<OwnedEntry> Owned(ActorInstance actor, EntryKind kind)
        {
            List<OwnedEntry> result = new List<OwnedEntry>();
            if (actor == null) return result;
            uint guid = actor.ActorGuid;
            switch (kind)
            {
                case EntryKind.Token:
                    Group(result, actor.TokenContainer?.GetInstances().Select(instance => instance?.Definition), def => def.Id, def => new OwnedEntry
                    {
                        Kind = kind,
                        Id = def.Id,
                        Name = GameText.TokenName(def),
                        Detail = GameText.TokenDetail(def),
                        Subtitle = def.IsPositive ? L.T("Positive token", "正面标记") : def.IsNegative ? L.T("Negative token", "负面标记") : L.T("Token", "标记"),
                        Icon = GameIcons.Token(def.Id),
                        Value = def,
                        Remove = () => EventDebugActorRemoveToken.Trigger(guid, def),
                    });
                    break;
                case EntryKind.Buff:
                    Group(result, actor.BuffContainer?.GetInstances().Select(instance => instance?.Definition), def => def.Id, def => new OwnedEntry
                    {
                        Kind = kind,
                        Id = def.Id,
                        Name = GameText.BuffName(def),
                        Detail = GameText.BuffDetail(def),
                        Subtitle = L.T("Buff", "增益"),
                        Icon = GameIcons.Buff(def),
                        Value = def,
                        Remove = () => EventDebugActorRemoveBuff.Trigger(guid, def),
                    });
                    break;
                case EntryKind.Dot:
                    Group(result, actor.DotContainer?.GetInstances().Select(instance => instance?.Definition), def => def.Id, def => new OwnedEntry
                    {
                        Kind = kind,
                        Id = def.Id,
                        Name = GameText.DotName(def),
                        Detail = GameText.DotDetail(def),
                        Subtitle = GameText.DotTypeName(GameText.DotType(def)),
                        Icon = GameIcons.Dot(GameText.DotType(def)),
                        Value = def,
                        Remove = () => EventDebugActorRemoveDot.Trigger(guid, def),
                    });
                    break;
                case EntryKind.Quirk:
                    Group(result, actor.QuirkContainer?.GetInstances().Select(instance => instance?.Definition), def => def.Id, def => new OwnedEntry
                    {
                        Kind = kind,
                        Id = def.Id,
                        Name = GameText.QuirkName(def),
                        Detail = GameText.QuirkDetail(def),
                        Subtitle = GameText.QuirkKindLabel(def),
                        Group = def.IsDisease || def.IsCurse ? GameData.CategoryDisease : def.IsNegative ? GameData.CategoryNegative : GameData.CategoryPositive,
                        Icon = GameIcons.Quirk(def),
                        Value = def,
                        Remove = () => EventDebugActorRemoveQuirk.Trigger(guid, def),
                    });
                    break;
                case EntryKind.Item:
                    AddInventory(result, actor.GetCombatSkillInventory(), GameData.CategoryCombatItem);
                    AddInventory(result, actor.GetTrinketInventory(), GameData.CategoryTrinket);
                    break;
            }

            return result;
        }

        private static void Group<T>(List<OwnedEntry> result, IEnumerable<T> definitions, Func<T, string> key, Func<T, OwnedEntry> create) where T : class
        {
            if (definitions == null) return;
            Dictionary<string, OwnedEntry> byId = new Dictionary<string, OwnedEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (T definition in definitions)
            {
                if (definition == null) continue;
                string id = key(definition) ?? "";
                if (byId.TryGetValue(id, out OwnedEntry existing))
                {
                    existing.Count++;
                    continue;
                }

                OwnedEntry entry = create(definition);
                byId[id] = entry;
                result.Add(entry);
            }
        }

        private static void AddInventory(List<OwnedEntry> result, ItemInventory inventory, string group)
        {
            if (inventory == null) return;
            IReadOnlyList<IReadOnlyItemInstance> items = inventory.GetItems();
            int total = inventory.GetNumberOfTotalSlots();
            for (int i = 0; i < total; i++)
            {
                IReadOnlyItemInstance item = i < items.Count ? items[i] : null;
                ItemDefinition definition = item?.GetItemDefinition();
                int index = i;
                if (definition == null)
                {
                    result.Add(new OwnedEntry
                    {
                        Kind = EntryKind.Item,
                        Group = group,
                        IsEmptySlot = true,
                        Name = L.T("Empty slot", "空槽"),
                        Subtitle = group == GameData.CategoryTrinket ? L.T("Trinket slot", "饰品栏") : L.T("Combat item slot", "战斗物品栏"),
                    });
                    continue;
                }

                result.Add(new OwnedEntry
                {
                    Kind = EntryKind.Item,
                    Id = definition.m_id,
                    Name = GameText.ItemName(definition),
                    Detail = GameText.ItemDetail(definition),
                    Subtitle = (group == GameData.CategoryTrinket ? L.T("Trinket", "饰品") : L.T("Combat item", "战斗物品")) + "  ×" + item.GetQty(),
                    Group = group,
                    Icon = GameIcons.Item(definition),
                    Value = definition,
                    Remove = () => inventory.RemoveItemAt(index, false),
                });
            }
        }

        /// <summary>Hash of everything the open tab shows, so the UI only rebuilds when the game state changed.</summary>
        internal static string StateFingerprint(ActorInstance actor)
        {
            if (actor == null) return "";
            try
            {
                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                builder.Append(Mathf.RoundToInt(actor.DisplayedHp)).Append('/').Append(Mathf.RoundToInt(actor.DisplayedHpMax)).Append('|');
                builder.Append(Mathf.RoundToInt(actor.Stress)).Append('/').Append(Mathf.RoundToInt(actor.StressMax)).Append('|');
                AppendIds(builder, actor.TokenContainer?.GetInstances().Select(instance => instance?.Definition?.Id));
                AppendIds(builder, actor.BuffContainer?.GetInstances().Select(instance => instance?.Definition?.Id));
                AppendIds(builder, actor.DotContainer?.GetInstances().Select(instance => instance?.Definition?.Id));
                AppendIds(builder, actor.QuirkContainer?.GetInstances().Select(instance => instance?.Definition?.Id));
                foreach (ItemInventory inventory in new[] { actor.GetCombatSkillInventory(), actor.GetTrinketInventory() })
                {
                    if (inventory == null) continue;
                    builder.Append(inventory.GetNumberOfTotalSlots()).Append(':');
                    AppendIds(builder, inventory.GetItems().Select(item => item == null ? "-" : item.GetItemDefinition()?.m_id + "x" + item.GetQty()));
                }

                return builder.ToString();
            }
            catch
            {
                return "";
            }
        }

        private static void AppendIds(System.Text.StringBuilder builder, IEnumerable<string> ids)
        {
            if (ids != null)
            {
                foreach (string id in ids) builder.Append(id).Append(',');
            }

            builder.Append('|');
        }
    }
}
