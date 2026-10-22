using System.Collections.Generic;
using Order.Content;
using Order.Simulation;

namespace Order.Presentation
{
    // The player's selection and control groups, as in StarCraft. Local to the player, not part of the simulation:
    // orders carry the selected units. Units are kept in display order: melee, ranged, air, then workers.
    public sealed class UnitSelection
    {
        public const int Groups = 10;
        readonly List<PrototypeUnit> units = new List<PrototypeUnit>();
        readonly List<int>[] groups = new List<int>[Groups];
        public IReadOnlyList<PrototypeUnit> Units => units;
        public int Count => units.Count;
        public bool Contains(PrototypeUnit unit) => units.Contains(unit);

        public UnitSelection() { for (int i = 0; i < Groups; i++) groups[i] = new List<int>(); }

        public void Clear() => units.Clear();
        public void Set(IEnumerable<PrototypeUnit> next) { units.Clear(); Add(next); }
        public void Add(IEnumerable<PrototypeUnit> more)
        {
            foreach (var unit in more) if (unit != null && !units.Contains(unit)) units.Add(unit);
            units.Sort((a, b) => Priority(a.Kind) != Priority(b.Kind) ? Priority(a.Kind).CompareTo(Priority(b.Kind)) : a.Id.CompareTo(b.Id));
        }
        public void Remove(PrototypeUnit unit) => units.Remove(unit);

        // Click: only this unit; Shift toggles it. Ctrl or a double click takes every unit of its kind among `visible`
        // (the units on screen); with Shift they are added.
        public void Click(PrototypeUnit unit, bool shift, bool sameKind, IEnumerable<PrototypeUnit> visible)
        {
            if (sameKind)
            {
                var same = new List<PrototypeUnit> { unit };
                foreach (var other in visible) if (other.Kind == unit.Kind) same.Add(other);
                if (shift) Add(same); else Set(same);
            }
            else if (shift) { if (units.Contains(unit)) units.Remove(unit); else Add(new[] { unit }); }
            else Set(new[] { unit });
        }
        // Box: the units inside replace the selection; with Shift they are added. An empty box without Shift clears it.
        public void Box(IEnumerable<PrototypeUnit> inside, bool shift)
        {
            if (shift) Add(inside); else Set(inside);
        }

        // Ctrl+number replaces a group, Shift+number adds to it, the number alone recalls it.
        public void Assign(int group) { groups[group].Clear(); foreach (var u in units) groups[group].Add(u.Id); }
        public void AddToGroup(int group) { foreach (var u in units) if (!groups[group].Contains(u.Id)) groups[group].Add(u.Id); }
        public bool Recall(int group, IReadOnlyList<PrototypeUnit> alive)
        {
            var members = Members(group, alive);
            if (members.Count == 0) return false;
            Set(members);
            return true;
        }
        public int GroupSize(int group, IReadOnlyList<PrototypeUnit> alive) => Members(group, alive).Count;
        List<PrototypeUnit> Members(int group, IReadOnlyList<PrototypeUnit> alive)
        {
            var members = new List<PrototypeUnit>();
            foreach (var u in alive) if (groups[group].Contains(u.Id)) members.Add(u);
            return members;
        }
        // Drops units that no longer exist.
        public void Prune(IReadOnlyList<PrototypeUnit> alive)
        {
            units.RemoveAll(u => !Contains(alive, u));
            foreach (var group in groups) group.RemoveAll(id => !Exists(alive, id));
        }
        static bool Contains(IReadOnlyList<PrototypeUnit> list, PrototypeUnit unit) { foreach (var u in list) if (u == unit) return true; return false; }
        static bool Exists(IReadOnlyList<PrototypeUnit> list, int id) { foreach (var u in list) if (u.Id == id) return true; return false; }
        public static int Priority(UnitKind kind) => kind == UnitKind.Worker ? 9 : (int)kind;
    }
}
