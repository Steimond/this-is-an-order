using System;
using System.Collections.Generic;
using Order.Content;
using Order.Contracts;
using Order.Balance;
using Order.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Order.Presentation
{
    public sealed partial class TemplePrototypeView
    {
        public void SetMode(MatchMode mode,int kind=-1)
        {
            if(mode==MatchMode.Build&&Array.IndexOf(Match.Roster(PrototypeMatch.Player).Buildable,kind)<0)mode=MatchMode.None;
            if(mode==MatchMode.Attack&&!HasFighter()){Match.Message="Выбранные юниты не атакуют.";mode=MatchMode.None;}
            if(mode==MatchMode.Rally&&(selectedBuilding==null||selectedBuilding.Stats.Trains.Length==0))mode=MatchMode.None;
            if((mode==MatchMode.Move&&!selection.Own)||(mode==MatchMode.Harvest&&!HasHarvester())||(mode==MatchMode.Repair&&Repairers(null).Count==0))SelectWorker();
            if(mode==MatchMode.Repair&&Repairers(null).Count==0){Match.Message="Чинить могут только рабочие людей.";mode=MatchMode.None;}
            Mode=mode;buildKind=mode==MatchMode.Build?kind:-1;ModeHint=Hint();hud?.Refresh();
        }
        bool HasHarvester(){foreach(var u in selection.Orderable)if(u.Stats.HarvestAmount>0)return true;return false;}
        // The selected workers of a side that repairs, but not the machinery they are to mend.
        List<PrototypeUnit> Repairers(PrototypeUnit target)
        {
            var workers=new List<PrototypeUnit>();
            foreach(var u in selection.Orderable)if(u.Kind==UnitKind.Worker&&u!=target&&Match.Roster(u.Owner).Repairs)workers.Add(u);
            return workers;
        }
        // Every selected worker mends the target, as SCVs do; the rest of the selection walks up to it.
        public bool RepairSelection(PrototypeBuilding building,PrototypeUnit unit)
        {
            var workers=Repairers(unit);
            if(workers.Count==0){Match.Message="Чинить могут только рабочие людей.";hud?.Refresh();return false;}
            int started=0;string reason=null;
            foreach(var w in workers){if(building!=null?Order(MatchCommand.Repair(Me,w,building)):Order(MatchCommand.Repair(Me,w,unit)))started++;else reason??=Match.Message;}
            if(started==0){Match.Message=reason;hud?.Refresh();return false;}
            float cx=building!=null?(float)building.X+building.Width*.5f:(float)unit.X,cz=building!=null?(float)building.Z+building.Width*.5f:(float)unit.Z;
            var rest=new List<PrototypeUnit>(selection.Orderable);rest.RemoveAll(u=>workers.Contains(u)||u==unit);
            var message=$"{(started>1?$"Рабочие ×{started}":workers[0].Stats.Name)}: ремонт — {(building!=null?building.Stats.Name:unit.Stats.Name)}.";
            if(rest.Count>0)Order(MatchCommand.Move(Me,rest,cx,cz));
            Match.Message=message;Mark(new Vector2(cx,cz),true);hud?.Refresh();
            return true;
        }
        // Shift: every selected worker repairs the target after what it is doing now.
        bool QueueRepairSelection(PrototypeBuilding building,PrototypeUnit unit)
        {
            var workers=Repairers(unit);int queued=0;string reason=null;
            foreach(var w in workers){if(building!=null?Order(MatchCommand.Repair(Me,w,building,true)):Order(MatchCommand.Repair(Me,w,unit,true)))queued++;else reason??=Match.Message;}
            if(queued==0){if(reason!=null)Match.Message=reason;hud?.Refresh();return false;}
            Match.Message=$"Ремонт добавлен в очередь: {(building!=null?building.Stats.Name:unit.Stats.Name)}.";
            Mark(building!=null?new Vector2((float)building.X+building.Width*.5f,(float)building.Z+building.Width*.5f):new Vector2((float)unit.X,(float)unit.Z),true);hud?.Refresh();
            return true;
        }
        // Any packed howitzer in the selection deploys where it stands; when all of them are deployed, they pack up.
        public void ToggleDeploySelection()
        {
            var guns=new List<PrototypeUnit>(selection.Orderable).FindAll(u=>u.Stats.Siege);if(guns.Count==0)return;
            bool deploy=guns.Exists(u=>u.SiegeMode==SiegeState.Packed||u.SiegeMode==SiegeState.Packing);
            foreach(var g in guns)if((g.SiegeMode==SiegeState.Packed||g.SiegeMode==SiegeState.Packing)==deploy)Order(MatchCommand.ToggleDeploy(Me,g));
            if(guns.Count>1)Match.Message=$"Гаубицы ×{guns.Count}: {(deploy?"развёртывание":"сворачиваются")}.";
            hud?.Refresh();
        }
        public void ToggleAutoRepair()
        {
            var workers=Repairers(null);if(workers.Count==0)return;
            bool on=!workers.TrueForAll(u=>u.AutoRepair);
            Order(MatchCommand.AutoRepair(Me,workers,on));
            Match.Message=$"Авторемонт {(on?"включён":"выключен")}.";hud?.Refresh();
        }
        // Right click on an own extractor means its rift: the ray past the model would hit the ground behind it.
        Vector2 ExtractorPoint(Vector2 screen,Vector2 point)
        {
            var b=BuildingAt(screen);
            return b!=null&&b.Owner==PrototypeMatch.Player&&b.Stats.Extracts?new Vector2(b.X+b.Width*.5f,b.Z+b.Width*.5f):point;
        }
        // The own building or machinery under the cursor that needs repair; with `damaged` only one that is damaged.
        bool RepairTargetAt(Vector2 screen,bool damaged,out PrototypeBuilding building,out PrototypeUnit unit)
        {
            unit=UnitAt(screen);building=null;
            if(unit!=null&&(unit.Owner!=PrototypeMatch.Player||!unit.Stats.Mechanical||(damaged&&unit.Health>=unit.Stats.Health)))unit=null;
            if(unit==null)
            {
                building=BuildingAt(screen);
                if(building!=null&&(building.Owner!=PrototypeMatch.Player||building.Ready<1||(damaged&&building.Health>=building.Stats.Health)))building=null;
            }
            return unit!=null||building!=null;
        }
        public void DisassembleSelection()
        {
            int started=0,metal=0,plasma=0;string reason=null;
            foreach(var u in new List<PrototypeUnit>(selection.Orderable))
            {
                var (m,p)=Match.DisassemblyReturn(u);
                if(Order(MatchCommand.Disassemble(Me,u))){started++;metal+=m;plasma+=p;}else reason??=Match.Message;
            }
            if(started>1)Match.Message=$"Разборка: {started} юнитов, вернётся металл {metal}"+(plasma>0?$", плазма {plasma}.":".");
            else if(started==0&&reason!=null)Match.Message=reason;
            SetMode(MatchMode.None);
        }
        bool HasFighter(){foreach(var u in selection.Orderable)if(u.Stats.Damage>0)return true;return false;}
        // A group order (formation or gathering on the point, see PrototypeMatch.MoveGroup); workers harvest a deposit there,
        // the rest walk up to it. The marker sits on the point, or on the deposit when the order targets one.
        public bool MoveSelection(Vector2 point)=>MoveSelection(point,false);
        bool MoveSelection(Vector2 point,bool queue)
        {
            var units=selection.Orderable;if(units.Count==0)return false;
            bool any=queue?Order(MatchCommand.Move(Me,units,point.x,point.y,true)):Order(MatchCommand.Move(Me,units,point.x,point.y));
            int cx=(int)point.x,cz=(int)point.y;
            var deposit=Match.Map.Inside(cx,cz)&&Match.Explored[cx,cz]?Match.Map.ResourceAt(cx,cz):null;
            Mark(deposit!=null?new Vector2(deposit.X+.5f,deposit.Z+.5f):point,any);
            return any;
        }
        // Attack-move (A + click): the group goes to the point in formation and fights everything on the way.
        public bool AttackMoveSelection(Vector2 point)=>AttackMoveSelection(point,false);
        bool AttackMoveSelection(Vector2 point,bool queue)
        {
            var units=selection.Orderable;if(units.Count==0)return false;
            bool any=queue?Order(MatchCommand.AttackMove(Me,units,point.x,point.y,true)):Order(MatchCommand.AttackMove(Me,units,point.x,point.y));
            Mark(point,any,true);
            return any;
        }
        // Units that cannot hit the target (an assault unit against air) walk up to it instead.
        public bool AttackSelection(PrototypeUnit target,bool queue=false)
        {
            var units=selection.Orderable;if(units.Count==0)return false;
            bool any=queue?Order(MatchCommand.Attack(Me,units,target,true)):Order(MatchCommand.Attack(Me,units,target));
            if(any)MarkTarget(target);
            hud?.Refresh();
            return any;
        }
        // Construction, as in StarCraft: the selected worker nearest the site goes to build it; with no worker selected, the
        // nearest free one does (an idle one first, then a metal miner with empty hands).
        public int BuildOrders{get;private set;}
        public bool Build(int x,int z,int kind)=>Build(x,z,kind,false);
        // queue: Shift — the selected worker already building or with builds queued takes this one too.
        bool Build(int x,int z,int kind,bool queue)
        {
            if(Array.IndexOf(Match.Roster(PrototypeMatch.Player).Buildable,kind)<0)return false;
            int w=Match.Roster(PrototypeMatch.Player).Buildings[kind].Width;float cx=x+w*.5f,cz=z+w*.5f;
            PrototypeUnit worker=null;float best=float.MaxValue;
            // A machine worker becomes the building, so Shift sends the next free worker of the selection instead of
            // queueing the order.
            bool next=Match.Roster(PrototypeMatch.Player).ConsumesWorker&&queue;
            if(next)queue=false;
            if(queue)foreach(var u in selection.Orderable)if(u.Kind==UnitKind.Worker&&(u.SiteKind>=0||u.Constructing!=null||u.Queued.Count>0)){worker=u;break;}
            if(worker==null)foreach(var u in selection.Orderable)
            {
                if(u.Kind!=UnitKind.Worker||(next&&u.SiteKind>=0))continue;
                float d=((float)u.X-cx)*((float)u.X-cx)+((float)u.Z-cz)*((float)u.Z-cz);
                if(d<best){best=d;worker=u;}
            }
            if(worker==null)foreach(var u in Match.Units)
            {
                if(u.Owner!=PrototypeMatch.Player||u.Kind!=UnitKind.Worker||u.Constructing!=null||u.SiteKind>=0)continue;
                float d=((float)u.X-cx)*((float)u.X-cx)+((float)u.Z-cz)*((float)u.Z-cz)+(u.HarvestTarget==null&&!u.Moving?0:u.Carrying>0||(u.HarvestTarget!=null&&u.HarvestTarget.Plasma)?20000:10000);
                if(d<best){best=d;worker=u;}
            }
            if(worker==null){Match.Message="Нет рабочих.";hud?.Refresh();return false;}
            if(queue){bool queued=Order(MatchCommand.Build(Me,worker,x,z,kind,true));if(queued)Mark(new Vector2(cx,cz),true);hud?.Refresh();return queued;}
            bool ok=Order(MatchCommand.Build(Me,worker,x,z,kind));
            if(ok){BuildOrders++;Mark(new Vector2(cx,cz),true);}
            hud?.Refresh();return ok;
        }
        // Right click on an own building under construction: the nearest selected worker carries on with it, the rest of the
        // selection walks up to it. False when no selected worker can take it, so the click is a plain move.
        bool ResumeSelection(PrototypeBuilding site)
        {
            if(site.Builder!=null&&!selection.Contains(site.Builder))return false;
            PrototypeUnit worker=null;float best=float.MaxValue,cx=site.X+site.Width*.5f,cz=site.Z+site.Width*.5f;
            foreach(var u in selection.Orderable)
            {
                if(u.Kind!=UnitKind.Worker)continue;
                float d=((float)u.X-cx)*((float)u.X-cx)+((float)u.Z-cz)*((float)u.Z-cz);
                if(d<best){best=d;worker=u;}
            }
            if(worker==null||!Order(MatchCommand.Resume(Me,worker,site)))return false;
            var rest=new List<PrototypeUnit>(selection.Orderable);rest.Remove(worker);
            if(rest.Count>0){var message=Match.Message;Order(MatchCommand.Move(Me,rest,cx,cz));Match.Message=message;}
            Mark(new Vector2(cx,cz),true);hud?.Refresh();
            return true;
        }
        public bool CancelConstruction()
        {
            bool ok=selectedBuilding!=null&&selectedBuilding.Owner==PrototypeMatch.Player&&Order(MatchCommand.CancelConstruction(Me,selectedBuilding));
            if(ok)selectedBuilding=null;
            hud?.Refresh();return ok;
        }
        // Production in the selected building; the queue and rally point live in the simulation.
        public bool Train(UnitKind kind){bool ok=selectedBuilding!=null&&Order(MatchCommand.Train(Me,selectedBuilding,kind));hud?.Refresh();return ok;}
        public void CancelTraining(int index){if(selectedBuilding!=null)Order(MatchCommand.CancelTraining(Me,selectedBuilding,index));hud?.Refresh();}
        // A rally point on a deposit is drawn at its centre: workers will harvest it.
        public void SetRallyPoint(Vector2 point)
        {
            if(selectedBuilding==null||selectedBuilding.Owner!=PrototypeMatch.Player||selectedBuilding.Stats.Trains.Length==0)return;
            int cx=(int)point.x,cz=(int)point.y;
            var deposit=Match.Map.Inside(cx,cz)&&Match.Explored[cx,cz]?Match.Map.ResourceAt(cx,cz):null;
            if(deposit!=null)point=new Vector2(deposit.X+.5f,deposit.Z+.5f);
            Order(MatchCommand.SetRally(Me,selectedBuilding,point.x,point.y));Mark(point,true);hud?.Refresh();
        }
        public bool AttackSelection(PrototypeBuilding target,bool queue=false)
        {
            var units=selection.Orderable;if(units.Count==0)return false;
            bool any=queue?Order(MatchCommand.Attack(Me,units,target,true)):Order(MatchCommand.Attack(Me,units,target));
            if(any)
            {
                var view=scenery.Model("Target marker",selectionMesh,markerMaterial,worldRoot.transform,false);
                view.transform.position=new Vector3((float)target.X+target.Width*.5f,(float)Match.Map.Height(target.X,target.Z)+.05f,(float)target.Z+target.Width*.5f);
                markers.Add(new Marker{View=view,Start=Time.time,Color=FailMark,Size=target.Width*.78f});
            }
            hud?.Refresh();
            return any;
        }
        public void HoldSelection(){Order(MatchCommand.Hold(Me,selection.Orderable));SetMode(MatchMode.None);}
        public void Stop(){Order(MatchCommand.Stop(Me,selection.Orderable));SetMode(MatchMode.None);}
        // Order from the map or minimap in the current mode; without a mode it is a move/harvest order.
        public void OrderTo(Vector2 point)
        {
            if(Mode==MatchMode.Ping){AddPin(point);return;}
            if(Mode==MatchMode.Build){bool queue=Queueing();if(Build((int)point.x,(int)point.y,buildKind,queue)&&!queue)SetMode(MatchMode.None);return;}
            if(Mode==MatchMode.Attack){bool queue=Queueing();AttackMoveSelection(point,queue);if(!queue)SetMode(MatchMode.None);return;}
            // From the minimap only a building can be pointed at.
            if(Mode==MatchMode.Repair)
            {
                var target=Match.Buildings.Find(b=>b.Owner==PrototypeMatch.Player&&b.Ready>=1&&point.x>=b.X&&point.x<b.X+b.Width&&point.y>=b.Z&&point.y<b.Z+b.Width);
                if(target==null){Match.Message="Укажите своё здание.";hud?.Refresh();return;}
                if(RepairSelection(target,null))SetMode(MatchMode.None);
                return;
            }
            // With a building selected, a plain order from the minimap sets its rally point.
            if(Mode==MatchMode.Rally||(Mode==MatchMode.None&&!selection.Own&&selectedBuilding!=null&&selectedBuilding.Owner==PrototypeMatch.Player)){SetRallyPoint(point);SetMode(MatchMode.None);return;}
            if(Mode==MatchMode.Harvest)
            {
                var deposit=Match.Map.Inside((int)point.x,(int)point.y)&&Match.Explored[(int)point.x,(int)point.y]?Match.Map.ResourceAt((int)point.x,(int)point.y):null;
                if(deposit==null){Match.Message="Укажите разведанную залежь.";return;}
                if(!HasHarvester())SelectWorker();
                // Only workers go: the rest of the selection stays where it is.
                selection.Set(new List<PrototypeUnit>(selection.Units).FindAll(u=>u.Stats.HarvestAmount>0));
            }
            if(!selection.Own)SelectWorker();
            MoveSelection(point);SetMode(MatchMode.None);
        }
    }
}
