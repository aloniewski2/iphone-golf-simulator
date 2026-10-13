using GolfArcade.Shot;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// Small shared broadcast vocabulary: distance, club, wind, and matching power/landing colours.
    /// The map positions come from GolfGame's actual launch simulation, never screen estimates.
    public sealed class GolfShotHud : MonoBehaviour
    {
        public static readonly Vector2 MapSize = new Vector2(360,440);
        public static readonly Color Ink = UiKit.Hex("10243D", .96f);
        public static readonly Color Mint = UiKit.Hex("D7F044");
        public static readonly Color[] Stops = { new(.52f,.85f,1), new(.5f,.95f,.78f), new(1,.88f,.42f), new(1,.61f,.36f) };
        Camera view; Canvas canvas; CanvasScaler scaler;
        Text hole, distance, club, wind, contact, liveYards, clubChange;
        Text[] clubOptions = new Text[3];
        RectTransform clubRoot;
        GolfMapPath projection;
        GolfClub? selectedClub; float clubChangedAt;
        RawImage mapImage; RectTransform infoRoot, mapRoot, meterRoot, contactRoot, tip;
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
            h.infoRoot=info;
            h.hole=Label(info,"Hole",22,new Vector2(24,-24),new Vector2(250,28));
            h.distance=Label(info,"Distance",46,new Vector2(24,-64),new Vector2(260,56));
            h.club=Label(info,"Club",24,new Vector2(24,-126),new Vector2(250,32));h.club.color=Mint;
            h.wind=Label(info,"Wind",22,new Vector2(24,-174),new Vector2(250,28));
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
            mapRoot.anchoredPosition=new Vector2(-right,-top);
            mapRoot.sizeDelta=portrait?new Vector2(344,454):new Vector2(380,498);
            meterRoot.anchorMin=meterRoot.anchorMax=new Vector2(0,portrait?.38f:.29f);
            meterRoot.anchoredPosition=new Vector2(left+38,0);
            clubRoot.anchoredPosition=new Vector2(left,portrait?250:40);
            if(selectedClub.HasValue && Time.unscaledTime-clubChangedAt>2.4f)clubChange.text="SELECT CLUB";
            contactRoot.anchoredPosition=new Vector2(0,-top-(portrait?232:0));
        }
        public void SetHole(int n,int par)=>hole.text=$"HOLE {n:00}  ·  PAR {par}";
        RectTransform conditionsRoot;
        /// Today's conditions under the shot card: a chip for the weather and one for each twist,
        /// in their colours with their icons.
        public void SetConditions(EventFace[] faces)
        {
            if(conditionsRoot)Destroy(conditionsRoot.gameObject);
            conditionsRoot=null;
            conditionCount=faces?.Length??0;PlaceBounty();
            if(faces==null||faces.Length==0)return;
            conditionsRoot=new GameObject("Conditions",typeof(RectTransform)).GetComponent<RectTransform>();
            conditionsRoot.SetParent(infoRoot,false);
            conditionsRoot.anchorMin=conditionsRoot.anchorMax=conditionsRoot.pivot=new Vector2(0,0);
            conditionsRoot.anchoredPosition=new Vector2(0,-10);conditionsRoot.sizeDelta=new Vector2(300,0);
            for(int i=0;i<faces.Length;i++){
                var f=faces[i];
                var chip=UiKit.Panel(conditionsRoot,"Chip "+f.Title,Ink,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-i*44),new Vector2(300,38));
                chip.rectTransform.pivot=new Vector2(0,1);chip.raycastTarget=false;
                var key=UiKit.Panel(chip.transform,"Colour",f.Colour,new Vector2(0,0),new Vector2(0,1),Vector2.zero,new Vector2(38,0));key.raycastTarget=false;
                var icon=Icons.Place(key.transform,f.Icon,Ink,new Vector2(.5f,.5f),Vector2.zero,26);icon.raycastTarget=false;
                var t=UiKit.Label(chip.transform,"Name",19,TextAnchor.MiddleLeft,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,UiKit.Strong,false);
                t.rectTransform.offsetMin=new Vector2(50,0);t.rectTransform.offsetMax=new Vector2(-10,0);t.text=f.Title.ToUpperInvariant();t.color=Color.white;t.raycastTarget=false;
            }
        }
        readonly System.Collections.Generic.List<RectTransform> ringMarks=new();
        RectTransform bountyRoot, coinsRoot; Text bountyTitle, bountyReward, coinsText; Image bountyMark, bountyKey;
        int conditionCount; float coinsBumpedAt=-9;
        /// The hole's bounty under the conditions: a gold chip with what to do and what it pays,
        /// ticked when done, crossed and dimmed when missed. Null takes it away.
        public void SetBounty(string title,int coins,int state,string icon)
        {
            if(title==null){if(bountyRoot)bountyRoot.gameObject.SetActive(false);return;}
            if(!bountyRoot){
                bountyRoot=UiKit.Panel(infoRoot,"Bounty",Ink,new Vector2(0,0),new Vector2(0,0),Vector2.zero,new Vector2(300,62)).rectTransform;
                bountyRoot.pivot=new Vector2(0,1);bountyRoot.GetComponent<Image>().raycastTarget=false;
                bountyKey=UiKit.Panel(bountyRoot,"Colour",Gold,new Vector2(0,0),new Vector2(0,1),Vector2.zero,new Vector2(46,0));bountyKey.raycastTarget=false;
                bountyMark=Icons.Place(bountyKey.transform,"star",Ink,new Vector2(.5f,.5f),Vector2.zero,30);bountyMark.raycastTarget=false;
                bountyReward=UiKit.Label(bountyRoot,"Reward",15,TextAnchor.UpperLeft,new Vector2(0,1),new Vector2(1,1),new Vector2(0,-6),new Vector2(0,20),UiKit.Strong,false);
                bountyReward.rectTransform.pivot=new Vector2(0,1);bountyReward.rectTransform.offsetMin=new Vector2(58,bountyReward.rectTransform.offsetMin.y);bountyReward.rectTransform.offsetMax=new Vector2(-8,-6);bountyReward.raycastTarget=false;
                bountyTitle=UiKit.Label(bountyRoot,"Task",19,TextAnchor.LowerLeft,Vector2.zero,new Vector2(1,0),new Vector2(0,6),new Vector2(0,30),UiKit.Strong,false);
                bountyTitle.rectTransform.pivot=new Vector2(0,0);bountyTitle.rectTransform.offsetMin=new Vector2(58,6);bountyTitle.rectTransform.offsetMax=new Vector2(-8,34);bountyTitle.raycastTarget=false;
                bountyTitle.horizontalOverflow=HorizontalWrapMode.Wrap;bountyTitle.verticalOverflow=VerticalWrapMode.Truncate;
                bountyTitle.resizeTextForBestFit=true;bountyTitle.resizeTextMinSize=12;bountyTitle.resizeTextMaxSize=19;
            }
            bountyRoot.gameObject.SetActive(true);
            bool done=state==1, missed=state==2;
            bountyKey.color=missed?UiKit.Hex("6B7785"):done?UiKit.Hex("5CD66B"):Gold;
            bountyMark.sprite=Icons.Get(done?"tick":missed?"cross":icon);
            bountyReward.text=done?$"BOUNTY DONE  ·  +{coins}":missed?"BOUNTY MISSED":$"BOUNTY  ·  +{coins} COINS";
            bountyReward.color=missed?new Color(1,1,1,.5f):Gold;
            bountyTitle.text=title.ToUpperInvariant();bountyTitle.color=missed?new Color(1,1,1,.5f):Color.white;
            PlaceBounty();
        }
        void PlaceBounty(){if(bountyRoot)bountyRoot.anchoredPosition=new Vector2(0,-10-conditionCount*44);}
        public static readonly Color Gold=UiKit.Hex("FFC93C");

        /// The round's coins under the shot map (null hides them); a new total pops the counter.
        public void SetCoins(int? coins)
        {
            if(coins==null){if(coinsRoot)coinsRoot.gameObject.SetActive(false);return;}
            if(!coinsRoot){
                coinsRoot=UiKit.Panel(mapRoot,"Coins",Ink,new Vector2(1,0),new Vector2(1,0),new Vector2(0,-12),new Vector2(170,54)).rectTransform;
                coinsRoot.pivot=new Vector2(1,1);coinsRoot.GetComponent<Image>().raycastTarget=false;
                var disc=Icons.Place(coinsRoot,"coin",Gold,new Vector2(0,.5f),new Vector2(30,0),36);disc.raycastTarget=false;
                coinsText=UiKit.Label(coinsRoot,"Count",28,TextAnchor.MiddleRight,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,UiKit.Display,false);
                coinsText.rectTransform.offsetMin=new Vector2(54,0);coinsText.rectTransform.offsetMax=new Vector2(-16,0);coinsText.color=Gold;coinsText.raycastTarget=false;
            }
            coinsRoot.gameObject.SetActive(true);
            string text=coins.Value.ToString("N0");
            if(coinsText.text!=text&&coinsText.text.Length>0)coinsBumpedAt=Time.unscaledTime;
            coinsText.text=text;
        }
        void Update()
        {
            if(!coinsRoot)return;
            float t=Time.unscaledTime-coinsBumpedAt;
            coinsRoot.localScale=Vector3.one*(t<.4f?1+.28f*Mathf.Sin(t/.4f*Mathf.PI):1);
        }

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
        public void SetWind(float angle,double mph,bool calm){wind.text=calm?"WIND  ·  CALM":$"WIND  {mph:F0} mph  ↗";if(!calm)wind.text=$"WIND  {mph:F0} mph  {Direction(angle)}";}
        static string Direction(float degrees){var dirs=new[]{"↑","↗","→","↘","↓","↙","←","↖"};return dirs[Mathf.RoundToInt(Mathf.Repeat(degrees,360)/45)%8];}
        public void SetFlight(bool on){flight=on;clubRoot.gameObject.SetActive(!on);meterRoot.gameObject.SetActive(!on);contact.transform.parent.gameObject.SetActive(on);}
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
            // the rings still standing, little gold hoops on the map
            while(ringMarks.Count<plan.Rings.Count){var m=Icons.Place(mapImage.rectTransform,"ring",Gold,new Vector2(.5f,.5f),Vector2.zero,24);m.raycastTarget=false;ringMarks.Add(m.rectTransform);}
            for(int i=0;i<ringMarks.Count;i++)Place(ringMarks[i],i<plan.Rings.Count?plan.Rings[i]:Vector3.zero,i<plan.Rings.Count);
            for(int i=0;i<targets.Length;i++)Place(targets[i],i<plan.Targets.Count?plan.Targets[i]:Vector3.zero,!flight&&i<plan.Targets.Count);
        }
    }
}
