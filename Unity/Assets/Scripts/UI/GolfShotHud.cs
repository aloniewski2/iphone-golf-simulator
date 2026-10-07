using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// Small shared broadcast vocabulary: distance, club, wind, and matching power/landing colours.
    /// The map positions come from GolfGame's actual launch simulation, never screen estimates.
    public sealed class GolfShotHud : MonoBehaviour
    {
        public static readonly Vector2 MapSize = new Vector2(264,324);
        public static readonly Color Ink = new Color(.025f,.065f,.105f,.92f);
        public static readonly Color Mint = new Color(.77f,.98f,.43f,1);
        public static readonly Color[] Stops = { new(.52f,.85f,1), new(.5f,.95f,.78f), new(1,.88f,.42f), new(1,.61f,.36f) };
        Camera view; Canvas canvas; CanvasScaler scaler;
        Text hole, distance, club, wind, contact, liveYards;
        RawImage mapImage; RectTransform mapRoot, meterRoot, tip;
        RectTransform ball, pin, live;
        readonly RectTransform[] targets = new RectTransform[4];
        readonly RectTransform[] segments = new RectTransform[4];
        const float MeterHeight = 288, SegmentHeight = 69;
        bool flight;
        public static GolfShotHud Create(Camera camera)
        {
            var go = new GameObject("Golf broadcast",typeof(RectTransform));
            var h=go.AddComponent<GolfShotHud>();h.view=camera;
            h.canvas=UiKit.Canvas(go);h.canvas.renderMode=RenderMode.ScreenSpaceCamera;h.canvas.worldCamera=camera;h.canvas.planeDistance=.6f;h.canvas.sortingOrder=35;
            h.scaler=go.GetComponent<CanvasScaler>();h.scaler.referenceResolution=new Vector2(1920,1080);h.scaler.matchWidthOrHeight=1;
            var info=Panel(go.transform,"Shot info",new Vector2(0,1),new Vector2(40,-40),new Vector2(300,216));
            h.hole=Label(info,"Hole",22,new Vector2(24,-24),new Vector2(250,28));
            h.distance=Label(info,"Distance",46,new Vector2(24,-64),new Vector2(260,56));
            h.club=Label(info,"Club",24,new Vector2(24,-126),new Vector2(250,32));h.club.color=Mint;
            h.wind=Label(info,"Wind",22,new Vector2(24,-174),new Vector2(250,28));
            h.mapRoot=Panel(go.transform,"Hole map",new Vector2(1,1),new Vector2(-40,-40),new Vector2(284,382));
            var title=Label(h.mapRoot,"Map title",20,new Vector2(18,-14),new Vector2(250,28));title.text="ESTIMATED LANDING";
            var mapGo=new GameObject("Course",typeof(RectTransform),typeof(RawImage));mapGo.transform.SetParent(h.mapRoot,false);
            h.mapImage=mapGo.GetComponent<RawImage>();h.mapImage.raycastTarget=false;
            var rt=h.mapImage.rectTransform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=new Vector2(10,10);rt.offsetMax=new Vector2(-10,-48);
            for(int i=0;i<4;i++)h.targets[i]=Mark(rt,"Landing "+(i+1),Stops[i],30);
            h.pin=Mark(rt,"Pin",new Color(1,.3f,.32f),16);
            h.ball=Mark(rt,"Ball",Color.white,16);
            h.live=Mark(rt,"Live landing",Mint,22);
            h.meterRoot=new GameObject("Power",typeof(RectTransform)).GetComponent<RectTransform>();h.meterRoot.SetParent(go.transform,false);
            // Float beside the golfer instead of grouping the meter with the minimap.
            h.meterRoot.anchorMin=h.meterRoot.anchorMax=new Vector2(.235f,.31f);
            h.meterRoot.pivot=new Vector2(.5f,0);h.meterRoot.sizeDelta=new Vector2(52,MeterHeight);
            var frame=UiKit.Panel(h.meterRoot,"Meter frame",new Color(.025f,.065f,.105f,.9f),Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,false);
            frame.rectTransform.offsetMin=new Vector2(-4,-4);frame.rectTransform.offsetMax=new Vector2(4,4);frame.raycastTarget=false;
            var powerTitle=Label(h.meterRoot,"Power title",18,new Vector2(-12,32),new Vector2(90,24));powerTitle.text="POWER";
            for(int i=0;i<4;i++){
                var section=UiKit.Panel(h.meterRoot,"Power section "+(i+1),new Color(Stops[i].r,Stops[i].g,Stops[i].b,.23f),Vector2.zero,Vector2.zero,new Vector2(0,i*72),new Vector2(52,SegmentHeight),false);
                section.raycastTarget=false;
                var level=UiKit.Panel(section.transform,"Fill",Stops[i],Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(52,0),false);
                level.raycastTarget=false;h.segments[i]=level.rectTransform;
            }
            var cursor=UiKit.Panel(h.meterRoot,"Current power",Color.white,Vector2.zero,Vector2.zero,new Vector2(-6,0),new Vector2(64,3),false);
            cursor.raycastTarget=false;h.tip=cursor.rectTransform;
            h.liveYards=Label(h.meterRoot,"Estimate",22,new Vector2(-22,-MeterHeight-16),new Vector2(100,32));h.liveYards.alignment=TextAnchor.UpperCenter;h.liveYards.color=Mint;
            var status=Panel(go.transform,"Contact",new Vector2(.5f,1),new Vector2(0,-40),new Vector2(340,64));
            h.contact=UiKit.Label(status,"Quality",26,TextAnchor.MiddleCenter,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,UiKit.Display,false);
            status.gameObject.SetActive(false);
            return h;
        }
        public static RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 pos,Vector2 size)
        {
            var r=UiKit.Panel(parent,name,Ink,anchor,anchor,pos,size).rectTransform;r.pivot=anchor;return r;
        }
        public static Text Label(Transform parent,string name,int size,Vector2 pos,Vector2 box)
        {
            var t=UiKit.Label(parent,name,size,TextAnchor.UpperLeft,new Vector2(0,1),new Vector2(0,1),pos,box,UiKit.Strong,false);t.rectTransform.pivot=new Vector2(0,1);t.color=Color.white;return t;
        }
        static RectTransform Mark(Transform parent,string name,Color colour,float size)
        {
            var rim=UiKit.Panel(parent,name,colour,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(size,size),false);rim.sprite=UiKit.Circle;rim.raycastTarget=false;
            var face=UiKit.Panel(rim.transform,"Golf ball",Color.white,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,false);face.sprite=UiKit.Circle;face.rectTransform.offsetMin=Vector2.one*4;face.rectTransform.offsetMax=-Vector2.one*4;face.raycastTarget=false;
            // A few soft dimples keep the markers recognisable as golf balls at HUD size.
            if(size>=28)foreach(var offset in new[]{new Vector2(-4,3),new Vector2(4,3),new Vector2(0,-4)}){
                var d=UiKit.Panel(face.transform,"Dimple",new Color(.56f,.64f,.67f,.4f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),offset,new Vector2(3,3),false);
                d.sprite=UiKit.Circle;d.raycastTarget=false;
            }
            return rim.rectTransform;
        }
        void LateUpdate(){canvas.targetDisplay=view.targetDisplay;scaler.referenceResolution=view.aspect<1.2f?new Vector2(1080,1920):new Vector2(1920,1080);scaler.matchWidthOrHeight=view.aspect<1.2f?0:1;meterRoot.anchorMin=meterRoot.anchorMax=new Vector2(view.aspect<1.2f?.12f:.235f,.31f);}
        public void SetHole(int n,int par)=>hole.text=$"HOLE {n:00}  ·  PAR {par}";
        public void SetDistance(double n,string unit)=>distance.text=$"{n:F0} <size=24>{unit.ToLowerInvariant()} TO PIN</size>";
        public void SetClub(string name)=>club.text=name;
        public void SetWind(float angle,double mph,bool calm){wind.text=calm?"WIND  ·  CALM":$"WIND  {mph:F0} mph  ↗";if(!calm)wind.text=$"WIND  {mph:F0} mph  {Direction(angle)}";}
        static string Direction(float degrees){var dirs=new[]{"↑","↗","→","↘","↓","↙","←","↖"};return dirs[Mathf.RoundToInt(Mathf.Repeat(degrees,360)/45)%8];}
        public void SetFlight(bool on){flight=on;meterRoot.gameObject.SetActive(!on);contact.transform.parent.gameObject.SetActive(on);}
        public void SetContact(string quality)=>contact.text=quality.TrimEnd('!')+" CONTACT";
        public void SetPower(float load,string yards){
            load=Mathf.Clamp01(load);
            for(int i=0;i<segments.Length;i++)segments[i].sizeDelta=new Vector2(52,SegmentHeight*Mathf.Clamp01(load*4-i));
            tip.anchoredPosition=new Vector2(-6,MeterHeight*load);
            liveYards.text=string.IsNullOrEmpty(yards)?"— yd":yards;
        }
        public void Draw(Camera map,Texture texture,Hud.MapPlan plan)
        {
            mapImage.texture=texture;
            void Place(RectTransform t,Vector3 world,bool show){var v=map.WorldToViewportPoint(world);t.gameObject.SetActive(show&&v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1);t.anchorMin=t.anchorMax=new Vector2(v.x,v.y);t.anchoredPosition=Vector2.zero;}
            Place(ball,plan.Ball,plan.ShowBall);Place(pin,plan.Pin,true);Place(live,plan.Load,plan.ShowLoad&&!flight);
            for(int i=0;i<targets.Length;i++)Place(targets[i],i<plan.Targets.Count?plan.Targets[i]:Vector3.zero,!flight&&i<plan.Targets.Count);
        }
    }
}
