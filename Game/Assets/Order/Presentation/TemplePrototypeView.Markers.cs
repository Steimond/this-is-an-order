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
        // Order marker on the ground: arrows converge on the point and fade; orange for an attack-move, red when no unit
        // could take the order. An attacked unit flashes a red circle that closes in on it.
        sealed class Marker{public GameObject View;public float Start,Size;public Color Color;public PrototypeUnit Unit;}
        static readonly Color MoveMark=new Color(.35f,1.6f,.5f,1),AttackMark=new Color(1.9f,.55f,.2f,1),FailMark=new Color(1.6f,.3f,.25f,1);
        void Mark(Vector2 point,bool ok,bool attack=false)
        {
            var view=scenery.Model("Order marker",markerMesh,markerMaterial,worldRoot.transform,false);
            float x=Mathf.Clamp(point.x,0,MapSize-.01f),z=Mathf.Clamp(point.y,0,MapSize-.01f);
            view.transform.SetPositionAndRotation(new Vector3(point.x,(float)Match.HeightAt(x,z)+.04f,point.y),Quaternion.FromToRotation(Vector3.up,GroundNormal(x,z)));
            markers.Add(new Marker{View=view,Start=Time.time,Color=!ok?FailMark:attack?AttackMark:MoveMark});
        }
        void MarkTarget(PrototypeUnit unit)
        {
            var view=scenery.Model("Target marker",selectionMesh,markerMaterial,worldRoot.transform,false);
            markers.Add(new Marker{View=view,Start=Time.time,Color=FailMark,Unit=unit});
        }
        void AnimateMarkers()
        {
            for(int i=markers.Count-1;i>=0;i--)
            {
                var marker=markers[i];float t=(Time.time-marker.Start)/.6f;
                if(t>=1||(marker.Unit!=null&&!marker.Unit.Alive)){Destroy(marker.View);markers.RemoveAt(i);continue;}
                if(marker.Unit!=null)
                {
                    var unit=marker.Unit;var at=Place(unit);float y=unit.Stats.Flying&&unitViews.TryGetValue(unit.Id,out var view)?view.Body.position.y-.3f:(float)Match.HeightAt(at.x,at.y)+.05f;
                    marker.View.transform.position=new Vector3(at.x,y,at.y);
                    marker.View.transform.localScale=Vector3.one*((float)unit.Stats.Radius+.16f)*Mathf.Lerp(1.9f,1.05f,Mathf.Sqrt(t));
                }
                else if(marker.Size>0)marker.View.transform.localScale=Vector3.one*marker.Size*Mathf.Lerp(1.35f,1,Mathf.Sqrt(t));
                else marker.View.transform.localScale=Vector3.one*Mathf.Lerp(1.3f,.55f,Mathf.Sqrt(t));
                var color=marker.Color;color.a=1-Mathf.SmoothStep(.35f,1,t);
                markerColor.SetColor("_Color",color);marker.View.GetComponent<MeshRenderer>().SetPropertyBlock(markerColor);
            }
        }
        // Soft dark disc (uv 0–1) under air units.
        static Mesh SoftQuad()
        {
            var mesh=new Mesh{name="Soft quad"};
            mesh.SetVertices(new[]{new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)});
            mesh.SetUVs(0,new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)});
            mesh.SetColors(new[]{Color.white,Color.white,Color.white,Color.white});
            mesh.SetTriangles(new[]{0,1,2,0,2,3},0);mesh.RecalculateBounds();
            return mesh;
        }
        void UpdateFocusRing()
        {
            Vector3 position=default;float radius=0;
            if(selectedBuilding!=null)
            {
                var b=selectedBuilding;position=new Vector3((float)b.X+b.Width*.5f,(float)Match.Map.Height(b.X,b.Z),(float)b.Z+b.Width*.5f);radius=b.Width*.76f;
                focusRing.GetComponent<MeshRenderer>().sharedMaterial=b.Owner==PrototypeMatch.Player?selectedMaterial:enemyRingMaterial;
            }
            else if(selectedDeposit!=null&&selectedDeposit.Remaining>0)
            {
                position=new Vector3((float)selectedDeposit.X+.5f,(float)Match.Map.Height(selectedDeposit.X,selectedDeposit.Z),(float)selectedDeposit.Z+.5f);radius=.68f;
                focusRing.GetComponent<MeshRenderer>().sharedMaterial=neutralMaterial;
            }
            focusRing.SetActive(radius>0);
            UpdateRally();
            if(radius>0){focusRing.transform.position=position;focusRing.transform.localScale=Vector3.one*radius*(1+.025f*Mathf.Sin(Time.time*5));}
        }
        // The selected building's rally point: a small flag and a line from the building along the ground.
        void UpdateRally()
        {
            var b=selectedBuilding;bool show=b!=null&&b.HasRally&&b.Owner==PrototypeMatch.Player;
            rallyFlag.SetActive(show);rallyLine.enabled=show;
            if(!show)return;
            float H(float x,float z)=>(float)Match.HeightAt(Mathf.Clamp(x,0,MapSize-.01f),Mathf.Clamp(z,0,MapSize-.01f))+.12f;
            var from=new Vector2((float)b.X+b.Width*.5f,(float)b.Z+b.Width*.5f);var to=new Vector2((float)b.RallyX,(float)b.RallyZ);
            int count=Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(from,to)/.5f)+1,2,160);
            rallyLine.positionCount=count;
            for(int i=0;i<count;i++){var p=Vector2.Lerp(from,to,i/(count-1f));rallyLine.SetPosition(i,new Vector3(p.x,H(p.x,p.y),p.y));}
            rallyFlag.transform.position=new Vector3(to.x,H(to.x,to.y)-.12f,to.y);
        }
    }
}
