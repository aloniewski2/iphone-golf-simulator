using GolfArcade.Course;
using GolfArcade.Shot;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// Small shared broadcast vocabulary: distance, club, wind, and matching power/landing colours.
    /// The map positions come from GolfGame's actual launch simulation, never screen estimates.
    /// The wind is an arrow (which way it blows, against the aim: up the screen is helping) and
    /// its speed; the lie has its own chip under the shot info — what the ball sits in and what
    /// that costs the club in hand.
    public sealed class GolfShotHud : MonoBehaviour
    {
        public static readonly Vector2 MapSize = new Vector2(360,440);
        public static readonly Color Ink = UiKit.Hex("10243D", .96f);
        public static readonly Color Mint = UiKit.Hex("D7F044");
        public static readonly Color[] Stops = { new(.52f,.85f,1), new(.5f,.95f,.78f), new(1,.88f,.42f), new(1,.61f,.36f) };
        Camera view; Canvas canvas; CanvasScaler scaler;
        Text hole, distance, club, wind, contact, liveYards, clubChange, lieName, lieCost;
        Text[] clubOptions = new Text[3];
        RectTransform clubRoot, windArrow, lieRoot;
        Image lieSwatch; Image[] lieMarks = new Image[3];
        GolfMapPath projection;
        GolfClub? selectedClub; float clubChangedAt;
        RawImage mapImage; RectTransform infoRoot, mapRoot, meterRoot, contactRoot, tip;
        RectTransform ball, pin, live;
        readonly RectTransform[] targets = new RectTransform[4];
        readonly RectTransform[] segments = new RectTransform[4];
        const float MeterHeight = 288, SegmentHeight = 69, InfoHeight = 216, LieHeight = 100;
        bool flight, lieSet;
        public static GolfShotHud Create(Camera camera)
        {
            var go = new GameObject("Golf broadcast",typeof(RectTransform));
            var h=go.AddComponent<GolfShotHud>();h.view=camera;
            h.canvas=UiKit.Canvas(go);h.canvas.renderMode=RenderMode.ScreenSpaceCamera;h.canvas.worldCamera=camera;h.canvas.planeDistance=.6f;h.canvas.sortingOrder=35;
            h.scaler=go.GetComponent<CanvasScaler>();h.scaler.referenceResolution=new Vector2(1920,1080);h.scaler.matchWidthOrHeight=1;
            var info=Panel(go.transform,"Shot info",new Vector2(0,1),new Vector2(40,-40),new Vector2(300,InfoHeight));
            h.infoRoot=info;
            h.hole=Label(info,"Hole",22,new Vector2(24,-24),new Vector2(250,28));
            h.distance=Label(info,"Distance",46,new Vector2(24,-64),new Vector2(260,56));
            h.club=Label(info,"Club",24,new Vector2(24,-126),new Vector2(250,32));h.club.color=Mint;
            // the wind: an arrow turned the way it blows, then its speed and how strong that is
            var arrow=UiKit.Panel(info,"Wind arrow",Color.white,new Vector2(0,1),new Vector2(0,1),new Vector2(41,-186),new Vector2(32,32),false);
            arrow.sprite=Arrow;arrow.raycastTarget=false;h.windArrow=arrow.rectTransform;h.windArrow.pivot=new Vector2(.5f,.5f);
            h.wind=Label(info,"Wind",22,new Vector2(66,-170),new Vector2(222,34));h.wind.supportRichText=true;
            // the lie: a swatch of what the ball sits in, its name, and what it costs
            var lie=Panel(go.transform,"Lie",new Vector2(0,1),new Vector2(40,-40-InfoHeight-12),new Vector2(300,LieHeight));
            h.lieRoot=lie;
            h.lieSwatch=UiKit.Panel(lie,"Swatch",Color.white,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(20,0),new Vector2(52,52),false);
            h.lieSwatch.sprite=UiKit.Circle;h.lieSwatch.raycastTarget=false;
            h.lieSwatch.gameObject.AddComponent<Mask>().showMaskGraphic=true;   // the marks stay inside the disc
            for(int i=0;i<h.lieMarks.Length;i++){
                h.lieMarks[i]=UiKit.Panel(h.lieSwatch.transform,"Mark",Color.white,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,Vector2.one,false);
                h.lieMarks[i].raycastTarget=false;
            }
            h.lieName=Label(lie,"Lie name",26,new Vector2(86,-12),new Vector2(204,32));
            h.lieCost=Label(lie,"Lie cost",16,new Vector2(86,-46),new Vector2(204,44));h.lieCost.color=new Color(1,1,1,.8f);
            h.lieCost.resizeTextForBestFit=true;h.lieCost.resizeTextMinSize=12;h.lieCost.resizeTextMaxSize=16;
            lie.gameObject.SetActive(false);
            h.mapRoot=Panel(go.transform,"Hole map",new Vector2(1,1),new Vector2(-40,-40),new Vector2(380,498));
            var title=Label(h.mapRoot,"Map title",20,new Vector2(18,-14),new Vector2(250,28));title.text="SHOT MAP";
            var mapGo=new GameObject("Course",typeof(RectTransform),typeof(RawImage));mapGo.transform.SetParent(h.mapRoot,false);
            h.mapImage=mapGo.GetComponent<RawImage>();h.mapImage.raycastTarget=false;
            var rt=h.mapImage.rectTransform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=new Vector2(10,10);rt.offsetMax=new Vector2(-10,-48);
            mapGo.AddComponent<RectMask2D>();
            var pathGo=new GameObject("Projected shot path",typeof(RectTransform),typeof(CanvasRenderer),typeof(GolfMapPath));pathGo.transform.SetParent(rt,false);
            h.projection=pathGo.GetComponent<GolfMapPath>();h.projection.raycastTarget=false;
            var pathRect=h.projection.rectTransform;pathRect.anchorMin=Vector2.zero;pathRect.anchorMax=Vector2.one;pathRect.offsetMin=pathRect.offsetMax=Vector2.zero;
            for(int i=0;i<4;i++){
                h.targets[i]=Mark(rt,"Landing "+(i+1),Stops[i],20);
                var badge=UiKit.Panel(h.targets[i],"Power label",Ink,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(i%2==0?46:-46,0),new Vector2(54,24),false);
                badge.raycastTarget=false;
                var power=UiKit.Label(badge.transform,"Power",18,TextAnchor.MiddleCenter,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,UiKit.Strong,false);
                power.text=$"{(i+1)*25}%";power.color=Stops[i];
            }
            h.clubRoot=Panel(go.transform,"Club selection",Vector2.zero,new Vector2(40,40),new Vector2(336,140));
            h.clubChange=Label(h.clubRoot,"Club change",19,new Vector2(18,-12),new Vector2(300,30));h.clubChange.text="SELECT CLUB";
            for(int i=0;i<3;i++){
                var slot=UiKit.Panel(h.clubRoot,"Club slot "+i,i==1?Mint:UiKit.Hex("284359"),Vector2.zero,Vector2.zero,new Vector2(12+i*106,12),new Vector2(100,82),false);slot.raycastTarget=false;
                h.clubOptions[i]=UiKit.Label(slot.transform,"Club name",18,TextAnchor.MiddleCenter,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,UiKit.Strong,false);
                h.clubOptions[i].color=i==1?Ink:Color.white;h.clubOptions[i].resizeTextForBestFit=true;h.clubOptions[i].resizeTextMinSize=14;h.clubOptions[i].resizeTextMaxSize=18;
            }
            h.pin=Mark(rt,"Pin",new Color(1,.3f,.32f),16);
            h.ball=Mark(rt,"Ball",Color.white,16);
            h.live=Mark(rt,"Live landing",Mint,22);
            h.meterRoot=new GameObject("Power",typeof(RectTransform)).GetComponent<RectTransform>();h.meterRoot.SetParent(go.transform,false);
            // The power gauge follows the reference at the left edge, beside the address silhouette.
            h.meterRoot.anchorMin=h.meterRoot.anchorMax=new Vector2(0,.29f);
            h.meterRoot.pivot=new Vector2(.5f,0);h.meterRoot.sizeDelta=new Vector2(52,MeterHeight);
            var frame=UiKit.Panel(h.meterRoot,"Meter frame",Ink,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            frame.rectTransform.offsetMin=new Vector2(-14,-48);frame.rectTransform.offsetMax=new Vector2(14,54);frame.raycastTarget=false;
            var powerTitle=Label(h.meterRoot,"Power title",18,new Vector2(-12,32),new Vector2(90,24));powerTitle.text="POWER";
            for(int i=0;i<4;i++){
                var section=UiKit.Panel(h.meterRoot,"Power section "+(i+1),UiKit.Hex("284359"),Vector2.zero,Vector2.zero,new Vector2(0,i*72),new Vector2(52,SegmentHeight),false);
                section.raycastTarget=false;
                // A thin colour key links each landing ball to its power band, even at rest.
                var key=UiKit.Panel(section.transform,"Landing colour",Stops[i],Vector2.zero,Vector2.zero,new Vector2(0,0),new Vector2(4,SegmentHeight),false);
                key.raycastTarget=false;
                var level=UiKit.Panel(section.transform,"Fill",Stops[i],Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(52,0),false);
                level.raycastTarget=false;h.segments[i]=level.rectTransform;
            }
            var cursor=UiKit.Panel(h.meterRoot,"Current power",Color.white,Vector2.zero,Vector2.zero,new Vector2(-6,0),new Vector2(64,3),false);
            cursor.raycastTarget=false;h.tip=cursor.rectTransform;
            h.liveYards=Label(h.meterRoot,"Estimate",20,new Vector2(-22,-MeterHeight-16),new Vector2(100,32));h.liveYards.alignment=TextAnchor.UpperCenter;h.liveYards.color=UiKit.Ink;
            var status=Panel(go.transform,"Contact",new Vector2(.5f,1),new Vector2(0,-40),new Vector2(340,64));
            h.contactRoot=status;
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
        void LateUpdate()=>RefreshLayout();
        // Also called by offscreen captures after assigning their render target. CanvasScaler's
        // next Update otherwise uses the editor window size for a portrait camera capture.
        public void RefreshLayout()
        {
            if (!view || !canvas) return;
            canvas.targetDisplay=view.targetDisplay;
            bool portrait=view.aspect<1.2f;
            float scale=Mathf.Max(.1f, portrait ? view.pixelWidth/1080f : view.pixelHeight/1080f);
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor=scale; canvas.scaleFactor=scale;
            float top=40, left=40, right=40;
            if (view.targetDisplay==0 && !view.targetTexture)
            {
                var safe=Screen.safeArea;
                top+=Mathf.Max(0,Screen.height-safe.yMax)/scale;
                left+=safe.xMin/scale;right+=Mathf.Max(0,Screen.width-safe.xMax)/scale;
            }
            infoRoot.anchoredPosition=new Vector2(left,-top);
            lieRoot.anchoredPosition=new Vector2(left,-top-InfoHeight-12);
            mapRoot.anchoredPosition=new Vector2(-right,-top);
            mapRoot.sizeDelta=portrait?new Vector2(344,454):new Vector2(380,498);
            meterRoot.anchorMin=meterRoot.anchorMax=new Vector2(0,portrait?.38f:.29f);
            meterRoot.anchoredPosition=new Vector2(left+38,0);
            clubRoot.anchoredPosition=new Vector2(left,portrait?250:40);
            if(selectedClub.HasValue && Time.unscaledTime-clubChangedAt>2.4f)clubChange.text="SELECT CLUB";
            contactRoot.anchoredPosition=new Vector2(0,-top-(portrait?232:0));
        }
        public void SetHole(int n,int par)=>hole.text=$"HOLE {n:00}  ·  PAR {par}";
        public void SetDistance(double n,string unit)=>distance.text=$"{n:F0} <size=24>{unit.ToLowerInvariant()} TO PIN</size>";
        public void SetClub(string name)=>club.text=name;
        public void SetClubSelection(GolfClub selected, bool announce=false)
        {
            if(selectedClub==selected)return;
            clubChange.text=announce&&selectedClub.HasValue?$"{selectedClub.Value.DisplayName()} → {selected.DisplayName()}":"SELECT CLUB";
            clubChangedAt=Time.unscaledTime;selectedClub=selected;
            int index=System.Array.IndexOf(GolfClubs.All,selected), count=GolfClubs.All.Length;
            for(int i=0;i<3;i++){
                var choice=GolfClubs.All[(index+i-1+count)%count];
                clubOptions[i].text=$"<size=28>{choice.Label()}</size>\n{choice.DisplayName()}";
            }
        }
        /// `angle` is where the wind blows toward, degrees right of the aim: the arrow points that way on the screen.
        public void SetWind(float angle,double mph,bool calm)
        {
            var w=new Wind(mph,0);
            var colour=calm||mph<Wind.ModerateMPH?Color.white:w.IsStrong?Stops[3]:Stops[2];
            wind.color=colour;
            windArrow.gameObject.SetActive(!calm);
            windArrow.localRotation=Quaternion.Euler(0,0,-angle);
            windArrow.GetComponent<Image>().color=colour;
            wind.text=calm?"CALM":$"<size=30>{mph:F0}</size> MPH  <size=18>{w.Strength.ToUpperInvariant()}</size>";
        }
        public void SetFlight(bool on){flight=on;clubRoot.gameObject.SetActive(!on);meterRoot.gameObject.SetActive(!on);lieRoot.gameObject.SetActive(!on&&lieSet);contact.transform.parent.gameObject.SetActive(on);}

        /// The ball's lie, before the shot: its name on a swatch of it, and what it costs the club in hand — the distance it
        /// keeps (CourseLies.PowerFactor) and whether it costs accuracy (CourseLies.Scatter).
        public void SetLie(CourseLie lie,GolfClub inHand)
        {
            lieSet=true;lieRoot.gameObject.SetActive(!flight);
            lieName.text=lie==CourseLie.Bunker?"SAND":lie==CourseLie.OutOfBounds?"OUT":lie.Label().ToUpperInvariant();
            int cost=inHand==GolfClub.Putter?0:Mathf.RoundToInt((float)(1-lie.PowerFactor(inHand))*100);
            double wild=lie.Scatter().degrees;
            string aim=wild>=5?"MUCH LESS ACCURATE":wild>0?"LESS ACCURATE":null;
            lieCost.text=cost>0&&aim!=null?$"-{cost}% DISTANCE\n{aim}":cost>0?$"-{cost}% DISTANCE":aim??(lie.IsPuttingSurface()?"PUTTING SURFACE":"CLEAN LIE");
            lieCost.color=cost>0||aim!=null?Stops[2]:new Color(1,1,1,.8f);
            var (fill,mark)=lie switch{
                CourseLie.Rough=>(UiKit.Hex("3E7A2E"),UiKit.Hex("1F4A18")),
                CourseLie.Bunker=>(UiKit.Hex("EED39A"),UiKit.Hex("B8955A")),
                CourseLie.Green or CourseLie.Fringe=>(UiKit.Hex("8FDB6A"),UiKit.Hex("B9EE98")),
                CourseLie.Ice=>(UiKit.Hex("D8F1FF"),UiKit.Hex("9CCBE6")),
                _=>(UiKit.Hex("6CC24A"),UiKit.Hex("8ED66C")),
            };
            lieSwatch.color=fill;
            // a hint of the surface on the swatch: blades of long grass, grains of sand, mown stripes
            for(int i=0;i<lieMarks.Length;i++){
                var r=lieMarks[i].rectTransform;lieMarks[i].color=mark;lieMarks[i].sprite=null;
                if(lie==CourseLie.Rough){r.sizeDelta=new Vector2(5,20);r.anchoredPosition=new Vector2(-11+11*i,i==1?2:-2);r.localRotation=Quaternion.Euler(0,0,12-12*i);}
                else if(lie==CourseLie.Bunker){lieMarks[i].sprite=UiKit.Circle;r.sizeDelta=new Vector2(7,7);r.anchoredPosition=new Vector2(-10+10*i,i==1?8:-5);r.localRotation=Quaternion.identity;}
                else{r.sizeDelta=new Vector2(10,44);r.anchoredPosition=new Vector2(-14+14*i,0);r.localRotation=Quaternion.Euler(0,0,30);lieMarks[i].color=i==1?fill:mark;}
            }
        }

        static Sprite arrowSprite;
        /// A plain arrow pointing up the screen, white, for tinting and turning.
        static Sprite Arrow
        {
            get
            {
                if(arrowSprite)return arrowSprite;
                const int n=64;var tex=new Texture2D(n,n,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,name="Wind arrow"};
                var px=new Color32[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++){
                    int inside=0;
                    for(int sy=0;sy<4;sy++)for(int sx=0;sx<4;sx++){
                        float u=(x+(sx+.5f)/4)/n-.5f,v=(y+(sy+.5f)/4)/n;
                        bool head=v>=.5f&&v<=.97f&&Mathf.Abs(u)<=.42f*(.97f-v)/.47f;
                        bool shaft=v>=.04f&&v<.5f&&Mathf.Abs(u)<=.1f;
                        if(head||shaft)inside++;
                    }
                    px[y*n+x]=new Color32(255,255,255,(byte)(inside*255/16));
                }
                tex.SetPixels32(px);tex.Apply();
                return arrowSprite=Sprite.Create(tex,new Rect(0,0,n,n),new Vector2(.5f,.5f),100);
            }
        }
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
            projection.SetPath(map,flight?plan.Trace:plan.Path,flight?plan.TraceColor:new Color(.25f,.78f,1),!flight);
            void Place(RectTransform t,Vector3 world,bool show){var v=map.WorldToViewportPoint(world);t.gameObject.SetActive(show&&v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1);t.anchorMin=t.anchorMax=new Vector2(v.x,v.y);t.anchoredPosition=Vector2.zero;}
            Place(ball,plan.Ball,plan.ShowBall);Place(pin,plan.Pin,true);Place(live,plan.Load,plan.ShowLoad&&!flight);
            for(int i=0;i<targets.Length;i++)Place(targets[i],i<plan.Targets.Count?plan.Targets[i]:Vector3.zero,!flight&&i<plan.Targets.Count);
        }
    }
}
