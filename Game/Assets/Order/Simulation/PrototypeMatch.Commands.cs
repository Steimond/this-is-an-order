using System;
using System.Collections.Generic;
using Order.Content;

namespace Order.Simulation
{
    // What a side can order, as plain data: a kind, the issuing side, the units it orders by Id, and its target — a unit
    // or building Id, a point, or a number (a building or unit kind, a queue index, an upgrade, an amount).
    public enum CommandKind
    {
        Move, AttackMove, AttackUnit, AttackBuilding, QueueMove, QueueAttackMove, QueueAttackUnit, QueueAttackBuilding,
        Stop, Hold, Build, QueueBuild, Resume, CancelConstruction, Train, CancelTraining, SetRally, Research, CancelResearch,
        RepairBuilding, RepairUnit, QueueRepairBuilding, QueueRepairUnit, AutoRepair, ToggleDeploy, Disassemble,
        // Test-ground cheats are commands too: in a networked match they change the state on every machine.
        CheatResources, CheatFastBuild, CheatNoSupply, CheatReveal, CheatInvulnerable, CheatWin, CheatBot, CheatWave, CheatSpawn,
    }
    public readonly struct MatchCommand
    {
        public readonly CommandKind Kind;
        public readonly int Side;
        public readonly int[] Units;
        // A unit or building Id, or a number; Extra a second number (an amount, the side units are spawned for, on/off).
        public readonly int Target, Extra;
        public readonly Fix X, Z;
        public MatchCommand(CommandKind kind, int side, int[] units, int target, Fix x, Fix z, int extra = 0)
        { Kind = kind; Side = side; Units = units ?? Array.Empty<int>(); Target = target; X = x; Z = z; Extra = extra; }

        static int[] Ids(IReadOnlyList<PrototypeUnit> units) { var ids = new int[units.Count]; for (int i = 0; i < ids.Length; i++) ids[i] = units[i].Id; return ids; }
        static int[] Ids(PrototypeUnit unit) => new[] { unit.Id };
        public static MatchCommand Move(int side, IReadOnlyList<PrototypeUnit> units, Fix x, Fix z, bool queue = false) => new MatchCommand(queue ? CommandKind.QueueMove : CommandKind.Move, side, Ids(units), 0, x, z);
        public static MatchCommand AttackMove(int side, IReadOnlyList<PrototypeUnit> units, Fix x, Fix z, bool queue = false) => new MatchCommand(queue ? CommandKind.QueueAttackMove : CommandKind.AttackMove, side, Ids(units), 0, x, z);
        public static MatchCommand Attack(int side, IReadOnlyList<PrototypeUnit> units, PrototypeUnit target, bool queue = false) => new MatchCommand(queue ? CommandKind.QueueAttackUnit : CommandKind.AttackUnit, side, Ids(units), target.Id, 0, 0);
        public static MatchCommand Attack(int side, IReadOnlyList<PrototypeUnit> units, PrototypeBuilding target, bool queue = false) => new MatchCommand(queue ? CommandKind.QueueAttackBuilding : CommandKind.AttackBuilding, side, Ids(units), target.Id, 0, 0);
        public static MatchCommand Stop(int side, IReadOnlyList<PrototypeUnit> units) => new MatchCommand(CommandKind.Stop, side, Ids(units), 0, 0, 0);
        public static MatchCommand Hold(int side, IReadOnlyList<PrototypeUnit> units) => new MatchCommand(CommandKind.Hold, side, Ids(units), 0, 0, 0);
        public static MatchCommand Build(int side, PrototypeUnit worker, int x, int z, int kind, bool queue = false) => new MatchCommand(queue ? CommandKind.QueueBuild : CommandKind.Build, side, Ids(worker), kind, x, z);
        public static MatchCommand Resume(int side, PrototypeUnit worker, PrototypeBuilding site) => new MatchCommand(CommandKind.Resume, side, Ids(worker), site.Id, 0, 0);
        public static MatchCommand CancelConstruction(int side, PrototypeBuilding site) => new MatchCommand(CommandKind.CancelConstruction, side, null, site.Id, 0, 0);
        public static MatchCommand Train(int side, PrototypeBuilding building, UnitKind kind) => new MatchCommand(CommandKind.Train, side, null, building.Id, 0, 0, (int)kind);
        public static MatchCommand CancelTraining(int side, PrototypeBuilding building, int index) => new MatchCommand(CommandKind.CancelTraining, side, null, building.Id, 0, 0, index);
        public static MatchCommand SetRally(int side, PrototypeBuilding building, Fix x, Fix z) => new MatchCommand(CommandKind.SetRally, side, null, building.Id, x, z);
        public static MatchCommand Research(int side, PrototypeBuilding building, Upgrade upgrade) => new MatchCommand(CommandKind.Research, side, null, building.Id, 0, 0, (int)upgrade);
        public static MatchCommand CancelResearch(int side, PrototypeBuilding building) => new MatchCommand(CommandKind.CancelResearch, side, null, building.Id, 0, 0);
        public static MatchCommand Repair(int side, PrototypeUnit worker, PrototypeBuilding target, bool queue = false) => new MatchCommand(queue ? CommandKind.QueueRepairBuilding : CommandKind.RepairBuilding, side, Ids(worker), target.Id, 0, 0);
        public static MatchCommand Repair(int side, PrototypeUnit worker, PrototypeUnit target, bool queue = false) => new MatchCommand(queue ? CommandKind.QueueRepairUnit : CommandKind.RepairUnit, side, Ids(worker), target.Id, 0, 0);
        public static MatchCommand AutoRepair(int side, IReadOnlyList<PrototypeUnit> workers, bool on) => new MatchCommand(CommandKind.AutoRepair, side, Ids(workers), 0, 0, 0, on ? 1 : 0);
        public static MatchCommand ToggleDeploy(int side, PrototypeUnit unit) => new MatchCommand(CommandKind.ToggleDeploy, side, Ids(unit), 0, 0, 0);
        public static MatchCommand Disassemble(int side, PrototypeUnit unit) => new MatchCommand(CommandKind.Disassemble, side, Ids(unit), 0, 0, 0);
        public static MatchCommand Cheat(CommandKind kind, int side, int amount = 0) => new MatchCommand(kind, side, null, 0, 0, 0, amount);
        public static MatchCommand Spawn(int side, UnitKind kind, int count, Fix x, Fix z, int owner) => new MatchCommand(CommandKind.CheatSpawn, side, null, (int)kind, x, z, count * PrototypeMatch.MaxSides + owner);
    }
    // A command with the frame it was applied before: the log of a match, enough to play it again.
    public readonly struct LoggedCommand
    {
        public readonly int Frame;
        public readonly MatchCommand Command;
        public LoggedCommand(int frame, MatchCommand command) { Frame = frame; Command = command; }
    }

    // Orders from people (the view, the HUD, voice, later the network) reach the match only as commands, between its steps:
    // a command applied now and one applied at the start of the next step find the same state, as nothing moves in
    // between. Every command is logged with the frame it came before, so a fresh match of the same setup fed the log on
    // the same frames plays the same match (Replay). A networked match will hold commands for a later frame instead and
    // apply them at its start on every machine. The bots order from inside the step: every machine runs them alike.
    // A side orders only its own units and buildings; commands naming others, or things gone, do nothing.
    public sealed partial class PrototypeMatch
    {
        readonly List<LoggedCommand> log = new List<LoggedCommand>();
        public IReadOnlyList<LoggedCommand> Log => log;
        bool stepping;

        public bool Execute(MatchCommand c)
        {
            if (stepping) throw new InvalidOperationException("Commands are applied between steps, not inside one.");
            if (c.Side < 0 || c.Side >= Sides) return false;
            log.Add(new LoggedCommand(Frame, c));
            return Apply(c);
        }
        // Plays a log into this match, a fresh one of the same setup, for this many frames.
        public void Replay(IReadOnlyList<LoggedCommand> commands, int frames)
        {
            int next = 0;
            for (int i = 0; i < frames; i++)
            {
                while (next < commands.Count && commands[next].Frame == Frame) Execute(commands[next++].Command);
                Advance();
            }
        }

        bool Apply(MatchCommand c)
        {
            var units = Own(c);
            switch (c.Kind)
            {
                case CommandKind.Move: return units.Count > 0 && MoveGroup(units, c.X, c.Z);
                case CommandKind.AttackMove: return units.Count > 0 && AttackMoveGroup(units, c.X, c.Z);
                case CommandKind.QueueMove: return units.Count > 0 && QueueMoveGroup(units, c.X, c.Z);
                case CommandKind.QueueAttackMove: return units.Count > 0 && QueueAttackMoveGroup(units, c.X, c.Z);
                case CommandKind.AttackUnit: { var t = UnitById(c.Target); return t != null && units.Count > 0 && AttackGroup(units, t); }
                case CommandKind.AttackBuilding: { var t = BuildingById(c.Target); return t != null && units.Count > 0 && AttackGroup(units, t); }
                case CommandKind.QueueAttackUnit: { var t = UnitById(c.Target); return t != null && units.Count > 0 && EnqueueGroup(units, t); }
                case CommandKind.QueueAttackBuilding: { var t = BuildingById(c.Target); return t != null && units.Count > 0 && EnqueueGroup(units, t); }
                case CommandKind.Stop: foreach (var u in units) Stop(u); return units.Count > 0;
                case CommandKind.Hold: foreach (var u in units) Hold(u); return units.Count > 0;
                case CommandKind.Build: return units.Count == 1 && Build(units[0], (int)c.X, (int)c.Z, c.Target);
                case CommandKind.QueueBuild: return units.Count == 1 && QueueBuild(units[0], (int)c.X, (int)c.Z, c.Target);
                case CommandKind.Resume: { var b = OwnBuilding(c); return b != null && units.Count == 1 && Resume(units[0], b); }
                case CommandKind.CancelConstruction: { var b = OwnBuilding(c); return b != null && CancelConstruction(b); }
                case CommandKind.Train: { var b = OwnBuilding(c); return b != null && Enum.IsDefined(typeof(UnitKind), c.Extra) && Train(b, (UnitKind)c.Extra); }
                case CommandKind.CancelTraining: { var b = OwnBuilding(c); return b != null && CancelTraining(b, c.Extra); }
                case CommandKind.SetRally: { var b = OwnBuilding(c); if (b == null) return false; SetRally(b, c.X, c.Z); return true; }
                case CommandKind.Research: { var b = OwnBuilding(c); return b != null && Enum.IsDefined(typeof(Upgrade), c.Extra) && Research(b, (Upgrade)c.Extra); }
                case CommandKind.CancelResearch: { var b = OwnBuilding(c); return b != null && CancelResearch(b); }
                case CommandKind.RepairBuilding: { var t = BuildingById(c.Target); return t != null && units.Count == 1 && Repair(units[0], t); }
                case CommandKind.RepairUnit: { var t = UnitById(c.Target); return t != null && units.Count == 1 && Repair(units[0], t); }
                case CommandKind.QueueRepairBuilding: { var t = BuildingById(c.Target); return t != null && units.Count == 1 && QueueRepair(units[0], t); }
                case CommandKind.QueueRepairUnit: { var t = UnitById(c.Target); return t != null && units.Count == 1 && QueueRepair(units[0], t); }
                case CommandKind.AutoRepair: { bool any = false; foreach (var u in units) any |= SetAutoRepair(u, c.Extra != 0); return any; }
                case CommandKind.ToggleDeploy: return units.Count == 1 && ToggleDeploy(units[0]);
                case CommandKind.Disassemble: return units.Count == 1 && Disassemble(units[0]);
                case CommandKind.CheatResources: AddResources(c.Extra, c.Extra); return true;
                case CommandKind.CheatFastBuild: return ToggleFastBuild();
                case CommandKind.CheatNoSupply: return ToggleNoSupply();
                case CommandKind.CheatReveal: return ToggleReveal();
                case CommandKind.CheatInvulnerable: return ToggleInvulnerable();
                case CommandKind.CheatWin: WinNow(); return true;
                case CommandKind.CheatBot: return ToggleBot();
                case CommandKind.CheatWave: SendWaveNow(); return true;
                case CommandKind.CheatSpawn:
                {
                    int owner = c.Extra % MaxSides, count = c.Extra / MaxSides;
                    return owner < Sides && Enum.IsDefined(typeof(UnitKind), c.Target) && Roster(owner).Has((UnitKind)c.Target) && SpawnSquad((UnitKind)c.Target, count, c.X, c.Z, owner).Count > 0;
                }
            }
            return false;
        }
        // The command's units that are alive and the side's own, in the command's order.
        List<PrototypeUnit> Own(MatchCommand c)
        {
            var units = new List<PrototypeUnit>(c.Units.Length);
            if (c.Units.Length == 0) return units;
            var byId = new Dictionary<int, PrototypeUnit>(Units.Count);
            foreach (var u in Units) if (u.Owner == c.Side && u.Alive) byId[u.Id] = u;
            foreach (int id in c.Units) if (byId.TryGetValue(id, out var u) && !units.Contains(u)) units.Add(u);
            return units;
        }
        PrototypeBuilding OwnBuilding(MatchCommand c) { var b = BuildingById(c.Target); return b != null && b.Owner == c.Side ? b : null; }
        public PrototypeUnit UnitById(int id) => Units.Find(u => u.Id == id && u.Alive);
        public PrototypeBuilding BuildingById(int id) => Buildings.Find(b => b.Id == id && b.Health > 0);
    }
}
