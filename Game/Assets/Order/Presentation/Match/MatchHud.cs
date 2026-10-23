using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Order.Application;
using Order.Content;
using Order.Contracts;
using Order.Input;
using Order.Simulation;
using Order.Voice;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Order.Presentation
{
    public enum MatchMode { None, Build, Move, Harvest, Ping, Attack, Rally }

    // What the HUD needs from the running match view.
    public interface IMatchHost
    {
        PrototypeMatch Match { get; }
        MatchMode Mode { get; }
        int BuildKind { get; }
        PrototypeBuilding SelectedBuilding { get; }
        ResourceDeposit SelectedDeposit { get; }
        IReadOnlyList<Vector2> Pins { get; }
        // Recent attacks on the player away from the camera: where and when (unscaled time), for the minimap.
        IReadOnlyList<(Vector2 point, float time)> Alerts { get; }
        UnitSelection Selection { get; }
        Texture MinimapTexture { get; }
        string ModeHint { get; }
        void SetMode(MatchMode mode, int buildKind = -1);
        void SelectWorker();
        void SelectArmy();
        void SelectKind(UnitKind kind);
        void RecallGroup(int index);
        bool MoveSelection(Vector2 point);
        bool AttackMoveSelection(Vector2 point);
        void HoldSelection();
        bool Train(UnitKind kind);
        void CancelTraining(int index);
        void SetRallyPoint(Vector2 point);
        // Construction: a worker goes to build with its corner at (x, z); the selected building under construction is
        // cancelled. BuildOrders counts the build orders given, so the card can leave the building list after one.
        bool Build(int x, int z, int kind);
        bool CancelConstruction();
        int BuildOrders { get; }
        void Stop();
        void Focus(Vector2 point);
        void FocusWorker();
        void FocusSelection();
        // Ground point in the middle of the view: where cheat units appear.
        Vector2 CameraFocus { get; }
        void FocusBase();
        void OrderTo(Vector2 point);
        void AddPin(Vector2 point);
        void ClearPins();
        Vector2[] ViewportOnGround();
        void Exit();
        // A new match on this screen with the given factions (the editor cheat panel).
        void Restart(FactionId player, FactionId enemy);
    }

    // Application services when the match runs from the command centre; null in the standalone test scene.
    public sealed class MatchServices
    {
        public AppController App;
        public AppCommandInput Input;
        public Phrasebook Phrasebook;
    }

    // Match HUD in the player's faction theme: resources on top; minimap, assistant portrait, selection and
    // command card on the bottom deck. The assistant takes typed orders (Enter) for the same actions as the buttons.
    public sealed class MatchHud : IDisposable
    {
        private const int Slots = 15;
        private readonly VisualElement root, screen;
        private readonly IMatchHost host;
        private readonly MatchServices services;
        private readonly StyleSheet style;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly float started = Time.realtimeSinceStartup;
        private HudCursor cursor;
        private readonly HudSound sound;
        private readonly MinimapPanel minimap;
        private readonly Label metal, metalRate, plasma, plasmaRate, supply, timer, eventLine;
        private readonly AssistantCore core;
        private readonly HudStatus mood;
        private readonly Label assistantName, subtitle;
        private readonly HudIcon selectionIcon;
        private readonly Label selectionName, selectionStatus, selectionStats, selectionEmpty;
        private readonly VisualElement upgrades, selectionPanel, selectionBody, unitArea, unitPages, unitGrid, groupTabs, selectionBox, queueRow;
        private readonly List<VisualElement> unitTiles = new List<VisualElement>(), tabs = new List<VisualElement>(), queueTiles = new List<VisualElement>(), pageButtons = new List<VisualElement>();
        // A big selection is shown a page at a time, as in StarCraft II: up to three rows of tiles, as many as the panel
        // has room for, and as many columns as fit.
        private const int TileRows = 3;
        private const float TileStep = 46, TileStepY = 50, PageStrip = 30;
        private int unitPage;
        private PrototypeUnit pagedFirst;
        // The building whose production queue is shown.
        private PrototypeBuilding queue;
#if UNITY_EDITOR
        // Cheat menu of the test ground; editor only.
        private readonly CheatPanel cheats;
#endif
        private readonly HudBar health, shields, progress;
        private readonly Label commandsTitle, banner, modeLabel;
        private readonly Button[] buttons = new Button[Slots];
        private readonly HudAction[] actions = new HudAction[Slots];
        private readonly VisualElement tooltip;
        private readonly Label tooltipTitle, tooltipKey, tooltipCost, tooltipText, tooltipLock;
        private readonly VisualElement commandLine;
        private readonly TextField commandField;
        private readonly OptionsWindow options;
        private readonly HudModal exitDialog;
        private readonly PhrasebookPanel phrasebookPanel;
        private readonly Queue<(float time, int metal, int plasma)> income = new Queue<(float, int, int)>();
        private int gainedMetal, gainedPlasma, closedFrame = -1, openedFrame = -1;
        private string lastMessage, lastHint, theme, speech = "";
        private float eventUntil, speechStart, speechDone, subtitleUntil;
        private int shown;
        private bool busy, disposed, speechAlert, lineFocused, quiet;
        private int hovered = -1;
        // Worker command card page: main orders or the list of buildings (B), as in StarCraft.
        private bool buildPage;
        private int builtCount = -1;

        public MatchHud(VisualElement root, IMatchHost host, MatchServices services)
        {
            this.root = root; this.host = host; this.services = services;
            root.Clear();
            root.styleSheets.Clear();
            style = Resources.Load<StyleSheet>("MatchHud");
            if (style != null) root.styleSheets.Add(style);
            screen = Hud.Box("match-hud", "match-hud");
            screen.pickingMode = PickingMode.Ignore;
            root.Add(screen);
            HudFonts.Apply(screen);
            var layout = Hud.Box("match-hud__layout"); layout.pickingMode = PickingMode.Ignore;
            screen.Add(layout);

            // Top bar
            var top = Hud.Box("match-top"); top.AddToClassList("match-hit");
            top.Add(new HudFrame(0, 14, 0, 14));
            var left = Hud.Box("match-top__left");
            var menu = Hud.Button("МЕНЮ", OpenMenu, "hud-button--ghost"); menu.Add(Hud.Keycap("F10"));
            left.Add(menu);
            left.Add(Hud.Button("В ШТАБ", Leave, "hud-button--ghost"));
            var title = Hud.Box("match-top__title");
            title.Add(Hud.Text("TEMPLE BASIN", "match-top__map"));
            modeLabel = Hud.Text("", "match-top__mode");
            title.Add(modeLabel);
            left.Add(title);
            top.Add(left);
            var resources = Hud.Box("match-resources");
            metal = Resource(resources, HudIconKind.Metal, "МЕТАЛЛ", out metalRate);
            plasma = Resource(resources, HudIconKind.Plasma, "ПЛАЗМА", out plasmaRate);
            supply = Resource(resources, HudIconKind.Supply, "ЛИМИТ", out _);
            top.Add(resources);
            var right = Hud.Box("match-top__right");
            right.Add(Hud.Text("ВРЕМЯ МАТЧА", "match-top__caption"));
            timer = Hud.Text("00:00", "match-top__timer");
            right.Add(timer);
            top.Add(right);
            layout.Add(top);
#if UNITY_EDITOR
            cheats = new CheatPanel(screen, left, host, Refresh);
#endif

            eventLine = Hud.Text("", "match-event");
            layout.Add(eventLine);

            // Bottom deck
            var deck = Hud.Box("match-deck"); deck.pickingMode = PickingMode.Ignore;
            minimap = new MinimapPanel(host);
            deck.Add(minimap.Root);

            var portrait = Hud.Box("match-portrait"); portrait.AddToClassList("match-hit");
            portrait.Add(new HudFrame(10, 10));
            var ring = Hud.Box("match-portrait__ring");
            ring.Add(new PortraitRing());
            core = new AssistantCore(); core.AddToClassList("match-portrait__core");
            ring.Add(core);
            portrait.Add(ring);
            assistantName = Hud.Text("", "match-portrait__name");
            portrait.Add(assistantName);
            mood = new HudStatus();
            portrait.Add(mood);
            var hint = Hud.Box("match-portrait__hint");
            hint.Add(Hud.Keycap("ENTER"));
            hint.Add(Hud.Text("ПРИКАЗ", "match-portrait__hint-text"));
            portrait.Add(hint);
            deck.Add(portrait);

            var selection = selectionPanel = Hud.Box("match-selection"); selection.AddToClassList("match-hit");
            selection.Add(new HudFrame(12, 12));
            subtitle = Hud.Text("", "match-subtitle");
            selection.Add(subtitle);
            var header = Hud.Box("match-selection__header");
            var tile = Hud.Box("match-selection__tile");
            tile.Add(new HudFrame(8, 8));
            selectionIcon = new HudIcon(HudIconKind.Worker); selectionIcon.AddToClassList("match-selection__icon");
            tile.Add(selectionIcon);
            header.Add(tile);
            var titles = Hud.Box("match-selection__titles");
            selectionName = Hud.Text("", "match-selection__name");
            selectionStatus = Hud.Text("", "match-selection__status");
            titles.Add(selectionName); titles.Add(selectionStatus);
            header.Add(titles);
            upgrades = Hud.Box("match-upgrades");
            header.Add(upgrades);
            selection.Add(header);
            selectionBody = Hud.Box("match-selection__body");
            health = new HudBar("ПРОЧНОСТЬ", "health");
            shields = new HudBar("ЩИТЫ", "shield");
            progress = new HudBar("СТРОИТЕЛЬСТВО", "progress", 20);
            selectionBody.Add(health); selectionBody.Add(shields); selectionBody.Add(progress);
            selectionStats = Hud.Text("", "match-selection__stats");
            selectionBody.Add(selectionStats);
            // Production queue: the unit in training first; a click cancels and refunds.
            queueRow = Hud.Box("match-queue");
            for (var i = 0; i < PrototypeMatch.QueueSize; i++)
            {
                var index = i;
                var slot = Hud.Box("match-unit-tile"); slot.AddToClassList("match-queue__slot");
                var icon = new HudIcon(HudIconKind.Worker); icon.AddToClassList("match-unit-tile__icon");
                slot.Add(icon);
                slot.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0 && queue != null && index < queue.Queue.Count) { host.CancelTraining(index); evt.StopPropagation(); } });
                queueRow.Add(slot); queueTiles.Add(slot);
            }
            selectionBody.Add(queueRow);
            selection.Add(selectionBody);
            selectionEmpty = Hud.Text("", "match-selection__empty");
            selection.Add(selectionEmpty);
            // Several units: one tile per unit with its health, as in StarCraft; pages of three rows when they do not fit.
            unitArea = Hud.Box("match-unit-area");
            unitPages = Hud.Box("match-unit-pages");
            unitGrid = Hud.Box("match-unit-grid");
            unitArea.Add(unitPages); unitArea.Add(unitGrid);
            selection.Add(unitArea);
            // Control groups as tabs on the top edge of the panel.
            groupTabs = Hud.Box("match-groups"); groupTabs.pickingMode = PickingMode.Ignore;
            for (var i = 0; i < UnitSelection.Groups; i++)
            {
                var index = i;
                var tab = Hud.Box("match-group");
                tab.Add(Hud.Text(i == 9 ? "0" : (i + 1).ToString(), "match-group__key"));
                tab.Add(Hud.Text("", "match-group__count"));
                tab.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0) { host.RecallGroup(index); evt.StopPropagation(); } });
                groupTabs.Add(tab); tabs.Add(tab);
            }
            selection.Add(groupTabs);
            deck.Add(selection);

            var commands = Hud.Box("match-commands"); commands.AddToClassList("match-hit");
            commands.Add(new HudFrame(12, 12));
            commandsTitle = Hud.Text("КОМАНДЫ", "match-commands__title");
            commands.Add(commandsTitle);
            var grid = Hud.Box("match-commands__grid");
            for (var i = 0; i < Slots; i++)
            {
                var slot = i;
                buttons[i] = HudCommandCard.Create(HudIconKind.Move, "", () => Run(slot));
                buttons[i].RegisterCallback<PointerEnterEvent>(_ => { hovered = slot; ShowTooltip(); });
                buttons[i].RegisterCallback<PointerLeaveEvent>(_ => { if (hovered == slot) { hovered = -1; ShowTooltip(); } });
                grid.Add(buttons[i]);
            }
            commands.Add(grid);
            tooltip = Hud.Box("match-tooltip"); tooltip.pickingMode = PickingMode.Ignore;
            tooltip.Add(new HudFrame(8, 8));
            var tooltipHead = Hud.Box("match-tooltip__head");
            tooltipTitle = Hud.Text("", "match-tooltip__title");
            tooltipKey = Hud.Keycap("");
            tooltipHead.Add(tooltipTitle); tooltipHead.Add(tooltipKey);
            tooltip.Add(tooltipHead);
            tooltipCost = Hud.Text("", "match-tooltip__cost");
            tooltipText = Hud.Text("", "match-tooltip__text");
            tooltipLock = Hud.Text("", "match-tooltip__lock");
            tooltip.Add(tooltipCost); tooltip.Add(tooltipText); tooltip.Add(tooltipLock);
            commands.Add(tooltip);
            deck.Add(commands);
            layout.Add(deck);

            // Typed orders to the assistant (Enter), above every window.
            commandLine = Hud.Box("match-command-line"); commandLine.AddToClassList("match-hit");
            commandLine.Add(new HudFrame(10, 10));
            commandLine.Add(Hud.Text("›", "console__prompt"));
            commandField = new TextField { name = "match-command" };
            commandField.AddToClassList("hud-input");
            commandField.textEdition.placeholder = services == null ? "Только приказы полигона: «иди в D-2-9», «стоп», «помощь»" : "Приказ ассистенту… «помощь» — список";
            commandField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                evt.StopPropagation();
                if (Time.frameCount != openedFrame) Submit();
            }, TrickleDown.TrickleDown);
            commandField.RegisterCallback<FocusInEvent>(_ => lineFocused = true);
            commandField.RegisterCallback<FocusOutEvent>(_ => lineFocused = false);
            commandLine.Add(commandField);
            commandLine.Add(Hud.Button("ОТПРАВИТЬ", Submit));
            screen.Add(commandLine);

            selectionBox = Hud.Box("match-selection-box"); selectionBox.pickingMode = PickingMode.Ignore;
            screen.Add(selectionBox);
            // Outcome of the match across the middle of the screen.
            banner = Hud.Text("", "match-banner"); banner.pickingMode = PickingMode.Ignore; banner.style.display = DisplayStyle.None;
            screen.Add(banner);

            if (services?.App != null)
            {
                options = new OptionsWindow(command => Send(command));
                options.CloseRequested += () => Send(AppCommand.Back());
                if (services.Phrasebook != null)
                {
                    phrasebookPanel = new PhrasebookPanel(services.Phrasebook, () => services.App.State);
                    options.Extra = tab => tab == AppScreen.VoiceSettings ? phrasebookPanel.Build() : null;
                }
                screen.Add(options.Layer);
                exitDialog = new HudModal("exit-dialog");
                var window = exitDialog.Window;
                window.AddToClassList("exit-dialog");
                window.Add(Hud.Text("ЗАВЕРШИТЬ СЕАНС?", "hud-modal__title"));
                window.Add(Hud.Text("Игра будет закрыта. Можно ответить ассистенту: «да» или «отмена».", "exit-dialog__text"));
                var buttonsRow = Hud.Box("exit-dialog__buttons");
                buttonsRow.Add(Hud.Button("ОТМЕНА", () => Send(AppCommand.CancelExit())));
                buttonsRow.Add(Hud.Button("ВЫЙТИ", () => Send(AppCommand.ConfirmExit()), "hud-button--danger"));
                window.Add(buttonsRow);
                screen.Add(exitDialog.Layer);
                services.App.Changed += OnAppChanged;
            }

            sound = new HudSound(screen);
            ApplyOptions();
            screen.schedule.Execute(Tick).Every(0);
            Refresh();
            Say("Полигон Temple Basin. Рабочий и отряд на позиции, жду приказов.");
        }

        // Team colour of own models: the accent of the HUD theme.
        public Color OwnColor => theme == "command" ? new Color(.25f, .83f, .78f) : theme == "resonance" ? new Color(.66f, .85f, 1f) : new Color(.94f, .71f, .24f);

        // True while the keyboard belongs to the HUD (typing, open window), so the camera ignores WASD.
        public bool KeyboardCaptured => lineFocused || commandLine.resolvedStyle.display == DisplayStyle.Flex || (options?.IsOpen ?? false) || (exitDialog?.IsOpen ?? false);

        public bool IsOverHud(Vector2 screenPosition)
        {
            if (screen.panel == null) return false;
            var point = RuntimePanelUtils.ScreenToPanel(screen.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            for (var element = screen.panel.Pick(point); element != null; element = element.parent)
                if (element.ClassListContains("match-hit") || element.ClassListContains("hud-modal")) return true;
            return false;
        }

        // Drag rectangle in screen pixels (origin bottom left); null hides it.
        public void SetSelectionBox(Rect? box)
        {
            selectionBox.style.display = box.HasValue && screen.panel != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (!box.HasValue || screen.panel == null) return;
            var r = box.Value;
            var a = RuntimePanelUtils.ScreenToPanel(screen.panel, new Vector2(r.xMin, Screen.height - r.yMax));
            var b = RuntimePanelUtils.ScreenToPanel(screen.panel, new Vector2(r.xMax, Screen.height - r.yMin));
            selectionBox.style.left = a.x; selectionBox.style.top = a.y;
            selectionBox.style.width = b.x - a.x; selectionBox.style.height = b.y - a.y;
        }

        public void HandleKeys(Keyboard keys)
        {
            if (keys == null || disposed) return;
            if (keys.escapeKey.wasPressedThisFrame && !HudKeyBind.Capturing && HudKeyBind.ConsumedFrame != Time.frameCount) Escape();
            if (keys.f10Key.wasPressedThisFrame) OpenMenu();
            var lineOpen = commandLine.resolvedStyle.display == DisplayStyle.Flex;
            if ((keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame) && !lineOpen && closedFrame != Time.frameCount && !(options?.IsOpen ?? false)) OpenLine();
            if (KeyboardCaptured) return;
#if UNITY_EDITOR
            if (keys.f9Key.wasPressedThisFrame) cheats.Toggle();
#endif
            if (keys.spaceKey.wasPressedThisFrame) host.FocusSelection();
            for (var i = 0; i < Slots; i++)
            {
                var action = actions[i];
                if (action?.Key == null || action.Locked != null) continue;
                if (keys[action.Key.Value].wasPressedThisFrame) { action.Run(); Refresh(); break; }
            }
        }

        public void Refresh()
        {
            if (disposed || host.Match == null) return;
            var match = host.Match;
            TrackIncome(match);
            metal.text = match.Metal.ToString();
            plasma.text = match.Plasma.ToString();
            var used = match.SupplyUsed; var limit = match.SupplyLimit;
            supply.text = $"{used} / {limit}";
            supply.EnableInClassList("is-full", used >= limit);
            // The map name is the title above; this line says who plays whom: "ФРАКЦИЯ 2 ПРОТИВ 1".
            modeLabel.text = PrototypeCatalog.FactionLabel(match.FactionOf(PrototypeMatch.Player)).ToUpperInvariant() + " ПРОТИВ " + (match.FactionOf(PrototypeMatch.Enemy) == FactionId.Expedition ? "1" : "2") + CheatLabel(match);
            modeLabel.EnableInClassList("is-cheating", match.Cheated);
            banner.style.display = match.Winner < 0 ? DisplayStyle.None : DisplayStyle.Flex;
            banner.text = match.Winner == PrototypeMatch.Player ? "ПОБЕДА" : "ПОРАЖЕНИЕ";
            banner.EnableInClassList("is-defeat", match.Winner == PrototypeMatch.Enemy);
            if (match.Message != lastMessage) { lastMessage = match.Message; ShowEvent(match.Message); }
            var hint = host.ModeHint;
            if (hint != lastHint) { lastHint = hint; if (!string.IsNullOrEmpty(hint)) ShowEvent(hint, true); }
            RefreshSelection();
            RefreshGroups(match);
            RefreshCommands();
#if UNITY_EDITOR
            cheats.Refresh();
#endif
            minimap.Refresh();
            assistantName.text = "АССИСТЕНТ  ·  " + PrototypeCatalog.FactionLabel(services?.App?.Settings.AssistantFaction ?? FactionId.Expedition).ToUpperInvariant();
            core.Faction = services?.App?.Settings.AssistantFaction ?? FactionId.Expedition;
        }

        private Label Resource(VisualElement parent, HudIconKind icon, string caption, out Label rate)
        {
            var item = Hud.Box("match-resource");
            var glyph = new HudIcon(icon); glyph.AddToClassList("match-resource__icon");
            item.Add(glyph);
            var texts = Hud.Box("match-resource__texts");
            texts.Add(Hud.Text(caption, "match-resource__caption"));
            var row = Hud.Box("match-resource__row");
            var value = Hud.Text("0", "match-resource__value");
            rate = Hud.Text("", "match-resource__rate");
            row.Add(value); row.Add(rate);
            texts.Add(row);
            item.Add(texts);
            parent.Add(item);
            return value;
        }

        private void TrackIncome(PrototypeMatch match)
        {
            var now = Time.realtimeSinceStartup;
            // Only what workers deliver counts: refunds and cheats are not income.
            gainedMetal = match.MetalHarvested; gainedPlasma = match.PlasmaHarvested;
            income.Enqueue((now, gainedMetal, gainedPlasma));
            while (income.Count > 1 && now - income.Peek().time > 60) income.Dequeue();
            var oldest = income.Peek();
            var span = now - oldest.time;
            string Rate(int gained) => span < 5 || gained == 0 ? "" : "+" + Mathf.RoundToInt(gained / span * 60) + "/мин";
            metalRate.text = Rate(gainedMetal - oldest.metal);
            plasmaRate.text = Rate(gainedPlasma - oldest.plasma);
        }

        private void RefreshSelection()
        {
            var match = host.Match;
            var balance = match.Balance;
            upgrades.Clear();
            shields.style.display = DisplayStyle.None;
            progress.style.display = DisplayStyle.None;
            queueRow.style.display = DisplayStyle.None; queue = null;
            var any = true;
            var selected = host.Selection.Units;
            var unit = selected.Count > 0 ? selected[0] : null;
            var many = selected.Count > 1;
            if (many)
            {
                selectionIcon.Kind = UnitIcon(unit.Kind);
                selectionName.text = "ОТРЯД · " + selected.Count;
                var parts = new List<string>();
                foreach (var group in selected.GroupBy(u => u.Kind)) parts.Add($"{group.First().Stats.Name} ×{group.Count()}".ToUpperInvariant());
                selectionStatus.text = string.Join("  ·  ", parts);
                RefreshTiles(selected);
            }
            else if (unit != null)
            {
                var stats = unit.Stats;
                var enemy = unit.Owner != PrototypeMatch.Player;
                selectionIcon.Kind = UnitIcon(unit.Kind);
                selectionName.text = stats.Name.ToUpperInvariant();
                selectionStatus.text = (enemy ? "ПРОТИВНИК · " : "") + State(unit, match);
                health.Set(unit.Health, stats.Health);
                // A machine worker's nanite cocoon shows in the shield bar while it is wrapped round a deposit.
                var nanites = unit.Kind == UnitKind.Worker && match.Roster(unit.Owner).NaniteMining;
                if (nanites && stats.Cocoon > 0 && unit.Wrap > 0)
                {
                    shields.style.display = DisplayStyle.Flex; shields.Q<Label>(className: "hud-bar__caption").text = "КОКОН";
                    shields.Set(unit.Cocoon, stats.Cocoon);
                }
                var place = MapGrid.Name(unit.X, unit.Z, TempleMap.Size);
                var targets = stats.HitsGround && stats.HitsAir ? "наземные и воздушные" : stats.HitsAir ? "только воздушные" : "только наземные";
                var role = stats.Damage <= 0 ? nanites ? $"Обвивает залежь: {stats.HarvestAmount} каждые {stats.HarvestInterval:0.#} с, кокон {stats.Cocoon}" : $"Груз {stats.HarvestAmount} за рейс, {stats.HarvestInterval:0.#} с у залежи"
                    : $"Урон {stats.Damage}  ·  {(stats.AttackRange < 1 ? "Ближний бой" : $"Дальность {stats.AttackRange:0.#}")}  ·  Перезарядка {stats.AttackCooldown:0.##} с  ·  Цели: {targets}";
                selectionStats.text = $"{role}  ·  Скорость {stats.Speed:0.#}  ·  Обзор {stats.Vision}{(stats.Flying ? "  ·  Воздушный" : "")}  ·  {place}";
                Upgrade(HudIconKind.Armor, "БРОНЯ", 0);
                Upgrade(HudIconKind.Target, "ОРУЖИЕ", stats.Damage > 0 ? 0 : -1);
            }
            else if (host.SelectedBuilding != null)
            {
                var building = host.SelectedBuilding;
                var stats = building.Stats;
                selectionIcon.Kind = building.Kind == 0 ? HudIconKind.Supply : building.Kind == 1 ? HudIconKind.Factory : HudIconKind.Headquarters;
                selectionName.text = stats.Name.ToUpperInvariant();
                var ready = building.Ready >= 1;
                var training = ready && building.Queue.Count > 0 ? match.Roster(building.Owner).Unit(building.Queue[0]) : null;
                // A building rises only while its worker is at it; without one the construction waits.
                var builder = building.Builder;
                var rising = building.SelfBuilt || (builder != null && PrototypeMatch.Gap(builder, building) <= PrototypeMatch.BuildReach);
                var own = building.Owner == PrototypeMatch.Player;
                selectionStatus.text = (own ? "" : "ПРОТИВНИК · ") + (!ready ? rising ? "СТРОИТЕЛЬСТВО" : builder != null ? "РАБОЧИЙ ИДЁТ К СТРОЙКЕ" : "СТРОЙКА ОСТАНОВЛЕНА"
                    : training == null ? "ГОТОВО" : building.SupplyBlocked ? "ЖДЁТ СНАБЖЕНИЯ" : "ОБУЧЕНИЕ · " + training.Name.ToUpperInvariant());
                health.Set(building.Health, stats.Health);
                var progressCaption = progress.Q<Label>(className: "hud-bar__caption");
                if (!ready)
                {
                    progress.style.display = DisplayStyle.Flex; progressCaption.text = "СТРОИТЕЛЬСТВО";
                    progress.Set(building.Ready, 1, $"{building.Ready * 100:0}%  ·  " + (rising ? $"{(1 - building.Ready) * stats.BuildSeconds:0.0} с"
                        : own ? "нужен рабочий: ПКМ рабочим по зданию" : "стоит"));
                }
                else if (training != null)
                {
                    progress.style.display = DisplayStyle.Flex; progressCaption.text = "ОБУЧЕНИЕ";
                    progress.Set(building.Training, training.TrainSeconds, building.SupplyBlocked ? "нужно снабжение"
                        : $"{building.Training / training.TrainSeconds * 100:0}%  ·  {training.TrainSeconds - building.Training:0.0} с");
                }
                if (building.Owner == PrototypeMatch.Player && stats.Trains.Length > 0) { queue = building; RefreshQueue(building); }
                selectionStats.text = $"Снабжение +{stats.Supply}  ·  Обзор {stats.Vision}  ·  След {stats.Width}×{stats.Width}  ·  {MapGrid.Name(building.X + stats.Width / 2f, building.Z + stats.Width / 2f, TempleMap.Size)}";
                Upgrade(HudIconKind.Armor, "БРОНЯ", 0);
            }
            else if (host.SelectedDeposit != null)
            {
                var deposit = host.SelectedDeposit;
                selectionIcon.Kind = deposit.Plasma ? HudIconKind.Plasma : HudIconKind.Metal;
                selectionName.text = deposit.Plasma ? "ПЛАЗМЕННЫЙ РАЗЛОМ" : "МЕТАЛЛИЧЕСКАЯ ЖИЛА";
                var visible = match.Visible[deposit.X, deposit.Z];
                selectionStatus.text = visible ? "ЗАЛЕЖЬ · ВИДНА" : "ЗАЛЕЖЬ · ПОСЛЕДНЕЕ НАБЛЮДЕНИЕ";
                var total = deposit.Plasma ? balance.PlasmaDeposit : balance.MetalDeposit;
                health.Set(deposit.Remaining, total, $"{deposit.Remaining} / {total}");
                var own = match.Roster(PrototypeMatch.Player);
                var yield = own.NaniteMining ? $"{own.Worker.Name} добывает {own.Worker.HarvestAmount} каждые {own.Worker.HarvestInterval:0.#} с" : $"{own.Worker.Name} выносит {own.Worker.HarvestAmount} за рейс";
                selectionStats.text = $"{yield}  ·  {MapGrid.Name(deposit.X + .5f, deposit.Z + .5f, TempleMap.Size)}";
            }
            else any = false;
            health.Q<Label>(className: "hud-bar__caption").text = host.SelectedDeposit != null && unit == null && host.SelectedBuilding == null ? "ЗАПАС" : "ПРОЧНОСТЬ";
            selectionBody.style.display = any && !many ? DisplayStyle.Flex : DisplayStyle.None;
            upgrades.style.display = any && !many ? DisplayStyle.Flex : DisplayStyle.None;
            unitArea.style.display = many ? DisplayStyle.Flex : DisplayStyle.None;
            selectionEmpty.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;
            if (!any)
            {
                selectionIcon.Kind = HudIconKind.Target;
                selectionName.text = "НЕТ ВЫДЕЛЕНИЯ";
                selectionStatus.text = "ЛКМ — выбрать  ·  ПКМ — приказ";
                selectionEmpty.text = "Выберите юнит, здание или залежь. Пробел — камера к выделенным.";
            }
        }

        // Active cheats after the mode, so a test run never passes for a fair one.
        private static string CheatLabel(PrototypeMatch match)
        {
            if (!match.Cheated) return "";
            var on = new List<string>();
            if (match.FastBuild) on.Add("×" + PrototypeMatch.FastFactor.ToString("0"));
            if (match.NoSupply) on.Add("БЕЗ ЛИМИТА");
            if (match.Revealed) on.Add("КАРТА");
            if (match.Invulnerable) on.Add("БЕССМЕРТИЕ");
            if (!match.BotEnabled) on.Add("БОТ ВЫКЛ");
            return "  ·  ЧИТЫ" + (on.Count > 0 ? ": " + string.Join(", ", on) : "");
        }

        private void RefreshQueue(PrototypeBuilding building)
        {
            queueRow.style.display = DisplayStyle.Flex;
            for (var i = 0; i < queueTiles.Count; i++)
            {
                var slot = queueTiles[i];
                var filled = i < building.Queue.Count;
                slot.EnableInClassList("is-empty", !filled);
                slot.EnableInClassList("is-current", filled && i == 0);
                var icon = slot.Q<HudIcon>();
                icon.style.visibility = filled ? Visibility.Visible : Visibility.Hidden;
                if (filled) icon.Kind = UnitIcon(building.Queue[i]);
                slot.tooltip = filled ? host.Match.Roster(building.Owner).Unit(building.Queue[i]).Name + " · клик — отмена" : "";
            }
        }

        // Hotkeys of the training buttons: the first letter of the unit's English name, as on StarCraft cards.
        private static Key TrainKey(UnitKind kind) => kind == UnitKind.Worker ? Key.E : kind == UnitKind.Melee ? Key.A : kind == UnitKind.Ranged ? Key.R : Key.G;

        private static string State(PrototypeUnit unit, PrototypeMatch match)
        {
            var deposit = unit.HarvestTarget;
            var site = unit.Constructing;
            if (site != null)
            {
                var name = site.Stats.Name.ToUpperInvariant();
                return unit.Moving ? "ИДЁТ К СТРОЙКЕ · " + name : $"СТРОИТ · {name} · {site.Ready * 100:0}%";
            }
            if (unit.SiteKind >= 0) return (unit.Moving ? "ИДЁТ СТРОИТЬ · " : "ЖДЁТ МЕСТА ДЛЯ СТРОЙКИ · ") + match.Roster(unit.Owner).Buildings[unit.SiteKind].Name.ToUpperInvariant();
            if (unit.Target != null || unit.TargetBuilding != null) return unit.Moving ? "ПРЕСЛЕДУЕТ ЦЕЛЬ" : "АТАКУЕТ";
            if (unit.Order == UnitOrder.Hold) return "ДЕРЖИТ ПОЗИЦИЮ";
            if (unit.Order == UnitOrder.AttackMove) return "АТАКА С ДВИЖЕНИЕМ";
            if (deposit != null && unit.Carrying > 0) return "НЕСЁТ " + (unit.CarriesPlasma ? "ПЛАЗМУ" : "МЕТАЛЛ");
            if (unit.Moving) return deposit != null ? "НАПРАВЛЯЕТСЯ К ЗАЛЕЖИ" : "ДВИЖЕНИЕ";
            if (deposit != null && unit.Wrap > 0 && unit.Wrap < 1) return $"ОБВИВАЕТ ЗАЛЕЖЬ · {unit.Wrap * 100:0}%";
            if (deposit != null && !unit.Mining && deposit.Remaining > 0) return "ЖДЁТ ОЧЕРЕДИ У ЗАЛЕЖИ";
            return deposit != null && deposit.Remaining > 0 ? "ДОБЫЧА · " + (deposit.Plasma ? "ПЛАЗМА" : "МЕТАЛЛ") : "ОЖИДАЕТ ПРИКАЗА";
        }

        // Tiles are reused; each keeps its unit in userData. Click: only this unit; Shift: drop it; Ctrl: keep only its kind.
        // A selection that does not fit is split into pages, switched with the numbers left of the tiles; a new selection
        // starts on the first page.
        private void RefreshTiles(IReadOnlyList<PrototypeUnit> selected)
        {
            // Rows: what the panel has room for below the header. The page numbers take a strip on the left only when there is
            // more than one page.
            var room = selectionPanel.resolvedStyle.height - selectionPanel.resolvedStyle.paddingBottom - unitArea.layout.y;
            var rows = float.IsNaN(room) || room < TileStepY ? TileRows : Mathf.Clamp((int)((room + 4) / TileStepY), 1, TileRows);
            var width = unitArea.resolvedStyle.width;
            int Fit(float w) => float.IsNaN(w) || w < TileStep ? 12 : Mathf.Max(1, (int)((w + 4) / TileStep));
            var columns = Fit(width);
            if (selected.Count > columns * rows) columns = Fit(width - PageStrip);
            var pageSize = columns * rows;
            var pages = (selected.Count + pageSize - 1) / pageSize;
            if (selected.Count == 0 || selected[0] != pagedFirst) { unitPage = 0; pagedFirst = selected.Count > 0 ? selected[0] : null; }
            unitPage = Mathf.Clamp(unitPage, 0, Mathf.Max(0, pages - 1));
            while (pageButtons.Count < pages)
            {
                var index = pageButtons.Count;
                var button = Hud.Box("match-unit-page");
                button.Add(Hud.Text((index + 1).ToString(), "match-unit-page__number"));
                button.RegisterCallback<PointerDownEvent>(evt => { if (evt.button != 0) return; unitPage = index; evt.StopPropagation(); Refresh(); });
                unitPages.Add(button); pageButtons.Add(button);
            }
            unitPages.style.display = pages > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            for (var i = 0; i < pageButtons.Count; i++)
            {
                pageButtons[i].style.display = i < pages ? DisplayStyle.Flex : DisplayStyle.None;
                pageButtons[i].EnableInClassList("is-active", i == unitPage);
            }
            var first = unitPage * pageSize;
            selected = selected.Skip(first).Take(pageSize).ToList();
            while (unitTiles.Count < selected.Count)
            {
                var tile = Hud.Box("match-unit-tile");
                var icon = new HudIcon(HudIconKind.Worker); icon.AddToClassList("match-unit-tile__icon");
                tile.Add(icon);
                var bar = Hud.Box("match-unit-tile__health"); bar.Add(Hud.Box("match-unit-tile__fill"));
                tile.Add(bar);
                tile.RegisterCallback<PointerDownEvent>(OnTile);
                unitGrid.Add(tile); unitTiles.Add(tile);
            }
            for (var i = 0; i < unitTiles.Count; i++)
            {
                var tile = unitTiles[i];
                tile.style.display = i < selected.Count ? DisplayStyle.Flex : DisplayStyle.None;
                if (i >= selected.Count) { tile.userData = null; continue; }
                var unit = selected[i];
                var share = Mathf.Clamp01(unit.Health / (float)unit.Stats.Health);
                tile.userData = unit;
                tile.tooltip = unit.Stats.Name;
                tile.Q<HudIcon>().Kind = UnitIcon(unit.Kind);
                tile.Q(className: "match-unit-tile__fill").style.width = Length.Percent(share * 100);
                tile.EnableInClassList("is-hurt", share < .6f);
                tile.EnableInClassList("is-critical", share < .3f);
            }
        }

        private void OnTile(PointerDownEvent evt)
        {
            if (evt.button != 0 || !((evt.currentTarget as VisualElement)?.userData is PrototypeUnit unit)) return;
            var selection = host.Selection;
            if (evt.shiftKey) selection.Remove(unit);
            else if (evt.ctrlKey) selection.Set(selection.Units.Where(u => u.Kind == unit.Kind).ToList());
            else selection.Set(new[] { unit });
            evt.StopPropagation();
            Refresh();
        }

        private void RefreshGroups(PrototypeMatch match)
        {
            for (var i = 0; i < tabs.Count; i++)
            {
                var size = host.Selection.GroupSize(i, match.Units);
                tabs[i].style.display = size > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                tabs[i].Q<Label>(className: "match-group__count").text = size.ToString();
            }
        }

        private static HudIconKind UnitIcon(UnitKind kind) =>
            kind == UnitKind.Melee ? HudIconKind.Assault : kind == UnitKind.Ranged ? HudIconKind.Rifleman : kind == UnitKind.Air ? HudIconKind.Gunship : HudIconKind.Worker;

        // Upgrade slot with level pips; -1 means the entity has no such stat.
        private void Upgrade(HudIconKind icon, string caption, int level)
        {
            var slot = Hud.Box("match-upgrade");
            slot.tooltip = caption;
            var glyph = new HudIcon(icon); glyph.AddToClassList("match-upgrade__icon");
            slot.Add(glyph);
            slot.Add(Hud.Text(level < 0 ? "—" : level.ToString(), "match-upgrade__level"));
            var pips = Hud.Box("match-upgrade__pips");
            for (var i = 0; i < 3; i++) { var pip = Hud.Box("match-upgrade__pip"); pip.EnableInClassList("is-on", i < level); pips.Add(pip); }
            slot.Add(pips);
            slot.EnableInClassList("is-none", level < 0);
            upgrades.Add(slot);
        }

        private sealed class HudAction
        {
            public string Title, Description, Hotkey, Locked;
            public HudIconKind Icon;
            public Key? Key;
            public int Metal, Plasma;
            public float Seconds;
            public bool Active;
            public Action Run;
        }

        private void RefreshCommands()
        {
            Array.Clear(actions, 0, actions.Length);
            var match = host.Match;
            var title = "КОМАНДЫ";
            var unit = host.Selection.Count > 0 ? host.Selection.Units[0] : null;
            // An enemy can be looked at, not ordered.
            if (unit != null && !host.Selection.Own) { title = "ПРОТИВНИК · " + unit.Stats.Name.ToUpperInvariant(); unit = null; }
            var worker = unit != null && unit.Stats.HarvestAmount > 0;
            if (worker)
            {
                // Placement shows the building list; a build order returns the card to the main page.
                if (host.Mode == MatchMode.Build) buildPage = true;
                if (builtCount >= 0 && host.BuildOrders != builtCount) buildPage = false;
                builtCount = host.BuildOrders;
                title = (buildPage ? "СТРОИТЕЛЬСТВО · " : "КОМАНДЫ · ") + Who().ToUpperInvariant();
            }
            else buildPage = false;
            if (unit != null && !buildPage)
            {
                if (!worker) title = "КОМАНДЫ · " + Who().ToUpperInvariant();
                actions[0] = new HudAction { Title = "Движение", Hotkey = "M", Key = Key.M, Icon = HudIconKind.Move, Active = host.Mode == MatchMode.Move,
                    Description = "Укажите точку на карте или миникарте. Также: ПКМ по карте.", Run = () => host.SetMode(MatchMode.Move) };
                actions[1] = new HudAction { Title = "Стоп", Hotkey = "X", Key = Key.X, Icon = HudIconKind.Stop,
                    Description = worker ? "Прервать движение, добычу и стройку: недостроенное здание будет ждать рабочего." : "Прервать приказ. Остановившись, юниты сами отвечают врагам рядом.", Run = host.Stop };
                if (worker)
                {
                    actions[2] = new HudAction { Title = "Добыча", Hotkey = "G", Key = Key.G, Icon = HudIconKind.Harvest, Active = host.Mode == MatchMode.Harvest,
                        Description = "Укажите разведанную жилу металла или разлом плазмы.", Run = () => host.SetMode(MatchMode.Harvest) };
                    actions[5] = new HudAction { Title = "Строительство", Hotkey = "B", Key = Key.B, Icon = HudIconKind.Build,
                        Description = "Открыть список построек. Рабочий идёт к месту и строит сам; ушёл — стройка ждёт. ПКМ рабочим по недостроенному зданию — продолжить.",
                        Run = () => buildPage = true };
                }
                else
                {
                    actions[2] = new HudAction { Title = "Атака", Hotkey = "A", Key = Key.A, Icon = HudIconKind.Target, Active = host.Mode == MatchMode.Attack,
                        Description = "ЛКМ по врагу — атаковать его, по земле — идти к точке и бить всех по пути. Также: ПКМ по врагу.",
                        Run = () => host.SetMode(MatchMode.Attack) };
                    actions[3] = new HudAction { Title = "Держать позицию", Hotkey = "H", Key = Key.H, Icon = HudIconKind.Hold,
                        Description = "Стоять на месте и стрелять только по целям в радиусе атаки. Свои юниты их не сдвигают.", Run = host.HoldSelection };
                }
            }
            else if (worker)
            {
                for (var kind = 0; kind < 2; kind++)
                {
                    var building = kind;
                    var stats = match.Roster(PrototypeMatch.Player).Buildings[kind];
                    actions[kind] = new HudAction { Title = stats.Name, Hotkey = kind == 0 ? "S" : "F", Key = kind == 0 ? UnityEngine.InputSystem.Key.S : UnityEngine.InputSystem.Key.F,
                        Icon = kind == 0 ? HudIconKind.Supply : HudIconKind.Factory, Metal = stats.Metal, Plasma = stats.Plasma, Seconds = stats.BuildSeconds,
                        Active = host.Mode == MatchMode.Build && host.BuildKind == kind,
                        Description = kind == 0 ? $"Снабжение +{stats.Supply}. След {stats.Width}×{stats.Width}." : $"Производственное здание. След {stats.Width}×{stats.Width}.",
                        Run = () => host.SetMode(MatchMode.Build, building) };
                }
            }
            else if (host.SelectedBuilding != null)
            {
                var building = host.SelectedBuilding;
                var stats = building.Stats;
                title = (building.Owner == PrototypeMatch.Player ? "КОМАНДЫ · " : "ПРОТИВНИК · ") + stats.Name.ToUpperInvariant();
                if (building.Owner == PrototypeMatch.Player)
                {
                    for (var i = 0; i < stats.Trains.Length && i < 4; i++)
                    {
                        var kind = stats.Trains[i];
                        var unitStats = match.Roster(building.Owner).Unit(kind);
                        var key = TrainKey(kind);
                        actions[i] = new HudAction { Title = unitStats.Name, Hotkey = key.ToString(), Key = key, Icon = UnitIcon(kind),
                            Metal = unitStats.Metal, Plasma = unitStats.Plasma, Seconds = unitStats.TrainSeconds,
                            Description = $"Снабжение {unitStats.Supply}. Очередь до {PrototypeMatch.QueueSize}; клик по юниту в очереди — отмена с возвратом стоимости.",
                            Locked = building.Ready < 1 ? "Здание ещё строится." : null, Run = () => host.Train(kind) };
                    }
                    if (stats.Trains.Length > 0)
                        actions[4] = new HudAction { Title = "Точка сбора", Hotkey = "Y", Key = Key.Y, Icon = HudIconKind.Pin, Active = host.Mode == MatchMode.Rally,
                            Description = "Куда идут новые юниты. Рабочие с точкой на залежи сразу добывают. Также: ПКМ по карте.", Run = () => host.SetMode(MatchMode.Rally) };
                }
            }
            if (host.Mode != MatchMode.None)
                actions[14] = new HudAction { Title = "Отмена", Hotkey = "ESC", Icon = HudIconKind.Close, Description = "Выйти из режима указания цели.", Run = () => host.SetMode(MatchMode.None) };
            else if (buildPage)
                actions[14] = new HudAction { Title = "Назад", Hotkey = "ESC", Icon = HudIconKind.Back, Description = "Вернуться к приказам рабочего.", Run = () => buildPage = false };
            else if (CancellableSite() != null)
                actions[14] = new HudAction { Title = "Отменить стройку", Hotkey = "ESC", Icon = HudIconKind.Close,
                    Description = "Снести недостроенное здание: вернётся три четверти стоимости.", Run = () => host.CancelConstruction() };
            commandsTitle.text = title;
            for (var i = 0; i < Slots; i++)
            {
                var action = actions[i];
                var button = buttons[i];
                button.EnableInClassList("is-empty", action == null);
                button.EnableInClassList("is-active", action?.Active ?? false);
                button.EnableInClassList("is-locked", action?.Locked != null);
                button.EnableInClassList("is-short", action != null && (match.Metal < action.Metal || match.Plasma < action.Plasma));
                button.SetEnabled(action != null);
                if (action == null) continue;
                button.Q<HudIcon>(className: "hud-command__icon").Kind = action.Icon;
                var key = button.Q<Label>(className: "hud-command__key");
                if (key != null) key.text = action.Hotkey;
            }
            ShowTooltip();
        }

        private void Run(int slot)
        {
            var action = actions[slot];
            if (action == null) return;
            if (action.Locked != null) { ShowEvent(action.Locked, true); sound.Play(HudCue.Deny); return; }
            action.Run();
            Refresh();
        }

        private void ShowTooltip()
        {
            var action = hovered >= 0 ? actions[hovered] : null;
            tooltip.EnableInClassList("is-visible", action != null);
            if (action == null) return;
            tooltipTitle.text = action.Title.ToUpperInvariant();
            tooltipKey.text = action.Hotkey;
            var cost = new List<string>();
            if (action.Metal > 0) cost.Add($"Металл {action.Metal}");
            if (action.Plasma > 0) cost.Add($"Плазма {action.Plasma}");
            if (action.Seconds > 0) cost.Add($"{action.Seconds:0.#} с");
            tooltipCost.text = string.Join("  ·  ", cost);
            tooltipCost.style.display = cost.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            tooltipCost.EnableInClassList("is-short", host.Match.Metal < action.Metal || host.Match.Plasma < action.Plasma);
            tooltipText.text = action.Description;
            tooltipLock.text = action.Locked ?? "";
            tooltipLock.style.display = action.Locked != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ShowEvent(string text, bool hint = false)
        {
            if (string.IsNullOrEmpty(text) || (quiet && !hint)) return;
            eventLine.text = text;
            eventLine.EnableInClassList("is-hint", hint);
            eventLine.AddToClassList("is-visible");
            eventUntil = Time.realtimeSinceStartup + (hint ? 6 : 3.5f);
        }

        private void Tick()
        {
            if (disposed) return;
            var now = Time.realtimeSinceStartup;
            var elapsed = (int)(now - started);
            timer.text = $"{elapsed / 60:00}:{elapsed % 60:00}";
            if (now > eventUntil && host.Mode == MatchMode.None) eventLine.RemoveFromClassList("is-visible");
            if (shown < speech.Length)
            {
                shown = Mathf.Min(speech.Length, Mathf.FloorToInt((now - speechStart) * 52) + 1);
                subtitle.text = speech.Substring(0, shown) + (shown < speech.Length ? "_" : "");
                core.SpeechLevel = .35f + .65f * Mathf.PerlinNoise(now * 8, 0);
                if (shown == speech.Length) { speechDone = now; subtitleUntil = now + (speechAlert ? 6 : 4.5f); }
            }
            else if (!busy && (core.Mood == AssistantMood.Speaking || core.Mood == AssistantMood.Alert) && now - speechDone > (speechAlert ? 3 : 1.4f))
                SetMood(lineFocused ? AssistantMood.Attentive : AssistantMood.Idle);
            subtitle.EnableInClassList("is-visible", shown < speech.Length || now < subtitleUntil || busy);
            screen.EnableInClassList("hud-blink", Mathf.FloorToInt(now * 2) % 2 == 0);
        }

        private void Say(string text, bool alert = false)
        {
            speech = text ?? "";
            shown = 0;
            speechStart = Time.realtimeSinceStartup;
            speechAlert = alert;
            subtitle.EnableInClassList("is-alert", alert);
            SetMood(alert ? AssistantMood.Alert : AssistantMood.Speaking);
            sound?.Play(alert ? HudCue.Deny : HudCue.Reply);
        }

        private void SetMood(AssistantMood next)
        {
            core.Mood = next;
            switch (next)
            {
                case AssistantMood.Thinking: mood.Set("АНАЛИЗ ПРИКАЗА", "pending"); break;
                case AssistantMood.Speaking: mood.Set("ОТВЕТ", "ok"); break;
                case AssistantMood.Alert: mood.Set("УТОЧНЕНИЕ", "warn"); break;
                case AssistantMood.Attentive: mood.Set("СЛУШАЮ", "ok"); break;
                default: mood.Set("НА СВЯЗИ", null); break;
            }
        }

        private void OpenLine()
        {
            commandLine.style.display = DisplayStyle.Flex;
            commandLine.BringToFront();
            openedFrame = Time.frameCount;
            commandField.schedule.Execute(() => commandField.Focus());
            SetMood(AssistantMood.Attentive);
        }

        private void CloseLine()
        {
            commandField.SetValueWithoutNotify("");
            commandField.Blur();
            commandLine.style.display = DisplayStyle.None;
            closedFrame = Time.frameCount;
            if (core.Mood == AssistantMood.Attentive) SetMood(AssistantMood.Idle);
        }

        private void Escape()
        {
            if (commandLine.resolvedStyle.display == DisplayStyle.Flex) { CloseLine(); return; }
#if UNITY_EDITOR
            if (cheats.IsOpen) { cheats.Close(); return; }
#endif
            var app = services?.App;
            if (app != null && app.ExitPending) { Send(AppCommand.CancelExit()); return; }
            if (app != null && app.Screen.IsSettings()) { Send(AppCommand.Back()); return; }
            if (host.Mode != MatchMode.None) { host.SetMode(MatchMode.None); return; }
            if (buildPage) { buildPage = false; Refresh(); return; }
            // As in StarCraft, Esc cancels the selected building under construction.
            if (CancellableSite() != null) { host.CancelConstruction(); Refresh(); }
        }
        private PrototypeBuilding CancellableSite()
        {
            var b = host.SelectedBuilding;
            return host.Selection.Count == 0 && b != null && b.Owner == PrototypeMatch.Player && b.Ready < 1 ? b : null;
        }

        private void OpenMenu()
        {
            if (services?.App != null) Send(AppCommand.OpenSettings());
            else Say("Настройки доступны при запуске полигона из командного центра.", true);
        }

        private void Leave()
        {
            if (services?.App != null) Send(AppCommand.Navigate(AppScreen.MainMenu));
            else host.Exit();
        }

        private void Send(AppCommand command)
        {
            var result = services.Input.SubmitManual(command);
            if (!disposed && !result.Applied) Say(result.Message, true);
        }

        private async void Submit()
        {
            var text = (commandField.value ?? "").Trim();
            CloseLine();
            if (text.Length == 0 || busy || disposed) return;
            // The assistant voices the result of typed orders; the event line stays for mouse and world messages.
            quiet = true;
            var handled = TryMatchCommand(text, out var reply, out var ok);
            lastMessage = host.Match?.Message;
            quiet = false;
            if (handled) { Say(reply, !ok); Refresh(); return; }
            if (services?.Input == null) { Say("Вне командного центра доступны только приказы полигона. «Помощь» — список.", true); return; }
            busy = true;
            SetMood(AssistantMood.Thinking);
            speech = ""; shown = 0;
            subtitle.text = "Разбираю приказ…";
            subtitle.RemoveFromClassList("is-alert");
            try
            {
                var app = services.App;
                var result = await services.Input.SubmitTranscriptAsync(text, new CommandContext(app.Session, CommandSource.Text, app.Revision), lifetime.Token);
                if (lifetime.IsCancellationRequested) return;
                busy = false;
                Say(result.Message, !result.Applied && result.Outcome != CommandOutcome.Answered);
            }
            finally { busy = false; }
        }

        // Orders for the test map, mirroring the command card: deterministic, no model involved.
        private bool TryMatchCommand(string text, out string reply, out bool ok)
        {
            ok = true; reply = null;
            var match = host.Match;
            var t = Regex.Replace(text.ToLowerInvariant().Replace('ё', 'е'), @"[.!?,;:«»""]+", " ").Trim();
            bool Is(string pattern) => Regex.IsMatch(t, pattern, RegexOptions.CultureInvariant);
            if (Is(@"^(?:помощь|справка|команды|что ты умеешь)$"))
            {
                reply = "Приказы полигона: «выбери армию», «выбери стрелков», «группа 1», «иди в D-2-9», «атакуй в F-6», «держи позицию», «обучи 2 стрелков», «к метке 2», «стоп», «добывай плазму», «построй узел снабжения в C-7», «метка в E-4», «камера на H-8», «сетка», «к базе». " +
                    (services == null ? "" : "Меню: «открой графику», «громкость 40», «в штаб».");
                return true;
            }
            var hasPoint = MapGrid.TryFind(t, TempleMap.Size, out var x, out var z, out var place);
            var pinMatch = Regex.Match(t, @"\bметк\w*\s*(?:номер\s*)?(\d)\b", RegexOptions.CultureInvariant);
            if (!hasPoint && pinMatch.Success)
            {
                var index = pinMatch.Groups[1].Value[0] - '1';
                if (index < 0 || index >= host.Pins.Count) { ok = false; reply = $"Метки {pinMatch.Groups[1].Value} нет. Сейчас меток: {host.Pins.Count}."; return true; }
                x = host.Pins[index].x; z = host.Pins[index].y; place = "метке " + (index + 1); hasPoint = true;
            }
            var point = new Vector2(x, z);
            if (Is(@"^(?:стоп|стой|остановись|отставить|прекрати)$"))
            {
                ok = host.Selection.Own; host.Stop();
                reply = ok ? Who() + ": стоп." : "Нет выделенных юнитов.";
                return true;
            }
            if (Is(@"^(?:отмена|отмени)$") && host.Mode != MatchMode.None) { host.SetMode(MatchMode.None); reply = "Режим отменён."; return true; }
            if (Is(@"^(?:к рабочему|покажи рабочего|камера к рабочему|где рабочий)$")) { host.FocusWorker(); reply = "Камера на рабочем."; return true; }
            if (Is(@"^(?:к базе|на базу|домой|покажи базу|камера к базе)$")) { host.FocusBase(); reply = "Камера на командном узле."; return true; }
            if (Is(@"^(?:выбери рабочего|рабочий)$")) { host.SelectWorker(); reply = "Рабочий выбран."; return true; }
            if (Is(@"^(?:(?:выбери|выдели) )?(?:всю )?(?:армию|войска|все войска)$"))
            {
                host.SelectArmy(); ok = host.Selection.Count > 0;
                reply = ok ? $"Армия выбрана: {host.Selection.Count}." : "Армии нет."; return true;
            }
            var kindMatch = Regex.Match(t, @"^(?:выбери|выдели) (?:всех )?(штурмовик\w*|стрел\w*|винтокрыл\w*|резак\w*|излучател\w*|планер\w*)$", RegexOptions.CultureInvariant);
            if (kindMatch.Success)
            {
                var word = kindMatch.Groups[1].Value;
                var kind = word.StartsWith("штурм") || word.StartsWith("резак") ? UnitKind.Melee : word.StartsWith("стрел") || word.StartsWith("излучател") ? UnitKind.Ranged : UnitKind.Air;
                host.SelectKind(kind); ok = host.Selection.Count > 0 && host.Selection.Units[0].Kind == kind;
                reply = ok ? $"{Who()}: выбрано {host.Selection.Count}." : "Таких юнитов нет."; return true;
            }
            var groupMatch = Regex.Match(t, @"^группа (\d)$", RegexOptions.CultureInvariant);
            if (groupMatch.Success)
            {
                var digit = groupMatch.Groups[1].Value[0] - '0'; var index = digit == 0 ? 9 : digit - 1;
                ok = host.Selection.GroupSize(index, match.Units) > 0;
                if (ok) host.RecallGroup(index);
                reply = ok ? $"Группа {digit}: {host.Selection.Count}." : $"Группа {digit} пуста. Назначить: Ctrl+{digit}."; return true;
            }
            if (Is(@"^(?:строительство|постройки|меню построек|строить)$")) { host.SelectWorker(); buildPage = true; reply = "Выберите здание: S — узел снабжения, F — цех."; return true; }
            if (Is(@"^(?:очисти|убери|сними|удали) метки$")) { host.ClearPins(); reply = "Метки сняты."; return true; }
            if (Is(@"^(?:сетка|покажи сетку|скрой сетку|координаты|сетка координат)$")) { minimap.SetGrid(!minimap.GridVisible); reply = minimap.GridVisible ? "Сетка координат включена." : "Сетка координат скрыта."; return true; }
            if (services == null && Is(@"^(?:в штаб|в меню|выйти в меню|выход)$")) { host.Exit(); return true; }
            if (Is(@"^(?:метка|поставь метку|отметь|пин)\b"))
            {
                if (!hasPoint) { host.SetMode(MatchMode.Ping); reply = "Укажите место метки на карте или миникарте."; return true; }
                host.AddPin(point); reply = $"Метка {host.Pins.Count}: {place}."; return true;
            }
            if (Is(@"^(?:камера|покажи|смотри|взгляни)\b") && hasPoint) { host.Focus(point); reply = "Камера: " + place + "."; return true; }
            var build = Is(@"\b(?:построй|строй|возведи|поставь)\b") ? Is(@"\bцех\w*") ? 1 : Is(@"\b(?:узел\w*|снабжени\w*|склад\w*)") ? 0 : -1 : -1;
            if (build >= 0)
            {
                var stats = match.Roster(PrototypeMatch.Player).Buildings[build];
                if (!hasPoint) { host.SetMode(MatchMode.Build, build); reply = $"{stats.Name}: укажите место. Esc — отмена."; return true; }
                var ox = Mathf.RoundToInt(x - stats.Width / 2f); var oz = Mathf.RoundToInt(z - stats.Width / 2f);
                ok = host.Build(ox, oz, build);
                reply = ok ? $"{stats.Name}: рабочий идёт строить в {place}." : $"Не могу строить в {place}: {match.Message}";
                return true;
            }
            if (Is(@"^(?:добывай|добыча|собирай|копай|на добычу|иди добывать)\b"))
            {
                var wantPlasma = Is(@"\bплазм\w*");
                var harvester = host.Selection.Orderable.FirstOrDefault(u => u.Stats.HarvestAmount > 0);
                if (harvester == null) { host.SelectWorker(); harvester = host.Selection.Units.FirstOrDefault(); }
                if (harvester == null) { ok = false; reply = "Нет рабочих."; return true; }
                var deposit = hasPoint ? match.Map.ResourceAt((int)x, (int)z) : match.Map.Resources
                    .Where(r => r.Plasma == wantPlasma && r.Remaining > 0 && match.Explored[r.X, r.Z])
                    .OrderBy(r => (r.X - harvester.X) * (r.X - harvester.X) + (r.Z - harvester.Z) * (r.Z - harvester.Z)).FirstOrDefault();
                if (deposit == null) { ok = false; reply = wantPlasma ? "Нет разведанной плазмы." : "Нет разведанного металла."; return true; }
                ok = match.Move(harvester, deposit.X + .5f, deposit.Z + .5f);
                reply = ok ? $"{harvester.Stats.Name}: добыча {(deposit.Plasma ? "плазмы" : "металла")} в {MapGrid.Name(deposit.X + .5f, deposit.Z + .5f, TempleMap.Size)}." : match.Message;
                return true;
            }
            var trainMatch = Regex.Match(t, @"^(?:обучи|найми|закажи|произведи)\s+(?:(\d)\s+)?(рабоч|ткач|штурмов|резак|стрел|излучател|винтокрыл|планер)\w*(?:\s+(\d))?$", RegexOptions.CultureInvariant);
            if (trainMatch.Success)
            {
                var word = trainMatch.Groups[2].Value;
                var kind = word == "рабоч" || word == "ткач" ? UnitKind.Worker : word == "штурмов" || word == "резак" ? UnitKind.Melee : word == "стрел" || word == "излучател" ? UnitKind.Ranged : UnitKind.Air;
                var digits = trainMatch.Groups[1].Success ? trainMatch.Groups[1].Value : trainMatch.Groups[3].Value;
                var count = Mathf.Clamp(digits.Length > 0 ? digits[0] - '0' : 1, 1, PrototypeMatch.QueueSize);
                // The selected building if it trains this unit, otherwise the least busy one that does.
                var selected = host.SelectedBuilding;
                var building = selected != null && selected.Owner == PrototypeMatch.Player && selected.Ready >= 1 && match.Trains(selected, kind) ? selected
                    : match.Buildings.Where(b => b.Owner == PrototypeMatch.Player && b.Ready >= 1 && match.Trains(b, kind)).OrderBy(b => b.Queue.Count).FirstOrDefault();
                var name = match.Roster(PrototypeMatch.Player).Unit(kind).Name;
                if (building == null) { ok = false; reply = kind == UnitKind.Worker ? "Нет готового командного узла." : $"{name}: нужен готовый цех."; return true; }
                var queued = 0;
                while (queued < count && match.Train(building, kind)) queued++;
                ok = queued > 0;
                reply = ok ? $"Обучение: {name}{(queued > 1 ? " ×" + queued : "")} — {building.Stats.Name.ToLowerInvariant()}." + (queued < count ? " Остальные: " + match.Message : "") : match.Message;
                return true;
            }
            if (Is(@"^(?:держи\w* позици\w*|держать позици\w*|стой на месте|стоять на месте)$"))
            {
                ok = host.Selection.Own && host.Selection.Units.Any(u => u.Stats.Damage > 0);
                if (ok) host.HoldSelection();
                reply = ok ? Who() + ": держим позицию." : "Нет выделенных боевых юнитов.";
                return true;
            }
            if (Is(@"^(?:атакуй|атака|в атаку|атаковать)\b"))
            {
                ok = host.Selection.Own && host.Selection.Units.Any(u => u.Stats.Damage > 0);
                if (!ok) { reply = "Нет выделенных боевых юнитов. «Выбери армию» — взять всех."; return true; }
                if (!hasPoint) { host.SetMode(MatchMode.Attack); reply = "Укажите врага или точку: ЛКМ по карте или миникарте."; return true; }
                ok = host.AttackMoveSelection(point);
                reply = ok ? Who() + ": атака с движением к " + place + "." : match.Message;
                return true;
            }
            if (Is(@"^(?:иди|идти|двигайся|движение|перемести\w*|отправь рабочего|рабочий|вперед)\b"))
            {
                if (Is(@"\bрабоч\w*") || host.Selection.Count == 0) host.SelectWorker();
                if (!hasPoint) { host.SetMode(MatchMode.Move); reply = "Укажите точку: ЛКМ по карте или миникарте."; return true; }
                ok = host.MoveSelection(point);
                reply = ok ? Who() + ": движение к " + place + "." : match.Message;
                return true;
            }
            return false;
        }

        private string Who() => host.Selection.Count == 1 ? host.Selection.Units[0].Stats.Name : "Отряд";

        private void OnAppChanged()
        {
            if (disposed) return;
            var app = services.App;
            if (app.Screen.IsSettings()) options.Show(app.Screen, app.Settings);
            else if (options.IsOpen) options.Close();
            options.Sync(app.Settings);
            if (app.ExitPending && !exitDialog.IsOpen) exitDialog.Open();
            else if (!app.ExitPending && exitDialog.IsOpen) exitDialog.Close();
            screen.EnableInClassList("is-modal", options.IsOpen || exitDialog.IsOpen);
            if (commandLine.resolvedStyle.display == DisplayStyle.Flex) commandLine.BringToFront();
            ApplyOptions();
            Refresh();
        }

        // Theme follows the player's faction unless the interface option asks for the command theme.
        private void ApplyOptions()
        {
            var settings = services?.App?.Settings;
            float Option(string id, float fallback) => settings.HasValue ? MenuCatalog.Value(settings.Value, MenuCatalog.Find(id)) : fallback;
            var next = Option("ui.hudTheme", 0) >= .5f ? "command" : host.Match != null && host.Match.FactionOf(PrototypeMatch.Player) == FactionId.Resonance ? "resonance" : "expedition";
            if (next != theme)
            {
                if (theme != null) screen.RemoveFromClassList("hud-theme-" + theme);
                theme = next;
                screen.AddToClassList("hud-theme-" + theme);
                cursor?.Dispose();
                cursor = theme == "command"
                    ? new HudCursor(screen, new Color(.25f, .83f, .78f), new Color(.73f, 1f, .97f), new Color(.016f, .035f, .047f), new Color(1f, .54f, .24f))
                    : theme == "resonance"
                    ? new HudCursor(screen, new Color(.66f, .85f, 1f), Color.white, new Color(.024f, .035f, .047f), new Color(1f, .7f, .28f))
                    : new HudCursor(screen, new Color(.94f, .71f, .24f), new Color(1f, .95f, .8f), new Color(.03f, .035f, .043f), new Color(1f, .42f, .24f));
            }
            cursor.Enabled = Option(MenuCatalog.TacticalCursor, 1) >= .5f;
            sound.Volume = Option(MenuCatalog.InterfaceVolume, 60) / 100f;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            if (services?.App != null) services.App.Changed -= OnAppChanged;
            phrasebookPanel?.Dispose();
            cursor?.Dispose();
            sound.Dispose();
            root.Clear();
            if (style != null) root.styleSheets.Remove(style);
        }

        // Circular frame around the assistant portrait.
        private sealed class PortraitRing : HudVector
        {
            public PortraitRing() { AddToClassList("match-portrait__frame"); }

            protected override void Draw(MeshGenerationContext context)
            {
                var r = contentRect;
                var radius = Mathf.Min(r.width, r.height) * .5f - 2;
                if (radius < 10) return;
                var c = r.center;
                var p = context.painter2D;
                p.fillColor = HudPaint.Alpha(Background, .85f);
                p.BeginPath(); HudPaint.Circle(p, c, radius); p.Fill();
                p.lineWidth = 1.5f; p.strokeColor = Line; p.BeginPath(); HudPaint.Circle(p, c, radius); p.Stroke();
                p.lineWidth = 3; p.strokeColor = Accent; p.lineCap = LineCap.Butt; p.BeginPath();
                HudPaint.Arc(p, c, radius - 4, 200, 250); HudPaint.Arc(p, c, radius - 4, 290, 340); HudPaint.Arc(p, c, radius - 4, 20, 70); HudPaint.Arc(p, c, radius - 4, 110, 160);
                p.Stroke();
                p.lineWidth = 1; p.strokeColor = HudPaint.Alpha(Line, .9f); p.BeginPath();
                for (var i = 0; i < 48; i++) { var a = i * 7.5f; p.MoveTo(HudPaint.Polar(c, radius - 9, a)); p.LineTo(HudPaint.Polar(c, radius - (i % 6 == 0 ? 15 : 12), a)); }
                p.Stroke();
            }
        }
    }
}
