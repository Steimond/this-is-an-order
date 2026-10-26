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
        void UpdateSelectionRings()
        {
            int shown=0,hints=0;
            foreach(var unit in selection.Units)Ring(selectionRings,shown++,unit,unit.Owner==PrototypeMatch.Player?unitRingMaterial:enemyRingMaterial,1);
            foreach(var unit in hovered)if(!selection.Contains(unit))Ring(hoverRings,hints++,unit,unit.Owner==PrototypeMatch.Player?hoverRingMaterial:enemyHoverMaterial,1.06f);
            for(int i=shown;i<selectionRings.Count;i++)selectionRings[i].SetActive(false);
            for(int i=hints;i<hoverRings.Count;i++)hoverRings[i].SetActive(false);
        }
        void Ring(List<GameObject> pool,int index,PrototypeUnit unit,Material material,float scale)
        {
            while(pool.Count<=index)pool.Add(scenery.Model("Selection circle",selectionMesh,material,worldRoot.transform,false));
            var ring=pool[index];ring.SetActive(true);ring.GetComponent<MeshRenderer>().sharedMaterial=material;
            float y;Quaternion tilt;var at=Place(unit);
            if(unit.Stats.Flying&&unitViews.TryGetValue(unit.Id,out var view)){y=view.Body.position.y-.3f;tilt=Quaternion.identity;}
            else{y=(float)Match.HeightAt(at.x,at.y)+.03f;tilt=Quaternion.FromToRotation(Vector3.up,GroundNormal(at.x,at.y));}
            ring.transform.SetPositionAndRotation(new Vector3(at.x,y,at.y),tilt);
            ring.transform.localScale=Vector3.one*((float)unit.Stats.Radius+.16f)*scale;
        }
        // Slope under a point for circles and markers: ramps tilt them, a nearby cliff does not (samples across a drop count as level).
        Vector3 GroundNormal(float x,float z)
        {
            const float e=.3f;
            float H(float px,float pz)=>(float)Match.HeightAt(Mathf.Clamp(px,0,MapSize-.01f),Mathf.Clamp(pz,0,MapSize-.01f));
            float h=H(x,z);
            float S(float px,float pz){float s=H(px,pz);return Mathf.Abs(s-h)>.51f?h:s;}
            return new Vector3(S(x-e,z)-S(x+e,z),2*e,S(x,z-e)-S(x,z+e)).normalized;
        }
        // Click: a unit (Shift toggles, Ctrl or a double click takes all of its kind on screen); otherwise the ground cell
        // picks a building or an explored deposit, and an empty click clears the selection.
        void Click(Vector2 screen,bool shift,bool ctrl,bool ground)
        {
            var unit=UnitAt(screen);
            if(unit!=null)
            {
                bool twice=lastClickUnit!=null&&lastClickUnit.Kind==unit.Kind&&Time.unscaledTime-lastClickTime<.35f&&(screen-lastClickAt).magnitude<14;
                selection.Click(unit,shift,ctrl||twice,UnitsOnScreen());
                lastClickUnit=twice?null:unit;lastClickTime=Time.unscaledTime;lastClickAt=screen;
                selectedBuilding=null;selectedDeposit=null;
            }
            else if(!shift)
            {
                selection.Clear();lastClickUnit=null;selectedBuilding=null;selectedDeposit=null;
                if(ground)
                {
                    selectedBuilding=BuildingAt(screen)??Match.Buildings.Find(b=>BuildingShown(b)&&hoverX>=b.X&&hoverX<b.X+b.Width&&hoverZ>=b.Z&&hoverZ<b.Z+b.Width);
                    selectedDeposit=selectedBuilding!=null||!Match.Map.Inside(hoverX,hoverZ)||!Match.Explored[hoverX,hoverZ]?null:Match.Map.ResourceAt(hoverX,hoverZ);
                }
            }
            // The selected building turns white on the minimap at once, not at the next world refresh.
            RefreshMinimap();hud?.Refresh();
        }
        void BoxSelect(List<PrototypeUnit> inside,bool shift)
        {
            selection.Box(inside,shift);
            if(selection.Count>0||!shift){selectedBuilding=null;selectedDeposit=null;}
            hud?.Refresh();
        }
        // On screen a unit is a segment along its body: from the feet (or below a hovering body) to the top of the model.
        bool ScreenAxis(PrototypeUnit unit,out Vector2 a,out Vector2 b,out float reach)
        {
            a=b=default;reach=0;
            // Enemies hidden in the fog cannot be picked.
            if(!unitViews.TryGetValue(unit.Id,out var view)||!view.Root.activeSelf)return false;
            var model=view.Model;var foot=view.Root.transform.position;
            var low=model.Hover>0?view.Body.position+Vector3.down*.25f:foot+Vector3.up*.1f*model.Scale;
            var high=model.Hover>0?view.Body.position+Vector3.up*.25f:foot+Vector3.up*model.Height*model.Scale;
            Vector3 sa=MapCamera.WorldToScreenPoint(low),sb=MapCamera.WorldToScreenPoint(high);if(sa.z<=0||sb.z<=0)return false;
            a=sa;b=sb;reach=Vector2.Distance(a,MapCamera.WorldToScreenPoint(low+MapCamera.transform.right*((float)unit.Stats.Radius*1.2f+.12f)));
            return true;
        }
        PrototypeUnit UnitAt(Vector2 screen)
        {
            PrototypeUnit best=null;float bestDistance=float.MaxValue;
            foreach(var unit in Match.Units)
            {
                if(!ScreenAxis(unit,out var a,out var b,out float reach))continue;
                Vector2 ab=b-a;float t=ab.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(screen-a,ab)/ab.sqrMagnitude):0;
                float distance=Vector2.Distance(screen,a+ab*t);
                if(distance<reach&&distance<bestDistance){best=unit;bestDistance=distance;}
            }
            return best;
        }
        static bool Queueing()=>Keyboard.current!=null&&Keyboard.current.shiftKey.isPressed;
        // With Shift held, the selected units' orders show as lines through their waypoints, as in StarCraft: green while all
        // are moves, orange when the queue holds an attack; queued points get a small circle of their own colour.
        void UpdateWaypoints()
        {
            int lines=0,dots=0;
            if(Queueing()&&selection.Own)
                foreach(var unit in selection.Units)
                {
                    if(unit.Queued.Count==0)continue;
                    Vector3 G(float x,float z)=>new Vector3(x,(float)Match.HeightAt(Mathf.Clamp(x,0,MapSize-.01f),Mathf.Clamp(z,0,MapSize-.01f))+.1f,z);
                    var from=Place(unit);waypointPoints.Clear();waypointPoints.Add(G(from.x,from.y));
                    bool any=false;float lx=0,lz=0;foreach(var p in unit.Route){lx=(float)p.X;lz=(float)p.Z;any=true;}
                    if(any)waypointPoints.Add(G(lx,lz));
                    bool attack=unit.Order==UnitOrder.AttackMove||unit.Order==UnitOrder.Attack;
                    foreach(var order in unit.Queued)
                    {
                        float x=order.Unit!=null?(float)order.Unit.X:(float)order.X,z=order.Unit!=null?(float)order.Unit.Z:(float)order.Z;
                        var at=G(x,z);waypointPoints.Add(at);
                        bool hit=order.Order!=UnitOrder.Move;attack|=hit;
                        while(waypointDots.Count<=dots)waypointDots.Add(scenery.Model("Waypoint",selectionMesh,markerMaterial,worldRoot.transform,false));
                        var dot=waypointDots[dots++];dot.SetActive(true);dot.transform.SetPositionAndRotation(at,Quaternion.identity);dot.transform.localScale=Vector3.one*.28f;
                        markerColor.SetColor("_Color",hit?AttackMark:MoveMark);dot.GetComponent<MeshRenderer>().SetPropertyBlock(markerColor);
                    }
                    while(waypointLines.Count<=lines)
                    {
                        var line=new GameObject("Waypoint line",typeof(LineRenderer)).GetComponent<LineRenderer>();line.transform.SetParent(worldRoot.transform,false);
                        line.sharedMaterial=markerMaterial;line.widthMultiplier=.045f;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                        waypointLines.Add(line);
                    }
                    var path=waypointLines[lines++];path.enabled=true;path.positionCount=waypointPoints.Count;path.SetPositions(waypointPoints.ToArray());
                    path.startColor=path.endColor=attack?new Color(1,.45f,.2f,.7f):new Color(.35f,1,.5f,.6f);
                }
            for(int i=lines;i<waypointLines.Count;i++)waypointLines[i].enabled=false;
            for(int i=dots;i<waypointDots.Count;i++)waypointDots[i].SetActive(false);
        }
        PrototypeUnit EnemyAt(Vector2 screen){var unit=UnitAt(screen);return unit!=null&&unit.Owner!=PrototypeMatch.Player?unit:null;}
        PrototypeBuilding EnemyBuildingAt(Vector2 screen){var b=BuildingAt(screen);return b!=null&&b.Owner!=PrototypeMatch.Player&&Match.SeenBy(PrototypeMatch.Player,b)?b:null;}
        // An own building under construction under the cursor: its model, or its footprint while it is still low.
        PrototypeBuilding SiteAt(Vector2 screen)
        {
            var b=BuildingAt(screen)??Match.Buildings.Find(s=>hoverX>=s.X&&hoverX<s.X+s.Width&&hoverZ>=s.Z&&hoverZ<s.Z+s.Width);
            return b!=null&&b.Owner==PrototypeMatch.Player&&b.Ready<1?b:null;
        }
        // A unit is inside the box when any of its feet, middle or top is.
        void UnitsIn(Rect box,List<PrototypeUnit> result)
        {
            foreach(var unit in Match.Units)
                if(ScreenAxis(unit,out var a,out var b,out _)&&(box.Contains(a)||box.Contains(b)||box.Contains((a+b)*.5f)))result.Add(unit);
        }
        List<PrototypeUnit> UnitsOnScreen()
        {
            var result=new List<PrototypeUnit>();UnitsIn(new Rect(0,0,Screen.width,Screen.height),result);return result;
        }
        // 1–0 recall groups (twice in a row: camera to the group), Ctrl+digit assigns, Shift+digit adds;
        // F1 picks the next idle worker, F2 the whole army, as in StarCraft.
        void SelectionKeys(Keyboard keys)
        {
            bool ctrl=keys.ctrlKey.isPressed,shift=keys.shiftKey.isPressed;
            for(int i=0;i<Digits.Length;i++)
            {
                if(!keys[Digits[i]].wasPressedThisFrame)continue;
                string label=i==9?"0":(i+1).ToString();
                if(ctrl){selection.Assign(i);Match.Message=selection.Count>0?$"Группа {label}: {selection.Count}.":$"Группа {label} очищена.";hud?.Refresh();}
                else if(shift){selection.AddToGroup(i);Match.Message=$"Группа {label}: {selection.GroupSize(i,Match.Units)}.";hud?.Refresh();}
                else RecallGroup(i);
            }
            if(keys.f1Key.wasPressedThisFrame)SelectIdleWorker();
            if(keys.f2Key.wasPressedThisFrame)SelectArmy();
        }
        public void RecallGroup(int index)
        {
            if(!selection.Recall(index,Match.Units))return;
            selectedBuilding=null;selectedDeposit=null;
            if(lastGroup==index&&Time.unscaledTime-lastGroupTime<.4f)FocusSelection();
            lastGroup=index;lastGroupTime=Time.unscaledTime;
            hud?.Refresh();
        }
        public void SelectArmy()=>SelectUnits(u=>u.Kind!=UnitKind.Worker);
        public void SelectKind(UnitKind kind)=>SelectUnits(u=>u.Kind==kind);
        void SelectUnits(Predicate<PrototypeUnit> filter)
        {
            var units=Match.Units.FindAll(u=>u.Owner==PrototypeMatch.Player&&filter(u));if(units.Count==0)return;
            selection.Set(units);selectedBuilding=null;selectedDeposit=null;hud?.Refresh();
        }
        // The next worker with no order after the one selected now; the camera follows it.
        void SelectIdleWorker()
        {
            var idle=Match.Units.FindAll(u=>u.Owner==PrototypeMatch.Player&&u.Kind==UnitKind.Worker&&!u.Moving&&u.HarvestTarget==null&&u.Constructing==null&&u.SiteKind<0);
            if(idle.Count==0){Match.Message="Нет свободных рабочих.";hud?.Refresh();return;}
            int current=selection.Count==1?idle.IndexOf(selection.Units[0]):-1;
            selection.Set(new[]{idle[(current+1)%idle.Count]});selectedBuilding=null;selectedDeposit=null;
            FocusSelection();hud?.Refresh();
        }
        public void SelectWorker()
        {
            var worker=Match.Units.Find(u=>u.Owner==PrototypeMatch.Player&&u.Kind==UnitKind.Worker);
            if(worker!=null)selection.Set(new[]{worker});else selection.Clear();
            selectedBuilding=null;selectedDeposit=null;hud?.Refresh();
        }
    }
}
