using Order.Content;

namespace Order.Simulation
{
    // A fingerprint of the match state: two runs of the same match with the same orders on the same frames must give the
    // same hash on every frame. Lockstep machines compare it to catch a desync; tests compare twin runs. It reads the
    // numbers bit for bit (Fix raw values), so a difference in the last bit of a position shows.
    public sealed partial class PrototypeMatch
    {
        public ulong StateHash()
        {
            var h=new StateHasher();
            h.Add(Frame);h.Add(Clock);h.Add(nextUnitId);
            for(int side=0;side<2;side++){h.Add(metalStock[side]);h.Add(plasmaStock[side]);}
            foreach(int level in levels)h.Add(level);
            foreach(var r in Map.Resources){h.Add(r.Remaining);h.Add(r.WrappedBy);}
            h.Add(Buildings.Count);
            foreach(var b in Buildings)
            {
                h.Add(b.X);h.Add(b.Z);h.Add(b.Kind);h.Add(b.Owner);h.Add(b.Health);h.Add(b.Ready);h.Add(b.Powered);h.Add(b.DarkFor);
                h.Add(b.Training);h.Add(b.started);h.Add(b.queue.Count);foreach(var kind in b.queue)h.Add((int)kind);
                h.Add(b.research.HasValue?(int)b.research.Value:-1);h.Add(b.ResearchTime);
                h.Add(b.builder?.Id??0);h.Add(b.RallyX);h.Add(b.RallyZ);
            }
            h.Add(Units.Count);
            foreach(var u in Units)
            {
                h.Add(u.Id);h.Add((int)u.Kind);h.Add(u.Owner);h.Add(u.X);h.Add(u.Z);h.Add(u.Health);h.Add((int)u.Order);
                h.Add(u.Target?.Id??0);h.Add(u.TargetBuilding!=null?Buildings.IndexOf(u.TargetBuilding):-1);h.Add(u.cooldown);
                h.Add(u.Carrying);h.Add(u.Wrap);h.Add(u.Cocoon);h.Add((int)u.SiegeMode);h.Add(u.siegeTimer);
                h.Add(u.leaping);h.Add(u.Disassembly);h.Add(u.queued.Count);
                h.Add(u.route.Count);foreach(var p in u.route){h.Add(p.X);h.Add(p.Z);}
            }
            for(int z=0;z<64;z++)for(int x=0;x<64;x++)h.Add(Explored[x,z]);
            return h.Value;
        }
        // FNV-1a over 64 bits.
        struct StateHasher
        {
            ulong value;
            public ulong Value=>value==0?14695981039346656037UL:value;
            public void Add(int v)
            {
                if(value==0)value=14695981039346656037UL;
                for(int i=0;i<4;i++){value^=(byte)(v>>(i*8));value*=1099511628211UL;}
            }
            public void Add(long v){Add((int)v);Add((int)(v>>32));}
            public void Add(Fix v)=>Add(v.Raw);
            public void Add(bool v)=>Add(v?1:0);
        }
    }
}
