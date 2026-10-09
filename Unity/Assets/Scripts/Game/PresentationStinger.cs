using UnityEngine;
namespace GolfArcade.Game {
    /// One cue source and a bounded duck envelope; cue lifetimes follow the active scene.
    public sealed class PresentationStinger : MonoBehaviour {
        static PresentationStinger instance; AudioSource source; AudioClip cue; float duckUntil;
        public static float MusicDuck => instance && Time.unscaledTime<instance.duckUntil?.5f:1;
        public static void Lead() {
            if(!instance) {
                instance=new GameObject("Presentation stinger").AddComponent<PresentationStinger>();
                instance.source=instance.gameObject.AddComponent<AudioSource>(); instance.source.playOnAwake=false;
                const int rate=22050;var samples=new float[(int)(rate*.18f)];
                for(int i=0;i<samples.Length;i++){float t=(float)i/rate;samples[i]=Mathf.Sin(2*Mathf.PI*(520*t+700*t*t))*Mathf.Exp(-t*20)*.25f;}
                instance.cue=AudioClip.Create("Presentation pop",samples.Length,1,rate,false);instance.cue.SetData(samples,0);
            }
            instance.duckUntil=Time.unscaledTime+.5f; instance.source.Stop(); instance.source.PlayOneShot(instance.cue,.7f);
        }
        public static void Stop() { if(instance){instance.source.Stop();instance.duckUntil=0;} }
        void OnDestroy(){if(instance==this)instance=null;if(cue)Destroy(cue);}
    }
}
