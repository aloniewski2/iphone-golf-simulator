using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace GolfArcade.EditorTools {
    public sealed class PointClipListenerProbe : MonoBehaviour {
        public static int Blocks; public static float Peak;
        void OnAudioFilterRead(float[] data,int channels) { System.Threading.Interlocked.Increment(ref Blocks); foreach(float v in data) Peak=Mathf.Max(Peak,Mathf.Abs(v)); }
    }
    [InitializeOnLoad] public static class PointClipAudioProbe {
        static double until;
        static PointClipAudioProbe() { EditorApplication.update+=Tick; }
        public static void Run() { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); SessionState.SetBool("PointClipAudioProbe",true); EditorApplication.isPlaying=true; }
        static void Tick() {
            if(!SessionState.GetBool("PointClipAudioProbe",false)||!EditorApplication.isPlaying) return;
            if(until==0) {
                var listener=new GameObject("Audio probe listener"); listener.AddComponent<AudioListener>(); listener.AddComponent<PointClipListenerProbe>();
                var source=new GameObject("Audio probe source").AddComponent<AudioSource>();
                var clip=AudioClip.Create("Tone",48000,1,48000,false); var samples=new float[48000]; for(int i=0;i<samples.Length;i++) samples[i]=Mathf.Sin(i*2*Mathf.PI*440/48000)*.25f; clip.SetData(samples,0);
                source.clip=clip; source.loop=true; source.Play(); AudioListener.volume=1;
                until=EditorApplication.timeSinceStartup+3; return;
            }
            if(EditorApplication.timeSinceStartup<until) return;
            Debug.Log($"[PointClipAudioProbe] blocks={PointClipListenerProbe.Blocks} peak={PointClipListenerProbe.Peak}");
            SessionState.SetBool("PointClipAudioProbe",false); EditorApplication.Exit(PointClipListenerProbe.Blocks>0?0:1);
        }
    }
}
