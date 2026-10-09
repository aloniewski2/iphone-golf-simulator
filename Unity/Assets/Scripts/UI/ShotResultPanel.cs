using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// The swing HUD continues into the result, with a small strip of emote/continue controls.
    public sealed class ShotResultPanel : MonoBehaviour
    {
        RectTransform summary;
        Text quality, distance, carry, extra, countdown, prompt;
        Image progress;
        CanvasGroup summaryFade;
        float elapsed, targetDistance;
        string distanceUnit="yd";
        bool countDistance;
        void Update() {
            elapsed+=Time.unscaledDeltaTime;
            if(countDistance)distance.text=$"{Mathf.Lerp(0,targetDistance,GolfArcade.Game.PresentationPolicy.Constrained?1:1-Mathf.Pow(1-Mathf.Clamp01(elapsed/.65f),3)):F0} {distanceUnit}";
        }
        RectTransform footer, remaining;
        Text detail;
        Camera view;
        CanvasScaler scaler;
        HoldButton next;
        readonly HoldButton[] emotes = new HoldButton[3];

        void LateUpdate()
        {
            bool portrait = view.aspect < 1.2f;
            scaler.referenceResolution = portrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = portrait ? 0 : 1;
            float scale = portrait ? .8f : 1;
            float t=GolfArcade.Game.PresentationPolicy.Constrained?1:Mathf.Clamp01(elapsed/.32f);
            float pop=1+Mathf.Sin(t*Mathf.PI)*.035f;
            summary.localScale = Vector3.one * scale * pop;
            summary.anchoredPosition=new Vector2(-36+(1-t)*70,-36);
            summaryFade.alpha=Mathf.Clamp01(t*2);
            remaining.localScale = Vector3.one * scale;
            remaining.anchoredPosition = new Vector2(-36, -36 - (370 + 16) * scale);
            footer.anchorMin = footer.anchorMax = portrait ? new Vector2(.5f, 0) : new Vector2(1, 0);
            footer.pivot = portrait ? new Vector2(.5f, 0) : new Vector2(1, 0);
            footer.anchoredPosition = portrait ? new Vector2(0, 52) : new Vector2(-36, 32);
            footer.localScale = Vector3.one * (portrait ? 1.9f : 1);
        }

        public static ShotResultPanel Create(Transform parent, Camera camera)
        {
            var go = new GameObject("Shot result", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = UiKit.Canvas(go);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = .5f;
            canvas.sortingOrder = 50; canvas.targetDisplay = camera.targetDisplay;
            var c = go.AddComponent<ShotResultPanel>();
            c.view = camera; c.scaler = go.GetComponent<CanvasScaler>();
            c.summary = GolfShotHud.Panel(go.transform, "Shot summary", new Vector2(1,1), new Vector2(-36,-36), new Vector2(440,370));
            c.summaryFade=c.summary.gameObject.AddComponent<CanvasGroup>();
            c.quality = GolfShotHud.Label(c.summary,"Contact",28,new Vector2(28,-26),new Vector2(384,42));
            c.quality.color = GolfShotHud.Mint;
            c.distance = GolfShotHud.Label(c.summary,"Distance",66,new Vector2(28,-84),new Vector2(384,88));
            c.carry = GolfShotHud.Label(c.summary,"Carry",26,new Vector2(28,-204),new Vector2(384,62));
            c.extra=GolfShotHud.Label(c.summary,"Roll and apex",24,new Vector2(28,-282),new Vector2(384,62));
            c.extra.color=GolfShotHud.Mint;
            c.remaining = UiKit.Pill(go.transform, "Remaining distance", GolfShotHud.Ink, new Vector2(1, 1), Vector2.zero,
                new Vector2(440, 68), out var detailFill, 4).GetComponent<RectTransform>();
            c.remaining.pivot = new Vector2(1, 1);
            c.detail = UiKit.Label(detailFill.transform, "Remaining", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, UiKit.Strong, false);
            c.detail.color = Color.white;
            c.detail.rectTransform.offsetMin = new Vector2(14, 0); c.detail.rectTransform.offsetMax = new Vector2(-14, 0);
            Icons.Fit(c.detail, 18, 26);
            c.footer = new GameObject("Shot actions", typeof(RectTransform)).GetComponent<RectTransform>();
            c.footer.SetParent(go.transform, false); c.footer.sizeDelta = new Vector2(440, 216);
            var caption = UiKit.Label(c.footer, "Emote prompt", 20, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1),
                Vector2.zero, new Vector2(440, 30), UiKit.Strong);
            c.prompt=caption;caption.text = "CHOOSE AN EMOTE";
            for (int i = 0; i < 3; i++)
            {
                c.emotes[i] = UiKit.Button(c.footer, "EMOTE", new Vector2(.5f, 0), new Vector2((i - 1) * 150, 132), new Vector2(140, 72), 21, GolfShotHud.Mint);
                c.emotes[i].GetComponentInChildren<Text>().color = UiKit.ArcadeInk;
            }
            c.next = UiKit.Button(c.footer, "NEXT SHOT", new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(440, 80), 28, GolfShotHud.Ink);
            c.countdown=UiKit.Label(c.footer,"Next player countdown",25,TextAnchor.MiddleCenter,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,40),new Vector2(440,56),UiKit.Strong,false);
            c.countdown.color=Color.white;
            var bar=UiKit.Panel(c.footer,"Countdown track",GolfShotHud.Ink,new Vector2(0,0),new Vector2(1,0),Vector2.zero,new Vector2(0,6),false);
            c.progress=UiKit.Panel(bar.transform,"Time left",GolfShotHud.Mint,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,false);
            c.progress.rectTransform.offsetMin=c.progress.rectTransform.offsetMax=Vector2.zero;
            c.countdown.gameObject.SetActive(false);c.progress.transform.parent.gameObject.SetActive(false);
            go.SetActive(false);
            return c;
        }

        public void Show(string grade, Color gradeColor, string outcome, (string icon, string title, string value)[] tiles,
            string remainingText, IList<Vector2> path, float reach, float target, string nextLabel, string[] emoteLabels, Action proceed, Func<int, bool> emote)
        {
            elapsed=0;countDistance=false;extra.text="";prompt.text="CHOOSE AN EMOTE";
            next.gameObject.SetActive(true);countdown.gameObject.SetActive(false);progress.transform.parent.gameObject.SetActive(false);
            gameObject.SetActive(true); LateUpdate();
            quality.color=gradeColor;
            quality.text = grade.TrimEnd('!') + " CONTACT";
            distance.text = tiles.Length > 5 ? tiles[5].value : "—";
            carry.text = $"CARRY  {(tiles.Length > 4 ? tiles[4].value : "—")}\n{outcome.ToUpperInvariant()}";
            detail.text = remainingText;
            next.GetComponentInChildren<Text>().text = nextLabel; next.Pressed = proceed;
            for (int i = 0; i < emotes.Length; i++)
            {
                int choice = i;
                emotes[i].gameObject.SetActive(true);
                emotes[i].GetComponentInChildren<Text>().text = emoteLabels[i].ToUpperInvariant();
                emotes[i].Pressed = () => emote(choice);
            }
        }
        public void ShowGolf(string player,string outcome,double carryYards,double rollYards,double apexYards,double remainingYards,bool putt,bool holed) {
            Show(player,GolfShotHud.Mint,outcome,new (string,string,string)[0],holed?"HOLED!":$"{remainingYards:F0} yd TO PIN",null,0,0,"",new[]{"WAVE","SCUBA","SPIKE"},null,_=>false);
            quality.text=player.ToUpperInvariant()+" · "+outcome;
            Icons.Fit(quality,18,28);
            targetDistance=(float)((carryYards+rollYards)*(putt?3:1));distanceUnit=putt?"ft":"yd";countDistance=true;
            carry.text=$"TOTAL DISTANCE\nCARRY  {carryYards:F0} yd";
            extra.text=$"ROLL  {rollYards:F0} yd     APEX  {apexYards:F0} yd";
            // Phones own emote selection during multiplayer; the TV leaves the party visible.
            foreach(var button in emotes)button.gameObject.SetActive(false);
            prompt.text="REACT ON YOUR PHONE";
        }
        public void AnimateStats(double total,double roll,double apex,bool putt) {
            targetDistance=(float)(total*(putt?3:1));distanceUnit=putt?"ft":"yd";countDistance=true;
            extra.text=$"ROLL  {roll:F0} yd     APEX  {apex:F0} yd";
        }
        public void SetCountdown(float seconds,string label) {
            next.gameObject.SetActive(false);countdown.gameObject.SetActive(true);progress.transform.parent.gameObject.SetActive(true);
            countdown.text=label+" IN "+Mathf.CeilToInt(Mathf.Max(0,seconds));
            progress.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(seconds/3),1);
        }
    }
}
