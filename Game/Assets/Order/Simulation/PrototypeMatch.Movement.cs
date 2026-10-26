using System;
using System.Collections.Generic;
using Order.Content;

namespace Order.Simulation
{
    // Unit movement in the spirit of StarCraft:
    // - A* over map cells (8 directions) for the unit's radius, smoothed into straight segments; an unreachable point
    //   sends the unit as close as it can get;
    // - a group order keeps the formation when the point lies outside a compact group, otherwise everyone converges;
    // - the first unit stops on the point, the rest stop on touching an ally that already arrived at the same point;
    // - moving units push idle allies aside, units passing each other side-step, nobody walks into a wall or off a cliff;
    //   air units only keep apart from each other.
    public sealed partial class PrototypeMatch
    {
        static readonly Fix Step = (Fix).05f;
        static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 }, Dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
        // Bumps when buildings or deposits change what blocks movement; caches below depend on it.
        int navigationVersion;
        readonly bool[] blocked = new bool[4096];
        int blockedVersion = -1, blockedBuildings = -1;
        readonly Dictionary<int, sbyte[]> edges = new Dictionary<int, sbyte[]>();
        // Cached edges are forgotten only round the spot that changed (see NavigationChanged).
        readonly Fix[] pathCost = new Fix[4096];
        readonly int[] cameFrom = new int[4096], seen = new int[4096], done = new int[4096];
        int searchStamp;
        readonly CellHeap open = new CellHeap();
        readonly List<PrototypeUnit>[] buckets = new List<PrototypeUnit>[4096];
        readonly List<int> usedBuckets = new List<int>();

        public bool Occupied(int x, int z)
        {
            if (blockedVersion != navigationVersion || blockedBuildings != Buildings.Count)
            {
                Array.Clear(blocked, 0, blocked.Length);
                foreach (var r in Map.Resources) if (r.Remaining > 0) blocked[r.Z * 64 + r.X] = true;
                foreach (var b in Buildings) for (int zz = b.Z; zz < b.Z + b.Width; zz++) for (int xx = b.X; xx < b.X + b.Width; xx++) if (Map.Inside(xx, zz)) blocked[zz * 64 + xx] = true;
                blockedVersion = navigationVersion; blockedBuildings = Buildings.Count;
            }
            return blocked[z * 64 + x];
        }
        // A building placed or gone, or a deposit mined out: the blocked-cell cache is rebuilt and the cached edges round
        // the spot are forgotten; edges elsewhere stay valid, so the next long path does not pay for the whole map again.
        void NavigationChanged(int x, int z, int width)
        {
            navigationVersion++;
            foreach (var table in edges.Values)
                for (int cz = Math.Max(0, z - 2); cz <= Math.Min(63, z + width + 1); cz++)
                for (int cx = Math.Max(0, x - 2); cx <= Math.Min(63, x + width + 1); cx++)
                    for (int dir = 0; dir < 8; dir++) table[(cz * 64 + cx) * 8 + dir] = 0;
        }
        public bool CanStep(int x, int z, int nx, int nz) => Map.Inside(nx, nz) && !Occupied(nx, nz) && !RampSide(x, z, nx, nz) && Math.Abs(Map.Height(x, z) - Map.Height(nx, nz)) <= (Fix).51f;
        // Ramps are entered and left only at their ends: their sides are walls along the whole length, even where the drop is
        // shallow, so a crowd cannot spill over the edge near the bottom. A plain cell is beside a ramp cell (not at its end)
        // when its row is outside the ramp at that ramp cell's column.
        bool RampSide(int ax, int az, int bx, int bz)
        {
            if (az == bz || !Map.Inside(ax, az) || !Map.Inside(bx, bz)) return false;
            bool a = Map.Ramp(ax, az), b = Map.Ramp(bx, bz);
            if (a == b) return false;
            return a ? !Map.Ramp(ax, bz) : !Map.Ramp(bx, az);
        }
        // Continuous straight segment for a ground unit of the given radius: inside the map, clear of blocked cells and cliffs.
        public bool CanTravel(Fix ax, Fix az, Fix bx, Fix bz, Fix radius)
        {
            Fix dx = bx - ax, dz = bz - az; int count = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dz * dz) / (Fix).1));
            Fix previous = HeightAt(ax, az);
            for (int i = 0; i <= count; i++)
            {
                Fix x = ax + dx * i / count, z = az + dz * i / count;
                if (x < radius || z < radius || x >= 64 - radius || z >= 64 - radius) return false;
                Fix h = HeightAt(x, z); if (Math.Abs(h - previous) > (Fix).51f) return false; previous = h;
                int cx = (int)x, cz = (int)z;
                if (i > 0 && RampSide((int)(ax + dx * (i - 1) / count), (int)(az + dz * (i - 1) / count), cx, cz)) return false;
                for (int zz = (int)(z - radius); zz <= (int)(z + radius); zz++) for (int xx = (int)(x - radius); xx <= (int)(x + radius); xx++)
                {
                    Fix nearX = Math.Max(xx, Math.Min(x, xx + 1)), nearZ = Math.Max(zz, Math.Min(z, zz + 1));
                    if ((x - nearX) * (x - nearX) + (z - nearZ) * (z - nearZ) >= radius * radius) continue;
                    if (Occupied(xx, zz) || RampSide(cx, cz, xx, zz) || Math.Abs(Map.Height(xx, zz, Math.Max(0, Math.Min(1, x - xx))) - h) > (Fix).51f) return false;
                }
            }
            return true;
        }

        // ---------- Orders ----------
        public bool Move(PrototypeUnit unit, int tx, int tz) => Move(unit, tx + (Fix).5f, tz + (Fix).5f);
        // A worker sent to a deposit harvests it after arrival; other units only walk up to it. A moving unit ignores enemies.
        public bool Move(PrototypeUnit unit, Fix targetX, Fix targetZ)
        {
            if (!InMap(targetX, targetZ)) return false;
            GiveOrder(unit, UnitOrder.Move);
            return PlanPath(unit, targetX, targetZ);
        }
        // A NaN point from the view arrives below the map (Fix converts NaN to its least value).
        static bool InMap(Fix x, Fix z) => x >= 0 && z >= 0 && x < 64 && z < 64;
        const string Unreachable = "Точка недоступна: двигаюсь как можно ближе.";
        // Route to a point without touching the order: used by orders, chasing, re-planning and returning to place.
        bool PlanPath(PrototypeUnit unit, Fix targetX, Fix targetZ)
        {
            if (!InMap(targetX, targetZ)) return false;
            int tx = (int)targetX, tz = (int)targetZ;
            unit.HarvestTarget = null; unit.arrived = false; unit.stuckTime = 0; unit.bestDistance = Fix.MaxValue; unit.replans = 0;
            var deposit = unit.Owner != Player || Explored[tx, tz] ? Map.ResourceAt(tx, tz) : null;
            Fix radius = unit.Stats.Radius;
            // A point inside a building means the free ground next to it on the unit's side: searching for the building's own
            // cell would explore the whole map before settling for the nearest reachable one.
            bool beside = false;
            if (!unit.Stats.Flying && deposit == null && Occupied(tx, tz) && NearestFree(unit, tx, tz, out Fix fx, out Fix fz))
            {
                targetX = fx; targetZ = fz; tx = (int)fx; tz = (int)fz; beside = true;
            }
            if (unit.Stats.Flying)
            {
                // Air units fly straight over cliffs, buildings and deposits.
                SetRoute(unit, new List<Waypoint> { new Waypoint(Math.Max(radius, Math.Min(64 - radius, targetX)), Math.Max(radius, Math.Min(64 - radius, targetZ))) });
                Message = "Приказ: движение."; return true;
            }
            if (deposit == null && CanTravel(unit.X, unit.Z, targetX, targetZ, radius))
            {
                SetRoute(unit, new List<Waypoint> { new Waypoint(targetX, targetZ) });
                Message = beside ? Unreachable : "Приказ: движение."; return true;
            }
            var cells = FindPath(Cell(unit.X, unit.Z), tz * 64 + tx, deposit, targetX, targetZ, radius, out bool reached);
            var points = new List<Waypoint> { new Waypoint(unit.X, unit.Z) };
            foreach (var c in cells) points.Add(new Waypoint(c % 64 + (Fix).5f, c / 64 + (Fix).5f));
            if (deposit == null && reached && CanTravel(points[points.Count - 1].X, points[points.Count - 1].Z, targetX, targetZ, radius)) points.Add(new Waypoint(targetX, targetZ));
            // Already in a cell next to the deposit, yet maybe pushed to its far side: the unit steps to its middle, within
            // reach of the deposit, instead of standing out of reach with nowhere to go.
            if (deposit != null && reached && cells.Count == 0) { int c = Cell(unit.X, unit.Z); points.Add(new Waypoint(c % 64 + (Fix).5f, c / 64 + (Fix).5f)); }
            // Pull the string: from each point jump to the farthest point still reachable in a straight line. It is found by
            // doubling the step while the line stays clear and then halving between the last clear and the first blocked
            // point, so a long path costs a few line checks per corner instead of one per cell.
            var route = new List<Waypoint>(); int anchor = 0;
            while (anchor < points.Count - 1)
            {
                var a = points[anchor];
                bool Clear(int k) => CanTravel(a.X, a.Z, points[k].X, points[k].Z, radius);
                int clear = anchor + 1, blocked = -1, step = 1;
                while (clear < points.Count - 1)
                {
                    int k = Math.Min(points.Count - 1, clear + step);
                    if (Clear(k)) { clear = k; step *= 2; } else { blocked = k; break; }
                }
                while (blocked > clear + 1) { int mid = (clear + blocked) / 2; if (Clear(mid)) clear = mid; else blocked = mid; }
                route.Add(points[clear]); anchor = clear;
            }
            SetRoute(unit, route);
            bool harvest = deposit != null && reached && unit.Stats.HarvestAmount > 0;
            // A rift without the side's extractor is only walked up to.
            var closed = harvest ? MiningBlocked(unit.Owner, deposit) : null;
            if (closed != null) harvest = false;
            unit.HarvestTarget = harvest ? deposit : null;
            Message = harvest ? "Приказ: добыча ресурса." : closed ?? (reached && !beside ? "Приказ: движение." : Unreachable);
            return true;
        }
        // The free cell nearest a blocked one; of the equally near, the one on the side of the unit or group.
        bool NearestFree(PrototypeUnit unit, int x, int z, out Fix fx, out Fix fz) => NearestFree(unit.Stats.Radius, unit.X, unit.Z, x, z, out fx, out fz);
        bool NearestFree(Fix radius, Fix fromX, Fix fromZ, int x, int z, out Fix fx, out Fix fz)
        {
            fx = fz = 0; Fix best = Fix.MaxValue;
            for (int ring = 1; ring <= 6 && best == Fix.MaxValue; ring++)
                for (int cz = z - ring; cz <= z + ring; cz++)
                for (int cx = x - ring; cx <= x + ring; cx++)
                {
                    if (Math.Max(Math.Abs(cx - x), Math.Abs(cz - z)) != ring || !Map.Inside(cx, cz) || Occupied(cx, cz) || Map.Ramp(cx, cz)) continue;
                    Fix px = cx + (Fix).5f, pz = cz + (Fix).5f;
                    if (!CanTravel(px, pz, px, pz, radius)) continue;
                    Fix score = (px - x - (Fix).5f) * (px - x - (Fix).5f) + (pz - z - (Fix).5f) * (pz - z - (Fix).5f) + ((px - fromX) * (px - fromX) + (pz - fromZ) * (pz - fromZ)) * (Fix).001f;
                    if (score < best) { best = score; fx = px; fz = pz; }
                }
            return best < Fix.MaxValue;
        }
        void SetRoute(PrototypeUnit unit, List<Waypoint> route)
        {
            unit.route.Clear();
            foreach (var p in route) if (p.X != unit.X || p.Z != unit.Z || route.Count == 1) unit.route.Enqueue(p);
            var end = unit.route.Count > 0 ? route[route.Count - 1] : new Waypoint(unit.X, unit.Z);
            unit.goalX = end.X; unit.goalZ = end.Z; unit.hasGoal = unit.route.Count > 0;
            if (!unit.hasGoal) Arrive(unit);
        }
        public void Stop(PrototypeUnit unit)
        {
            GiveOrder(unit, UnitOrder.Idle);
            unit.route.Clear(); unit.HarvestTarget = null; unit.hasGoal = false; unit.arrived = false; Message = "Приказ: стоп.";
        }

        // Several units to one point. When a compact group is sent beyond its own bounds, every unit keeps its place
        // in the formation (if that spot is free and on the clicked level); a click inside the group gathers it on the point.
        public bool MoveGroup(IReadOnlyList<PrototypeUnit> units, Fix x, Fix z) => GroupOrder(units, x, z, false, false);
        bool GroupOrder(IReadOnlyList<PrototypeUnit> units, Fix x, Fix z, bool attack, bool queue)
        {
            if (units.Count == 0) return false;
            if (units.Count == 1 && !queue) return attack ? AttackMove(units[0], x, z) : Move(units[0], x, z);
            int cx = (int)x, cz = (int)z;
            bool deposit = Map.Inside(cx, cz) && Explored[cx, cz] && Map.ResourceAt(cx, cz) != null;
            Fix mx = 0, mz = 0, minX = 64, minZ = 64, maxX = 0, maxZ = 0, spread = 0;
            foreach (var u in units) { mx += u.X; mz += u.Z; minX = Math.Min(minX, u.X); maxX = Math.Max(maxX, u.X); minZ = Math.Min(minZ, u.Z); maxZ = Math.Max(maxZ, u.Z); }
            mx /= units.Count; mz /= units.Count;
            // A click on a building sends the group to the free ground next to it on the group's side.
            if (!deposit && Map.Inside(cx, cz) && Occupied(cx, cz) && NearestFree((Fix).45f, mx, mz, cx, cz, out Fix fx, out Fix fz)) { x = fx; z = fz; cx = (int)x; cz = (int)z; }
            foreach (var u in units) spread = Math.Max(spread, Math.Sqrt((u.X - mx) * (u.X - mx) + (u.Z - mz) * (u.Z - mz)));
            bool formation = !deposit && spread <= 5 && (x < minX - (Fix).5f || x > maxX + (Fix).5f || z < minZ - (Fix).5f || z > maxZ + (Fix).5f);
            Fix level = Map.Inside(cx, cz) ? HeightAt(x, z) : 0;
            bool any = false; var message = Message;
            (Fix, Fix) Spot(PrototypeUnit u)
            {
                Fix gx = x + u.X - mx, gz = z + u.Z - mz;
                return formation && FreeSpot(u, gx, gz, level) ? (gx, gz) : (x, z);
            }
            // One path search serves the group: the largest ground unit nearest the middle finds the way, and the other
            // ground units near it on its level follow that way's corners when they can reach the first one and their own
            // spot from the last one in a straight line; otherwise they search for themselves.
            PrototypeUnit leader = null; List<Waypoint> corners = null;
            if (!queue && !deposit)
            {
                Fix best = Fix.MaxValue;
                foreach (var u in units)
                {
                    if (u.Stats.Flying) continue;
                    Fix score = -u.Stats.Radius * 1000 + (u.X - mx) * (u.X - mx) + (u.Z - mz) * (u.Z - mz);
                    if (score < best) { best = score; leader = u; }
                }
                if (leader != null)
                {
                    var (lx, lz) = Spot(leader);
                    any |= attack ? AttackMove(leader, lx, lz) : Move(leader, lx, lz);
                    if (any) message = Message;
                    corners = new List<Waypoint>(leader.route);
                    if (corners.Count > 0) corners.RemoveAt(corners.Count - 1);
                }
            }
            foreach (var u in units)
            {
                if (u == leader) continue;
                var (px, pz) = Spot(u);
                if (queue) { Enqueue(u, new QueuedOrder(attack ? UnitOrder.AttackMove : UnitOrder.Move, px, pz)); any = true; message = "Приказ добавлен в очередь."; continue; }
                if (corners != null && corners.Count > 0 && Follows(u, leader, corners, px, pz)) { OrderAlong(u, corners, px, pz, attack); any = true; continue; }
                any |= attack ? AttackMove(u, px, pz) : Move(u, px, pz);
                if (any) message = Message;
            }
            Message = message;
            if (!queue) SpreadLanes(units, x, z);
            return any;
        }
        // A group's paths share their corners, so the whole group would file through one lane round every corner and up
        // every ramp. Each unit's corners are moved off the obstacle by its offset across the group's way, counted from the
        // unit nearest the inner side of that corner, as far as the passage allows: the group keeps its order across and
        // fills the width of a ramp or a gap.
        void SpreadLanes(IReadOnlyList<PrototypeUnit> units, Fix x, Fix z)
        {
            Fix mx = 0, mz = 0; int count = 0;
            foreach (var u in units) if (!u.Stats.Flying) { mx += u.X; mz += u.Z; count++; }
            if (count < 2) return;
            mx /= count; mz /= count;
            Fix dx = x - mx, dz = z - mz, dl = Math.Sqrt(dx * dx + dz * dz);
            if (dl < 1) return;
            Fix lx = -dz / dl, lz = dx / dl, minLateral = Fix.MaxValue, maxLateral = Fix.MinValue;
            foreach (var u in units)
            {
                if (u.Stats.Flying) continue;
                Fix lateral = (u.X - mx) * lx + (u.Z - mz) * lz;
                minLateral = Math.Min(minLateral, lateral); maxLateral = Math.Max(maxLateral, lateral);
            }
            foreach (var u in units)
            {
                if (u.Stats.Flying || u.route.Count < 2) continue;
                Fix lateral = (u.X - mx) * lx + (u.Z - mz) * lz, r = u.Stats.Radius;
                var points = new List<Waypoint>(u.route);
                Fix px = u.X, pz = u.Z;
                for (int k = 0; k < points.Count - 1; k++)
                {
                    var p = points[k]; var next = points[k + 1];
                    Fix ix = p.X - px, iz = p.Z - pz, il = Math.Sqrt(ix * ix + iz * iz);
                    Fix ox = next.X - p.X, oz = next.Z - p.Z, ol = Math.Sqrt(ox * ox + oz * oz);
                    if (il < (Fix)1e-3f || ol < (Fix)1e-3f) { px = p.X; pz = p.Z; continue; }
                    ix /= il; iz /= il; ox /= ol; oz /= ol;
                    // The path turns towards the inner side of the corner; the unit moves away from it.
                    Fix turn = ix * oz - iz * ox, tx = ix + ox, tz = iz + oz, tl = Math.Sqrt(tx * tx + tz * tz);
                    if (tl < (Fix)1e-3f || Math.Abs(turn) < (Fix)1e-3f) { px = p.X; pz = p.Z; continue; }
                    Fix nx = -tz / tl, nz = tx / tl, side = turn > 0 ? 1 : -1;
                    Fix innerX = nx * side, innerZ = nz * side;
                    Fix fromInner = innerX * lx + innerZ * lz > 0 ? maxLateral - lateral : lateral - minLateral;
                    fromInner = Math.Min(fromInner, 4);
                    foreach (var share in Shares)
                    {
                        Fix sx = p.X - innerX * fromInner * share, sz = p.Z - innerZ * fromInner * share;
                        if (share > 0 && !(InMap(sx, sz) && CanTravel(px, pz, sx, sz, r) && CanTravel(sx, sz, next.X, next.Z, r))) continue;
                        points[k] = new Waypoint(sx, sz); break;
                    }
                    px = points[k].X; pz = points[k].Z;
                }
                u.route.Clear(); foreach (var p in points) u.route.Enqueue(p);
            }
        }
        static readonly Fix[] Shares = { 1, (Fix).75f, (Fix).5f, (Fix).25f, 0 };
        bool Follows(PrototypeUnit u, PrototypeUnit leader, List<Waypoint> corners, Fix x, Fix z)
        {
            if (u.Stats.Flying || Math.Abs(HeightAt(u.X, u.Z) - HeightAt(leader.X, leader.Z)) > (Fix).5f) return false;
            if ((u.X - leader.X) * (u.X - leader.X) + (u.Z - leader.Z) * (u.Z - leader.Z) > 36) return false;
            Fix r = u.Stats.Radius;
            return CanTravel(u.X, u.Z, corners[0].X, corners[0].Z, r) && CanTravel(corners[corners.Count - 1].X, corners[corners.Count - 1].Z, x, z, r);
        }
        // The same order as Move or AttackMove, along corners found for another unit.
        void OrderAlong(PrototypeUnit u, List<Waypoint> corners, Fix x, Fix z, bool attack)
        {
            attack &= u.Stats.Damage > 0;
            GiveOrder(u, attack ? UnitOrder.AttackMove : UnitOrder.Move);
            if (attack) { u.attackX = x; u.attackZ = z; }
            u.HarvestTarget = null; u.arrived = false; u.stuckTime = 0; u.bestDistance = Fix.MaxValue; u.replans = 0;
            SetRoute(u, new List<Waypoint>(corners) { new Waypoint(x, z) });
        }
        // Fills the cached edges for every ground unit size at once, so the first long path of the match does not stall it.
        public void PrepareNavigation()
        {
            var radii = new HashSet<Fix>();
            foreach (var roster in new[] { Balance.Expedition, Balance.Resonance }) foreach (var stats in roster.Units) if (stats != null && !stats.Flying) radii.Add(stats.Radius);
            foreach (var radius in radii) for (int cell = 0; cell < 4096; cell++) for (int dir = 0; dir < 8; dir++) Passable(cell, dir, radius);
        }
        bool FreeSpot(PrototypeUnit unit, Fix x, Fix z, Fix level)
        {
            Fix r = unit.Stats.Radius;
            if (x < r || z < r || x >= 64 - r || z >= 64 - r) return false;
            if (unit.Stats.Flying) return true;
            return Math.Abs(HeightAt(x, z) - level) < (Fix).51f && CanTravel(x, z, x, z, r);
        }
        // Ground units whose remaining route now crosses a building plan it again to the same destination.
        void RouteAroundNewBuildings()
        {
            foreach (var u in Units)
            {
                if (u.Stats.Flying || !u.Moving) continue;
                Fix x = u.X, z = u.Z; bool crossed = false;
                foreach (var p in u.route) { if (!CanTravel(x, z, p.X, p.Z, u.Stats.Radius)) crossed = true; x = p.X; z = p.Z; }
                if (crossed) Replan(u);
            }
        }
        void Replan(PrototypeUnit u)
        {
            var message = Message; var deposit = u.HarvestTarget; int replans = u.replans;
            if (u.delivering) Haul(u); else if (deposit != null) PlanPath(u, deposit.X + (Fix).5f, deposit.Z + (Fix).5f); else PlanPath(u, u.goalX, u.goalZ);
            u.replans = replans; Message = message;
        }

        // ---------- Path search ----------
        static int Cell(Fix x, Fix z) => Math.Max(0, Math.Min(63, (int)z)) * 64 + Math.Max(0, Math.Min(63, (int)x));
        // Cells from the start (excluded) to the goal cell, or to a cell next to the deposit. When the goal cannot be
        // reached, the path leads to the reachable cell closest to the target.
        List<int> FindPath(int start, int goal, ResourceDeposit deposit, Fix targetX, Fix targetZ, Fix radius, out bool reached)
        {
            searchStamp++; open.Clear();
            int best = start; Fix bestDistance = Fix.MaxValue;
            seen[start] = searchStamp; pathCost[start] = 0; cameFrom[start] = start;
            open.Push(start, Heuristic(start, goal));
            reached = false; int end = -1;
            while (open.Count > 0)
            {
                int p = open.Pop();
                if (done[p] == searchStamp) continue;
                done[p] = searchStamp;
                int x = p % 64, z = p / 64;
                if (deposit != null ? Math.Abs(x - deposit.X) + Math.Abs(z - deposit.Z) == 1 : p == goal) { reached = true; end = p; break; }
                Fix distance = (x + (Fix).5f - targetX) * (x + (Fix).5f - targetX) + (z + (Fix).5f - targetZ) * (z + (Fix).5f - targetZ);
                if (distance < bestDistance) { bestDistance = distance; best = p; }
                for (int dir = 0; dir < 8; dir++)
                {
                    if (!Passable(p, dir, radius)) continue;
                    int n = (z + Dz[dir]) * 64 + x + Dx[dir];
                    Fix cost = pathCost[p] + (dir < 4 ? 1 : (Fix)1.41421f);
                    if (done[n] == searchStamp || (seen[n] == searchStamp && cost >= pathCost[n])) continue;
                    seen[n] = searchStamp; pathCost[n] = cost; cameFrom[n] = p;
                    open.Push(n, cost + Heuristic(n, goal) + Straying(n, start, goal));
                }
            }
            if (end < 0) end = best;
            var cells = new List<int>();
            for (int p = end; p != start; p = cameFrom[p]) cells.Add(p);
            cells.Reverse();
            return cells;
        }
        static Fix Heuristic(int cell, int goal)
        {
            int dx = Math.Abs(cell % 64 - goal % 64), dz = Math.Abs(cell / 64 - goal / 64);
            return dx + dz - (Fix).58579f * Math.Min(dx, dz);
        }
        // Many paths cost exactly the same in exact arithmetic; of those the search prefers cells near the straight line
        // from start to goal, so the way, and the corners a group shares, do not depend on the order of the heap.
        static Fix Straying(int cell, int start, int goal)
        {
            int gx = goal % 64, gz = goal / 64;
            return Math.Abs((cell % 64 - gx) * (start / 64 - gz) - (start % 64 - gx) * (cell / 64 - gz)) * (Fix).001f;
        }
        // Edge between neighbouring cell centres for a radius, cached until a building or deposit near it changes.
        bool Passable(int cell, int dir, Fix radius)
        {
            int key = (int)Math.Round(radius * 1000);
            if (!edges.TryGetValue(key, out var table)) edges[key] = table = new sbyte[4096 * 8];
            int index = cell * 8 + dir;
            if (table[index] != 0) return table[index] > 0;
            int x = cell % 64, z = cell / 64, nx = x + Dx[dir], nz = z + Dz[dir];
            bool ok = CanStep(x, z, nx, nz) && (dir < 4 || (CanStep(x, z, nx, z) && CanStep(x, z, x, nz))) && CanTravel(x + (Fix).5f, z + (Fix).5f, nx + (Fix).5f, nz + (Fix).5f, radius);
            table[index] = (sbyte)(ok ? 1 : -1);
            return ok;
        }

        // ---------- Steps ----------
        void StepUnits(Fix dt)
        {
            foreach (var u in Units) { u.levelY = HeightAt(u.X, u.Z); u.touchedArrived = false; u.startX = u.X; u.startZ = u.Z; }
            FillBlocks();
            foreach (var u in Units) Fight(u, dt);
            blocksTrue = false;
            Lap(StepProfile.Phase.Fight);
            FillBuckets();
            foreach (var u in Units) if (u.leaping) StepLeap(u, dt); else if (!Anchored(u)) Follow(u, dt);
            Lap(StepProfile.Phase.Move);
            for (int i = 0; i < 2; i++) { FillBuckets(); Separate(); }
            Lap(StepProfile.Phase.Separate);
            foreach (var u in Units)
            {
                if (!u.Stats.Flying) { KeepOutOfObstacles(u); continue; }
                Fix r = u.Stats.Radius; u.X = Math.Max(r, Math.Min(64 - r, u.X)); u.Z = Math.Max(r, Math.Min(64 - r, u.Z));
            }
            Lap(StepProfile.Phase.Obstacles);
            foreach (var u in Units) { if (!u.leaping && !Anchored(u)) UpdateArrival(u, dt); u.lastDx = u.X - u.startX; u.lastDz = u.Z - u.startZ; }
            Lap(StepProfile.Phase.Arrival);
            TickConstruction(dt);
            TickRepair(dt);
            TickAutoRepair(dt);
            // Before mining: it counts the time since the last hit, which the nanite cocoon waits for too.
            TickMending(dt);
            TickHarvest(dt);
            TickDisassembly(dt);
            Lap(StepProfile.Phase.Works);
            RemoveDead();
            RunQueues();
            Lap(StepProfile.Phase.Cleanup);
        }
        // Exactly along the route: a lone unit ends precisely on its destination. In a crowd a unit pushed off its line does
        // not go back for every corner: once near one, with a clear straight line to the next, it cuts across.
        void Follow(PrototypeUnit u, Fix dt)
        {
            if (u.route.Count > 1)
            {
                var corner = u.route.Peek();
                Fix cx = corner.X - u.X, cz = corner.Z - u.Z, reach = u.Stats.Radius + (Fix).4f;
                if (cx * cx + cz * cz < reach * reach)
                {
                    var next = Second(u.route);
                    if (CanTravel(u.X, u.Z, next.X, next.Z, u.Stats.Radius)) u.route.Dequeue();
                }
            }
            Fix travel = Math.Min(dt * u.Stats.Speed, FreeTravel(u, dt));
            while (u.route.Count > 0 && travel > 0)
            {
                var p = u.route.Peek(); Fix dx = p.X - u.X, dz = p.Z - u.Z, d = Math.Sqrt(dx * dx + dz * dz);
                if (d <= travel) { u.X = p.X; u.Z = p.Z; travel -= d; u.route.Dequeue(); }
                else { u.X += dx / d * travel; u.Z += dz / d * travel; travel = 0; }
            }
        }
        static Waypoint Second(Queue<Waypoint> route) { int i = 0; foreach (var p in route) if (i++ == 1) return p; return route.Peek(); }
        // Unit vector toward the next waypoint; `fallback` when there is none or it is reached.
        static void Heading(PrototypeUnit u, Fix fallbackX, Fix fallbackZ, out Fix hx, out Fix hz)
        {
            hx = fallbackX; hz = fallbackZ;
            if (u.route.Count == 0) return;
            var next = u.route.Peek(); Fix dx = next.X - u.X, dz = next.Z - u.Z, l = Math.Sqrt(dx * dx + dz * dz);
            if (l > (Fix)1e-4f) { hx = dx / l; hz = dz / l; }
        }
        // A worker in the mining cycle passes through others, as in StarCraft; a leaper in flight passes over them.
        static bool Ghost(PrototypeUnit u) => u.HarvestTarget != null || u.leaping;
        // Units by map cell; contacts are always within the 3×3 cells around a unit.
        void FillBuckets()
        {
            foreach (var index in usedBuckets) buckets[index].Clear();
            usedBuckets.Clear();
            foreach (var u in Units)
            {
                if (Ghost(u)) continue;
                int index = Cell(u.X, u.Z);
                if (buckets[index] == null) buckets[index] = new List<PrototypeUnit>();
                if (buckets[index].Count == 0) usedBuckets.Add(index);
                buckets[index].Add(u);
            }
        }
        // How far a unit may advance this step: behind an ally in its lane that goes the same way it queues at that
        // ally's pace instead of stepping into it and being pushed back every step.
        Fix FreeTravel(PrototypeUnit u, Fix dt)
        {
            Fix free = dt * u.Stats.Speed;
            if (u.route.Count == 0 || Ghost(u)) return free;
            Heading(u, 0, 0, out Fix hx, out Fix hz);
            if (hx == 0 && hz == 0) return free;
            int cx = Math.Max(0, Math.Min(63, (int)u.X)), cz = Math.Max(0, Math.Min(63, (int)u.Z));
            for (int z = Math.Max(0, cz - 1); z <= Math.Min(63, cz + 1); z++)
            for (int x = Math.Max(0, cx - 1); x <= Math.Min(63, cx + 1); x++)
            {
                var bucket = buckets[z * 64 + x];
                if (bucket == null) continue;
                foreach (var v in bucket)
                {
                    if (v == u || !v.Moving || v.Stats.Flying != u.Stats.Flying) continue;
                    Fix dx = v.X - u.X, dz = v.Z - u.Z, ahead = dx * hx + dz * hz;
                    if (ahead <= 0) continue;
                    Fix reach = u.Stats.Radius + v.Stats.Radius, lateral = Math.Abs(dx * hz - dz * hx);
                    if (lateral >= reach * (Fix).9f) continue;
                    Heading(v, 0, 0, out Fix vx, out Fix vz);
                    Fix together = hx * vx + hz * vz;
                    if (together < (Fix).3f) continue;
                    // Two units side by side may each see the other ahead in its lane; only one of them waits.
                    if (-(dx * vx + dz * vz) > 0 && u.Id < v.Id) continue;
                    // The ally ahead moves on by about what it covered last step along our heading.
                    Fix gap = ahead - Math.Sqrt(reach * reach - lateral * lateral), pace = Math.Max(0, v.lastDx * hx + v.lastDz * hz);
                    free = Math.Min(free, Math.Max(0, gap) + pace);
                }
            }
            return free;
        }
        // Overlapping units push each other apart. A moving unit shoves an idle ally out of the way, unless that ally has
        // already arrived at the same point: then the mover stops beside it. Two moving units also side-step, so they pass.
        // A standing unit that holds position, builds, is shooting, or belongs to the other side is not pushed at all.
        void Separate()
        {
            foreach (var u in Units)
            {
                if (Ghost(u)) continue;
                int cx = Math.Max(0, Math.Min(63, (int)u.X)), cz = Math.Max(0, Math.Min(63, (int)u.Z));
                for (int z = Math.Max(0, cz - 1); z <= Math.Min(63, cz + 1); z++)
                for (int x = Math.Max(0, cx - 1); x <= Math.Min(63, cx + 1); x++)
                {
                    var bucket = buckets[z * 64 + x];
                    if (bucket == null) continue;
                    foreach (var v in bucket) if (v.Id > u.Id && v.Stats.Flying == u.Stats.Flying) Push(u, v);
                }
            }
        }
        void Push(PrototypeUnit a, PrototypeUnit b)
        {
            Fix dx = b.X - a.X, dz = b.Z - a.Z, reach = a.Stats.Radius + b.Stats.Radius;
            // Apart along one axis already means apart: the same answer as the squared distance, without multiplying.
            if (Math.Abs(dx) >= reach || Math.Abs(dz) >= reach) return;
            Fix d2 = dx * dx + dz * dz;
            if (d2 >= reach * reach) return;
            Fix d = Math.Sqrt(d2), nx, nz;
            if (d > (Fix)1e-4f) { nx = dx / d; nz = dz / d; }
            else { Fix angle = (a.Id * 7 + b.Id * 3) * (Fix)2.39996f; nx = Math.Cos(angle); nz = Math.Sin(angle); }
            Fix overlap = reach - d + (Fix)1e-4f;
            bool af = Firm(a, b), bf = Firm(b, a);
            if (af != bf) { if (af) Slide(b, a, nx, nz, overlap); else Slide(a, b, -nx, -nz, overlap); return; }
            bool am = a.Moving, bm = b.Moving;
            if (am && !bm && !(b.arrived && SameGoal(a, b))) { Shove(a, b, nx, nz, overlap); return; }
            if (bm && !am && !(a.arrived && SameGoal(b, a))) { Shove(b, a, -nx, -nz, overlap); return; }
            Fix wa = (Fix).5f, wb = (Fix).5f;
            if (am && bm)
            {
                Heading(a, nx, nz, out Fix ax, out Fix az); Heading(b, -nx, -nz, out Fix bx, out Fix bz);
                if (ax * bx + az * bz < -(Fix).3f)
                {
                    // Head-on: each also steps to its own right, so they pass instead of pressing into each other.
                    a.X += (-nx * (Fix).5f + az * (Fix).4f) * overlap; a.Z += (-nz * (Fix).5f - ax * (Fix).4f) * overlap;
                    b.X += (nx * (Fix).5f + bz * (Fix).4f) * overlap; b.Z += (nz * (Fix).5f - bx * (Fix).4f) * overlap;
                    return;
                }
                // Same way: the one behind gives way and queues up; the one in front keeps its line.
                bool bAhead = nx * (ax + bx) + nz * (az + bz) > 0;
                wa = bAhead ? (Fix).85f : (Fix).15f; wb = 1 - wa;
            }
            else if (am) a.touchedArrived = true;
            else if (bm) b.touchedArrived = true;
            bool aHome = !am && AtHome(a), bHome = !bm && AtHome(b);
            // A unit standing on the very spot it was sent to gives way little; the one still looking for room moves.
            if (!am && !bm && aHome != bHome) { wa = aHome ? (Fix).15f : (Fix).85f; wb = 1 - wa; }
            a.X -= nx * overlap * wa; a.Z -= nz * overlap * wa;
            b.X += nx * overlap * wb; b.Z += nz * overlap * wb;
            // Two idle units easing apart settle where they end up instead of walking back into each other; a unit pushed off
            // its own spot keeps it and returns there once the crowd has settled.
            if (!am && !bm)
            {
                if (!aHome) { a.restX = a.X; a.restZ = a.Z; }
                if (!bHome) { b.restX = b.X; b.restZ = b.Z; }
            }
        }
        static bool Firm(PrototypeUnit u, PrototypeUnit other) =>
            // A howitzer that is not packed is anchored to the ground.
            Anchored(u) || (!u.Moving && (u.Order == UnitOrder.Hold || u.Constructing != null || u.Welding || u.Owner != other.Owner || (u.engaged && (u.Target != null || u.TargetBuilding != null))));
        // Only the other unit gives way; a mover pressing into a firm unit also slips sideways, so it goes round it.
        // (nx, nz) points from the firm unit to the one that gives way. A ground unit slips to the side that is free of walls and
        // standing units, so a line of units hitting a wall is passed on the open side instead of being pressed into the crowd.
        void Slide(PrototypeUnit unit, PrototypeUnit firm, Fix nx, Fix nz, Fix overlap)
        {
            Fix dx = nx, dz = nz;
            if (unit.Moving)
            {
                Heading(unit, -nx, -nz, out Fix hx, out Fix hz);
                Fix along = nx * hx + nz * hz, sx = nx - hx * along, sz = nz - hz * along, sl = Math.Sqrt(sx * sx + sz * sz);
                if (sl < (Fix).2f) { Fix side = unit.Id % 2 == 0 ? 1 : -1; sx = -hz * side; sz = hx * side; } else { sx /= sl; sz /= sl; }
                if (!unit.Stats.Flying)
                {
                    Fix probe = unit.Stats.Radius + (Fix).35f;
                    Fix ax = unit.X + hx * (Fix).3f, az = unit.Z + hz * (Fix).3f;
                    if (!Open(unit, ax + sx * probe, az + sz * probe) && Open(unit, ax - sx * probe, az - sz * probe)) { sx = -sx; sz = -sz; }
                }
                dx += sx; dz += sz;
            }
            unit.X += dx * overlap; unit.Z += dz * overlap;
        }
        // Room for the unit at a point: no wall, cliff or building there and no other standing unit of its layer.
        bool Open(PrototypeUnit unit, Fix x, Fix z)
        {
            Fix r = unit.Stats.Radius;
            if (!CanTravel(x, z, x, z, r)) return false;
            foreach (var v in Units)
            {
                if (v == unit || v.Moving || v.Stats.Flying != unit.Stats.Flying || Ghost(v)) continue;
                Fix reach = (r + v.Stats.Radius) * (Fix).9f, dx = v.X - x, dz = v.Z - z;
                if (dx * dx + dz * dz < reach * reach) return false;
            }
            return true;
        }
        // An idle unit in the way steps aside across the mover's heading rather than being bulldozed ahead of it.
        static void Shove(PrototypeUnit mover, PrototypeUnit idle, Fix nx, Fix nz, Fix overlap)
        {
            var next = mover.route.Peek();
            Fix hx = next.X - mover.X, hz = next.Z - mover.Z, hl = Math.Sqrt(hx * hx + hz * hz);
            if (hl < (Fix)1e-4f) { hx = nx; hz = nz; } else { hx /= hl; hz /= hl; }
            Fix along = nx * hx + nz * hz, sx = nx - hx * along, sz = nz - hz * along, sl = Math.Sqrt(sx * sx + sz * sz);
            if (sl < (Fix).2f) { Fix side = idle.Id % 2 == 0 ? 1 : -1; sx = -hz * side; sz = hx * side; }
            else { sx /= sl; sz /= sl; }
            Fix dx = sx + hx * Math.Max(0, along) * (Fix).3f, dz = sz + hz * Math.Max(0, along) * (Fix).3f, dl = Math.Sqrt(dx * dx + dz * dz);
            idle.X += dx / dl * overlap * (Fix).9f; idle.Z += dz / dl * overlap * (Fix).9f;
            idle.calmTime = 0;
            mover.X -= nx * overlap * (Fix).1f; mover.Z -= nz * overlap * (Fix).1f;
        }
        static bool AtHome(PrototypeUnit u) => u.arrived && u.holdsSpot;
        static bool SameGoal(PrototypeUnit mover, PrototypeUnit arrived) =>
            (mover.goalX - arrived.arrivedX) * (mover.goalX - arrived.arrivedX) + (mover.goalZ - arrived.arrivedZ) * (mover.goalZ - arrived.arrivedZ) < (Fix).09f;
        // Slides a ground unit back out of blocked cells, cliff edges, ramp sides and the map border. Cells are judged from the
        // height and cell the unit stood on before this step, so a push never drops it off a cliff, lifts it onto a plateau
        // or carries it over a ramp side; a unit on a ramp is also kept off the cells beside it, as CanTravel requires.
        void KeepOutOfObstacles(PrototypeUnit u)
        {
            Fix r = u.Stats.Radius;
            int sx = Math.Max(0, Math.Min(63, (int)u.startX)), sz = Math.Max(0, Math.Min(63, (int)u.startZ));
            for (int iteration = 0; iteration < 2; iteration++)
            {
                u.X = Math.Max(r + (Fix)1e-4f, Math.Min(64 - r - (Fix)1e-4f, u.X)); u.Z = Math.Max(r + (Fix)1e-4f, Math.Min(64 - r - (Fix)1e-4f, u.Z));
                for (int zz = (int)(u.Z - r); zz <= (int)(u.Z + r); zz++)
                for (int xx = (int)(u.X - r); xx <= (int)(u.X + r); xx++)
                {
                    if (Map.Inside(xx, zz) && !Occupied(xx, zz) && !RampSide(sx, sz, xx, zz) && !RampSide((int)u.X, (int)u.Z, xx, zz) && Math.Abs(Map.Height(xx, zz, Math.Max(0, Math.Min(1, u.X - xx))) - u.levelY) <= (Fix).51f) continue;
                    Fix nearX = Math.Max(xx, Math.Min(u.X, xx + 1)), nearZ = Math.Max(zz, Math.Min(u.Z, zz + 1));
                    Fix dx = u.X - nearX, dz = u.Z - nearZ, d2 = dx * dx + dz * dz;
                    if (d2 >= r * r) continue;
                    if (d2 < (Fix)1e-8f)
                    {
                        // The centre went inside the cell: leave through the nearest side.
                        Fix left = u.X - xx, right = xx + 1 - u.X, back = u.Z - zz, front = zz + 1 - u.Z, least = Math.Min(Math.Min(left, right), Math.Min(back, front));
                        if (least == left) u.X = xx - r - (Fix)1e-4f; else if (least == right) u.X = xx + 1 + r + (Fix)1e-4f;
                        else if (least == back) u.Z = zz - r - (Fix)1e-4f; else u.Z = zz + 1 + r + (Fix)1e-4f;
                        continue;
                    }
                    Fix d = Math.Sqrt(d2), push = r - d + (Fix)1e-4f;
                    u.X += dx / d * push; u.Z += dz / d * push;
                }
            }
        }
        // Done when the route ends, on touching an ally that already arrived at the same point nearby, or when the unit has
        // made no progress along its route for a while: close to the goal it stops, farther away it plans again and keeps going.
        void UpdateArrival(PrototypeUnit u, Fix dt)
        {
            if (!u.hasGoal) { ReturnToRest(u, dt); return; }
            if (u.route.Count == 0) { Arrive(u); return; }
            Fix dx = u.goalX - u.X, dz = u.goalZ - u.Z, d = Math.Sqrt(dx * dx + dz * dz);
            if (u.touchedArrived && d < 4 && !u.engaged) { Stand(u); return; }
            Fix left = RouteLength(u);
            if (left < u.bestDistance - (Fix).1f) { u.bestDistance = left; u.stuckTime = 0; return; }
            if ((u.stuckTime += dt) < (Fix)1.2f) return;
            if (d < (Fix)1.5f) Stand(u);
            else { u.replans++; Replan(u); u.stuckTime = 0; u.bestDistance = RouteLength(u); }
        }
        static Fix RouteLength(PrototypeUnit u)
        {
            Fix x = u.X, z = u.Z, sum = 0;
            foreach (var p in u.route) { sum += Math.Sqrt((p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z)); x = p.X; z = p.Z; }
            return sum;
        }
        // A returning unit keeps the destination of its real order, so later arrivals still gather around it. The end of a
        // move or attack-move leaves the unit idle; the end of a chase does not end the order.
        void Arrive(PrototypeUnit u)
        {
            u.route.Clear(); u.hasGoal = false; u.arrived = true; u.replans = 0;
            // A unit that reached its very destination holds that spot against the units still settling round it.
            if (!u.returning) { u.arrivedX = u.goalX; u.arrivedZ = u.goalZ; u.holdsSpot = (u.X - u.goalX) * (u.X - u.goalX) + (u.Z - u.goalZ) * (u.Z - u.goalZ) < (Fix).15f * (Fix).15f; }
            u.returning = false; u.restX = u.X; u.restZ = u.Z; u.calmTime = 0;
            if (!u.engaged && (u.Order == UnitOrder.Move || u.Order == UnitOrder.AttackMove)) u.Order = UnitOrder.Idle;
        }
        // An arrived unit shoved off its place by units passing through walks back once the crowd has gone: at most twice
        // per order, so neighbours cannot keep trading places.
        void ReturnToRest(PrototypeUnit u, Fix dt)
        {
            if (!u.arrived || u.returns >= 2 || Ghost(u) || u.engaged || u.Order != UnitOrder.Idle) return;
            Fix dx = u.restX - u.X, dz = u.restZ - u.Z;
            if (dx * dx + dz * dz < (Fix).25f) return;
            if ((u.calmTime += dt) < (Fix).6f) return;
            int returns = u.returns; Fix ax = u.arrivedX, az = u.arrivedZ, rx = u.restX, rz = u.restZ; var message = Message;
            if (PlanPath(u, rx, rz)) { u.returning = true; u.arrived = true; u.arrivedX = ax; u.arrivedZ = az; }
            u.returns = returns + 1; Message = message;
        }
        // Stops short of the goal; a worker gives up harvesting unless it stands next to its deposit or carries a load home.
        void Stand(PrototypeUnit u)
        {
            var deposit = u.HarvestTarget;
            if (deposit != null && !u.delivering && !NextTo(u, deposit)) u.HarvestTarget = null;
            Arrive(u);
        }

        // Binary min-heap of cells keyed by A* priority.
        sealed class CellHeap
        {
            readonly List<(int cell, Fix key)> items = new List<(int, Fix)>();
            public int Count => items.Count;
            public void Clear() => items.Clear();
            public void Push(int cell, Fix key)
            {
                items.Add((cell, key));
                for (int i = items.Count - 1; i > 0;)
                {
                    int parent = (i - 1) / 2;
                    if (items[parent].key <= items[i].key) break;
                    (items[parent], items[i]) = (items[i], items[parent]); i = parent;
                }
            }
            public int Pop()
            {
                int top = items[0].cell, last = items.Count - 1;
                items[0] = items[last]; items.RemoveAt(last);
                for (int i = 0; ;)
                {
                    int l = i * 2 + 1, r = l + 1, least = i;
                    if (l < items.Count && items[l].key < items[least].key) least = l;
                    if (r < items.Count && items[r].key < items[least].key) least = r;
                    if (least == i) break;
                    (items[least], items[i]) = (items[i], items[least]); i = least;
                }
                return top;
            }
        }
    }
}
