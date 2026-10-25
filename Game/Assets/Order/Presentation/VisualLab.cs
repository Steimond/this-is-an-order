using System.Collections.Generic;
using Order.Content;
using Order.Contracts;
using Order.Simulation;
using UnityEngine;

namespace Order.Presentation
{
    // A scene to tune the look of everything in the game (Assets/Order/Scenes/VisualLab.unity, Order → Create Visual Lab):
    // the match is laid out instead of played. The bot is off, the whole map is seen and the machines' network reaches
    // everywhere, so nothing moves, fights or goes dark by itself.
    // - In the basin, the machine buildings stand in rows, one row a kind, one column a stage of their rise: the worker's
    //   puddle, the filling, the draining and finished (Stages).
    // - On the north-east plateau, the humans' buildings, finished and half-built.
    // - On the plateaus, every unit of both sides.
    // The nanite liquid is tuned in its material, Resources/NaniteLiquid, live even in Play Mode, and it stays tuned after
    // Play Mode; the stages, time and camera are tuned here.
    [RequireComponent(typeof(TemplePrototypeView))]
    public sealed class VisualLab : MonoBehaviour
    {
        [Tooltip("Construction progress of each column of machine sites, 0 to 1: the worker's puddle, the filling, the draining, finished.")]
        public float[] Stages = { .04f, .12f, .3f, .55f, .85f, 1 };
        [Tooltip("Run every site through its whole rise again and again, the columns a little apart, instead of holding the stages.")]
        public bool Cycle;
        [Tooltip("Seconds for one rise when cycling.")]
        public float CycleSeconds = 16;
        [Tooltip("Game speed: the liquid's own motion follows it too.")]
        [Range(0, 3)] public float TimeScale = 1;
        public enum Shot { MachineSites, CloseUp, HumanBuildings, MachineUnits, HumanUnits }
        [Tooltip("Where the camera looks; pick one to move it there.")]
        public Shot Look = Shot.MachineSites;
        [Tooltip("For the close-up: the machine building's row (0 compute node, 1 assembler, 2 core, 3 relay, 4 firmware node) and its stage column.")]
        public int CloseRow = 2, CloseColumn = 3;

        const int MachineX = 21, MachineZ = 21, Step = 4;
        readonly List<(PrototypeBuilding site, int column)> sites = new List<(PrototypeBuilding, int)>();
        TemplePrototypeView view;
        Shot shown = (Shot)(-1);
        int shownRow = -1, shownColumn = -1;

        void Awake()
        {
            view = GetComponent<TemplePrototypeView>();
            view.Sides = (FactionId.Resonance, FactionId.Expedition);
            view.Setup = Arrange;
        }

        void Arrange(PrototypeMatch match)
        {
            sites.Clear();
            match.ToggleReveal(); match.ReachEverywhere();
            const int Machines = PrototypeMatch.Player, Humans = PrototypeMatch.Enemy;
            var machines = match.Roster(Machines);
            for (int kind = 0; kind < machines.Buildings.Length; kind++)
                for (int column = 0; column < Stages.Length; column++)
                    sites.Add((match.PlaceBuilding(kind, MachineX + column * Step, MachineZ + kind * Step, Machines, Stage(column)), column));
            // The humans' buildings on their plateau: a finished row and a half-built one, the extractor on its rift.
            var humans = match.Roster(Humans);
            for (int kind = 0, x = 45; kind < humans.Buildings.Length; kind++)
            {
                var stats = humans.Buildings[kind];
                if (stats == null) continue;
                if (stats.Extracts)
                {
                    var rift = match.Map.Resources.Find(r => r.Plasma && r.X > 44 && r.Z > 44);
                    if (rift != null) match.PlaceBuilding(kind, rift.X, rift.Z, Humans, 1);
                    continue;
                }
                match.PlaceBuilding(kind, x, 45, Humans, 1); match.PlaceBuilding(kind, x, 49, Humans, .5f);
                x += stats.Width + 1;
            }
            // Every unit of both sides, a row on each start plateau, far from each other.
            Line(match, Machines, 3.5f, 16.5f);
            Line(match, Humans, 45.5f, 55.5f);
        }
        static void Line(PrototypeMatch match, int owner, float x, float z)
        {
            var units = match.Roster(owner).Units;
            for (int kind = 0; kind < units.Length; kind++)
                if (units[kind] != null) { match.Spawn((UnitKind)kind, x, z, owner); x += 2.2f; }
        }
        float Stage(int column) => Stages.Length == 0 ? 1 : Mathf.Clamp01(Stages[Mathf.Min(column, Stages.Length - 1)]);

        // After the match's own tick, so the view draws each site at exactly its stage.
        void LateUpdate()
        {
            var match = view.Match;
            if (match == null) return;
            // Nothing selected: a selected worker would draw the coverage of every core in the lab.
            if (shown < 0) view.Selection.Clear();
            if (!Mathf.Approximately(Time.timeScale, TimeScale)) Time.timeScale = TimeScale;
            int columns = Mathf.Max(1, Stages.Length);
            foreach (var (site, column) in sites)
                site.Ready = Cycle ? Mathf.Repeat(Time.time / Mathf.Max(1, CycleSeconds) + column / (float)columns, 1) : Stage(column);
            if (Look != shown || (Look == Shot.CloseUp && (CloseRow != shownRow || CloseColumn != shownColumn))) Aim();
        }
        void Aim()
        {
            shown = Look; shownRow = CloseRow; shownColumn = CloseColumn;
            int rows = view.Match.Roster(PrototypeMatch.Player).Buildings.Length, columns = Mathf.Max(1, Stages.Length);
            switch (Look)
            {
                case Shot.MachineSites: view.LookAt(new Vector2(MachineX + columns * Step * .5f, MachineZ + rows * Step * .5f - 2), 22); break;
                case Shot.CloseUp:
                    int row = Mathf.Clamp(CloseRow, 0, rows - 1), column = Mathf.Clamp(CloseColumn, 0, columns - 1);
                    view.LookAt(new Vector2(MachineX + column * Step + 1.5f, MachineZ + row * Step + 1.5f), 6); break;
                case Shot.HumanBuildings: view.LookAt(new Vector2(52, 49), 14); break;
                case Shot.MachineUnits: view.LookAt(new Vector2(9, 16.5f), 8); break;
                case Shot.HumanUnits: view.LookAt(new Vector2(52, 55.5f), 9); break;
            }
        }
        void OnDisable() => Time.timeScale = 1;
    }
}
