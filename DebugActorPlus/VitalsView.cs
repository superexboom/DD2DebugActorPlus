using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DD2DebugActorPlus
{
    internal sealed class VitalsView : EditorView
    {
        private VitalBlock _health;
        private VitalBlock _stress;
        private TextMeshProUGUI _overview;
        private string _fingerprint = "";

        public VitalsView(EditorScreen screen) : base(screen)
        {
        }

        public override string TabTitle => L.T("Health / Stress", "生命 / 压力");

        protected override void BuildContent()
        {
            HorizontalLayoutGroup row = U.Row(Root, "Row", 12f, null, TextAnchor.UpperLeft);
            U.Stretch((RectTransform)row.transform);
            row.childForceExpandHeight = true;

            _health = new VitalBlock(Screen, row.transform, true);
            _stress = new VitalBlock(Screen, row.transform, false);

            VerticalLayoutGroup overview = U.Column(row.transform, "Overview", 8f, U.Pad(16));
            U.Background(overview, U.Theme.Panel);
            U.Size(overview, 320f, -1f, -1f, 1f);
            U.Label(overview.transform, L.T("Current state", "当前状态"), FS.Emph, UiTheme.Gold, TextAlignmentOptions.MidlineLeft, true);
            U.Divider(overview.transform);
            ScrollView scroll = U.Scroll(overview.transform, "OverviewScroll");
            U.Size(scroll.Root, -1f, -1f, 1f, 1f);
            _overview = U.Label(scroll.Content, "", FS.Small, UiTheme.Text, TextAlignmentOptions.TopLeft, false, true);
        }

        public override void OnShown()
        {
            _fingerprint = "";
        }

        public override void Tick(bool force)
        {
            ActorInstance actor = Screen.SelectedActor;
            string fingerprint = actor == null ? "none" : actor.ActorGuid + "#" + GameActions.StateFingerprint(actor);
            if (!force && fingerprint == _fingerprint) return;
            _fingerprint = fingerprint;
            _health.Refresh(actor);
            _stress.Refresh(actor);
            _overview.text = actor == null ? L.T("No actor selected.", "未选择角色。") : Overview(actor);
        }

        private static string Overview(ActorInstance actor)
        {
            List<string> lines = new List<string>();
            AddSection(lines, L.T("Tokens", "标记"), GameActions.Owned(actor, EntryKind.Token));
            AddSection(lines, L.T("Buffs", "增益"), GameActions.Owned(actor, EntryKind.Buff));
            AddSection(lines, L.T("Damage over time", "持续伤害"), GameActions.Owned(actor, EntryKind.Dot));
            AddSection(lines, L.T("Quirks", "特质"), GameActions.Owned(actor, EntryKind.Quirk));
            AddSection(lines, L.T("Items", "物品"), GameActions.Owned(actor, EntryKind.Item).Where(entry => !entry.IsEmptySlot).ToList());
            return string.Join("\n", lines.ToArray());
        }

        private static void AddSection(List<string> lines, string title, List<OwnedEntry> entries)
        {
            lines.Add(U.Colored($"{title} · {entries.Count}", UiTheme.Gold));
            if (entries.Count == 0)
            {
                lines.Add(U.Colored(L.T("  none", "  无"), UiTheme.TextDim));
            }
            else
            {
                foreach (OwnedEntry entry in entries.Take(14))
                {
                    lines.Add("  " + entry.Name + (entry.Count > 1 ? " ×" + entry.Count : ""));
                }

                if (entries.Count > 14) lines.Add(U.Colored(L.F("  … {0} more", "  …… 另有 {0} 项", entries.Count - 14), UiTheme.TextDim));
            }

            lines.Add("");
        }

        private sealed class VitalBlock
        {
            private readonly EditorScreen _screen;
            private readonly bool _isHealth;
            private readonly TextMeshProUGUI _value;
            private readonly TextMeshProUGUI _note;
            private readonly BarView _bar;
            private readonly TMP_InputField _amount;
            private readonly List<ButtonView> _buttons = new List<ButtonView>();

            public VitalBlock(EditorScreen screen, Transform parent, bool isHealth)
            {
                _screen = screen;
                _isHealth = isHealth;

                VerticalLayoutGroup panel = U.Column(parent, isHealth ? "Health" : "Stress", 12f, U.Pad(18));
                U.Background(panel, U.Theme.Panel);
                U.Size(panel, -1f, -1f, 1f, 1f);

                HorizontalLayoutGroup header = U.Row(panel.transform, "Header", 8f);
                U.Size(header, -1f, 40f, 1f);
                TextMeshProUGUI title = U.Label(header.transform, isHealth ? L.T("Health", "生命") : L.T("Stress", "压力"), FS.Header, UiTheme.Gold, TextAlignmentOptions.MidlineLeft, true);
                U.Size(title, -1f, 40f, 1f);
                _value = U.Label(header.transform, "", FS.Big, UiTheme.Text, TextAlignmentOptions.MidlineRight, true);
                U.Size(_value, 180f, 40f);

                _bar = U.Bar(panel.transform, isHealth ? UiTheme.HealthFill : UiTheme.StressFill, 26f, FS.Small);

                U.Label(panel.transform, L.T("Amount", "数值"), FS.Small, UiTheme.TextMuted);
                HorizontalLayoutGroup amountRow = U.Row(panel.transform, "Amount", 6f);
                U.Size(amountRow, -1f, 38f, 1f);
                _amount = U.Input(amountRow.transform, "0", null, 38f, FS.Body);
                _amount.contentType = TMP_InputField.ContentType.IntegerNumber;
                _amount.text = isHealth ? "5" : "10";
                U.Size(_amount, 96f, 38f);
                _buttons.Add(U.Button(amountRow.transform, "−10", ButtonKind.Normal, () => Step(-10), 54f, 38f));
                _buttons.Add(U.Button(amountRow.transform, "−1", ButtonKind.Normal, () => Step(-1), 46f, 38f));
                _buttons.Add(U.Button(amountRow.transform, "+1", ButtonKind.Normal, () => Step(1), 46f, 38f));
                _buttons.Add(U.Button(amountRow.transform, "+10", ButtonKind.Normal, () => Step(10), 54f, 38f));
                _buttons.Add(U.Button(amountRow.transform, L.T("Max", "最大"), ButtonKind.Normal, () => _amount.text = Max().ToString(), -1f, 38f));

                HorizontalLayoutGroup actions = U.Row(panel.transform, "Actions", 8f);
                U.Size(actions, -1f, 44f, 1f);
                if (isHealth)
                {
                    _buttons.Add(Action(actions.transform, L.T("Deal damage", "造成伤害"), ButtonKind.Danger, (actor, amount) => GameActions.Damage(actor, amount), L.T("Dealt {0} damage", "造成 {0} 点伤害")));
                    _buttons.Add(Action(actions.transform, L.T("Heal", "恢复生命"), ButtonKind.Primary, (actor, amount) => GameActions.Heal(actor, amount), L.T("Healed {0}", "恢复 {0} 点生命")));
                    _buttons.Add(Action(actions.transform, L.T("Set to amount", "设为该数值"), ButtonKind.Normal, (actor, amount) => GameActions.SetHealth(actor, amount), L.T("Health set to {0}", "生命设为 {0}")));
                }
                else
                {
                    _buttons.Add(Action(actions.transform, L.T("Add stress", "增加压力"), ButtonKind.Danger, (actor, amount) => GameActions.AddStress(actor, amount), L.T("Added {0} stress", "增加 {0} 点压力")));
                    _buttons.Add(Action(actions.transform, L.T("Relieve stress", "降低压力"), ButtonKind.Primary, (actor, amount) => GameActions.ReduceStress(actor, amount), L.T("Relieved {0} stress", "降低 {0} 点压力")));
                    _buttons.Add(Action(actions.transform, L.T("Set to amount", "设为该数值"), ButtonKind.Normal, (actor, amount) => GameActions.SetStress(actor, amount), L.T("Stress set to {0}", "压力设为 {0}")));
                }

                U.Label(panel.transform, L.T("Quick", "快捷"), FS.Small, UiTheme.TextMuted);
                HorizontalLayoutGroup quick = U.Row(panel.transform, "Quick", 8f);
                U.Size(quick, -1f, 40f, 1f);
                if (isHealth)
                {
                    _buttons.Add(Quick(quick.transform, L.T("Full health", "回满生命"), actor => GameActions.SetHealth(actor, Mathf.RoundToInt(actor.DisplayedHpMax)), L.T("Health restored to full", "生命已回满")));
                    _buttons.Add(Quick(quick.transform, L.T("Set to 1", "设为 1"), actor => GameActions.SetHealth(actor, 1), L.T("Health set to 1", "生命设为 1")));
                }
                else
                {
                    _buttons.Add(Quick(quick.transform, L.T("Clear stress", "清空压力"), actor => GameActions.SetStress(actor, 0), L.T("Stress cleared", "压力已清空")));
                    _buttons.Add(Quick(quick.transform, L.T("Max stress", "压力拉满"), actor => GameActions.SetStress(actor, Mathf.RoundToInt(actor.StressMax)), L.T("Stress set to max", "压力已拉满")));
                }

                U.Spacer(panel.transform, -1f, -1f, false);
                _note = U.Label(panel.transform, "", FS.Small, UiTheme.TextDim, TextAlignmentOptions.TopLeft, false, true);
                U.Size(_note, -1f, -1f, 1f, 1f);
            }

            private ButtonView Action(Transform parent, string label, ButtonKind kind, Action<ActorInstance, int> apply, string message)
            {
                ButtonView button = U.Button(parent, label, kind, () =>
                {
                    ActorInstance actor = _screen.SelectedActor;
                    if (actor == null) return;
                    int amount = Amount();
                    _screen.Run(string.Format(message, amount) + " → " + _screen.SelectedName, () => apply(actor, amount));
                }, -1f, 44f, FS.Body);
                U.Size(button.GameObject.transform, -1f, 44f, 1f);
                return button;
            }

            private ButtonView Quick(Transform parent, string label, Action<ActorInstance> apply, string message)
            {
                return U.Button(parent, label, ButtonKind.Normal, () =>
                {
                    ActorInstance actor = _screen.SelectedActor;
                    if (actor == null) return;
                    _screen.Run(message + " → " + _screen.SelectedName, () => apply(actor));
                }, -1f, 40f);
            }

            private int Max()
            {
                ActorInstance actor = _screen.SelectedActor;
                if (actor == null) return 0;
                return Mathf.RoundToInt(_isHealth ? actor.DisplayedHpMax : actor.StressMax);
            }

            private int Amount()
            {
                if (!int.TryParse(_amount.text, out int value)) value = 0;
                return Mathf.Clamp(value, 0, Mathf.Max(999, Max()));
            }

            private void Step(int delta)
            {
                _amount.text = Mathf.Clamp(Amount() + delta, 0, Mathf.Max(999, Max())).ToString();
            }

            public void Refresh(ActorInstance actor)
            {
                bool enabled = actor != null && (_isHealth || actor.StressMax > 0f);
                foreach (ButtonView button in _buttons) button.SetInteractable(enabled);
                _amount.interactable = enabled;
                if (actor == null)
                {
                    _value.text = "—";
                    _bar.Set(0f, 1f, "");
                    _note.text = L.T("Select an actor above.", "请在上方选择一个角色。");
                    return;
                }

                if (_isHealth)
                {
                    _value.text = $"{actor.DisplayedHp:0} / {actor.DisplayedHpMax:0}";
                    _bar.Set(actor.DisplayedHp, actor.DisplayedHpMax, $"{actor.DisplayedHp:0} / {actor.DisplayedHpMax:0}");
                    _note.text = L.T("Damage goes through the game's normal damage path (death's door and deaths can happen).",
                        "伤害走游戏正常的伤害流程，可能触发死门或死亡。");
                }
                else if (actor.StressMax > 0f)
                {
                    _value.text = $"{actor.Stress:0} / {actor.StressMax:0}";
                    _bar.Set(actor.Stress, actor.StressMax, $"{actor.Stress:0} / {actor.StressMax:0}");
                    _note.text = L.T("Reaching max stress triggers the game's breakdown (meltdown) logic.",
                        "压力达到上限会触发游戏的崩溃判定。");
                }
                else
                {
                    _value.text = "—";
                    _bar.Set(0f, 1f, "");
                    _note.text = L.T("This actor has no stress.", "这个角色没有压力。");
                }
            }
        }
    }
}
