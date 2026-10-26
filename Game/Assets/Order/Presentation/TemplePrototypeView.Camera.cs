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
        // Selected units get a green circle, the hovered unit (or units under the selection box) a faint one.
        // Circles follow the slope of a ramp; an air unit's circle hangs just under its body, so it stays next to it on screen.
        // Map point under the cursor. A click on a cliff face means the ground at its foot, as in StarCraft: the point moves
        // off the wall to the side it faces, so orders, markers and pins never land on the wall itself.
        bool GroundPoint(Vector2 screen,out Vector2 point)
        {
            point=default;
            if(!Physics.Raycast(MapCamera.ScreenPointToRay(screen),out var hit,180))return false;
            var at=hit.point;
            // Past the sloped foot and plinth of the wall (about 0.3), with room for a unit to stand.
            if(hit.normal.y<.5f){var away=new Vector3(hit.normal.x,0,hit.normal.z).normalized;at+=away*.55f;}
            point=new Vector2(at.x,at.z);
            return true;
        }
        // Zooming in lowers the camera toward the horizon, as in StarCraft; it follows the terrain height smoothly.
        // The focus sits in the middle of the view left free by the HUD deck, not in the middle of the screen.
        void UpdateCamera()
        {
            MapCamera.transform.rotation=Quaternion.Euler(Mathf.Lerp(46,57,Mathf.InverseLerp(MinZoom,MaxZoom,zoom)),0,0);
            var target=new Vector3(focus.x,focusHeight,focus.z);
            MapCamera.transform.position=target-MapCamera.transform.forward*(zoom/Mathf.Tan(MapCamera.fieldOfView*Mathf.Deg2Rad*.5f));
            if(new Plane(Vector3.up,target).Raycast(MapCamera.ViewportPointToRay(new Vector3(.5f,.59f,0)),out float d))
            {var seen=MapCamera.ViewportPointToRay(new Vector3(.5f,.59f,0)).GetPoint(d);MapCamera.transform.position+=new Vector3(target.x-seen.x,0,target.z-seen.z);}
        }
        public void Focus(Vector2 point)=>focus=new Vector3(Mathf.Clamp(point.x,0,MapSize),0,Mathf.Clamp(point.y,0,MapSize));
        // The camera moved at once, from this distance (the visual lab's presets).
        public void LookAt(Vector2 point,float distance){Focus(point);zoom=targetZoom=Mathf.Clamp(distance,MinZoom,MaxZoom);focusHeight=(float)Match.HeightAt(focus.x,focus.z);}
        public Vector2 CameraFocus=>new Vector2(focus.x,focus.z);
        public void FocusWorker(){var worker=Match.Units.Find(u=>u.Owner==PrototypeMatch.Player&&u.Kind==UnitKind.Worker);if(worker!=null)focus=new Vector3((float)worker.X,0,(float)worker.Z);}
        // Camera to the middle of the selected units; without a selection, to the worker.
        public void FocusSelection()
        {
            if(selection.Count==0){FocusWorker();return;}
            float x=0,z=0;foreach(var u in selection.Units){x+=(float)u.X;z+=(float)u.Z;}
            focus=new Vector3(x/selection.Count,0,z/selection.Count);
        }
        public void FocusBase()
        {
            var hq=Match.Buildings.Find(b=>b.Kind==2);
            if(hq!=null)focus=new Vector3(hq.X+hq.Width*.5f,0,hq.Z+hq.Width*.5f);
        }
        static bool Inside(Vector2[] polygon,Vector2 p)
        {
            bool inside=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
                if((polygon[i].y>p.y)!=(polygon[j].y>p.y)&&p.x<(polygon[j].x-polygon[i].x)*(p.y-polygon[i].y)/(polygon[j].y-polygon[i].y)+polygon[i].x)inside=!inside;
            return inside;
        }
        // Camera view projected onto the ground plane, as (x, z) points for the minimap frame.
        public Vector2[] ViewportOnGround()
        {
            if(MapCamera==null)return null;
            var result=new Vector2[4];Vector2[] corners={new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
            // Project onto the terrain level under the camera focus, not y=0, so the frame matches what is on screen.
            var plane=new Plane(Vector3.up,new Vector3(0,(float)Match.HeightAt(Mathf.Clamp(focus.x,0,MapSize-.1f),Mathf.Clamp(focus.z,0,MapSize-.1f)),0));
            for(int i=0;i<4;i++){var ray=MapCamera.ViewportPointToRay(corners[i]);if(!plane.Raycast(ray,out float d))return null;var p=ray.GetPoint(d);result[i]=new Vector2(p.x,p.z);}
            return result;
        }
    }
}
