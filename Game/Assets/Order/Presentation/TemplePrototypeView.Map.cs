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
        // Terrain, fog, deposits and buildings; the camera frame, worker, route and pins are vector overlays in the HUD.
        void RefreshMinimap()
        {
            const int t=TempleScenery.FogTexels;int n=Match.Map.Size*t;
            var pixels=new Color[n*n];
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                float lit=Match.Visible[x/t,z/t]?1:Match.Explored[x/t,z/t]?.32f:.05f;
                var c=miniBase[z*n+x]*lit;c.a=1;pixels[z*n+x]=c;
            }
            void Cell(int cx,int cz,Color color){for(int z=cz*t;z<cz*t+t;z++)for(int x=cx*t;x<cx*t+t;x++)pixels[z*n+x]=color;}
            foreach(var r in Match.Map.Resources)if(Match.Explored[r.X,r.Z]&&r.Remaining>0)Cell(r.X,r.Z,(r.Plasma?MinimapPanel.Plasma:MinimapPanel.Metal)*(Match.Visible[r.X,r.Z]?1:.4f));
            foreach(var b in Match.Buildings)
            {
                if(!BuildingShown(b))continue;
                var color=(b==selectedBuilding?MinimapPanel.Selected:b.Owner==PrototypeMatch.Player?MinimapPanel.Own:MinimapPanel.Enemy)*(b.Ready>=1?1:.6f);color.a=1;
                for(int z=b.Z;z<b.Z+b.Width;z++)for(int x=b.X;x<b.X+b.Width;x++)Cell(x,z,color);
            }
            miniTexture.SetPixels(pixels);miniTexture.Apply(false);
        }
        public void AddPin(Vector2 point)
        {
            if(pins.Count>=9){pins.RemoveAt(0);Destroy(pinViews[0]);pinViews.RemoveAt(0);}
            pins.Add(point);
            var height=(float)Match.HeightAt(Mathf.Clamp(point.x,0,MapSize-.1f),Mathf.Clamp(point.y,0,MapSize-.1f));
            var beacon=scenery.Model("Pin",beaconMesh,beaconMaterial,worldRoot.transform,false);
            beacon.transform.position=new Vector3(point.x,height,point.y);
            pinViews.Add(beacon);
            if(Mode==MatchMode.Ping)SetMode(MatchMode.None);
            Match.Message=$"Метка {pins.Count}: {MapGrid.Name(point.x,point.y,Match.Map.Size)}.";
            hud?.Refresh();
        }
        public void ClearPins()
        {
            foreach(var view in pinViews)if(view!=null)Destroy(view);
            pinViews.Clear();pins.Clear();hud?.Refresh();
        }
        // An attack the player cannot see on screen: a line in the event log with the grid square, and a ping on the minimap.
        void OnUnderAttack(Fix fx,Fix fz,bool building)
        {
            float x=(float)fx,z=(float)fz;
            var frame=ViewportOnGround();var point=new Vector2(x,z);
            if(frame!=null&&Inside(frame,point))return;
            alerts.RemoveAll(a=>Time.unscaledTime-a.time>AlertSeconds);
            alerts.Add((point,Time.unscaledTime));
            Match.Message=(building?"Атакована база: ":"Наши войска под атакой: ")+MapGrid.Name(x,z,Match.Map.Size)+".";
            hud?.Refresh();
        }
    }
}
