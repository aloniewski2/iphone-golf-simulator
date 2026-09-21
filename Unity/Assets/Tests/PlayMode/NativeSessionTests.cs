using System.Collections;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GolfArcade.Tests {
    public class NativeSessionTests {
        [UnityTest] public IEnumerator NativeLaunchSupportsBothSportsIdentitiesAndHands() {
            var host=new GameObject("NativeSportsSession"); Object.DontDestroyOnLoad(host);
            var bridge=host.AddComponent<NativeSportsSession>();
            try {
                foreach(var sport in new[]{"golf","tennis"})
                foreach(var female in new[]{false,true})
                foreach(var left in new[]{false,true}) {
                    string id=$"{sport}-{female}-{left}";
                    bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message {version=1,session=id,action="start",sport=sport,female=female,left=left,touch=true}));
                    for(int frame=0;frame<600;frame++) {
                        yield return null;
                        if(bridge.Ready) break;
                    }
                    Assert.IsTrue(bridge.Ready,"A real gameplay camera must finish rendering before ready");
                    Assert.IsNotNull(bridge.GameplayCamera);
                    Assert.AreEqual(0,bridge.GameplayCamera.targetDisplay);
                    Assert.IsTrue(NativeSportsSession.Active);
                    Assert.AreEqual(left,NativeSportsSession.Left);
                    Assert.AreEqual(0,Time.timeScale,"Native start must wait for explicit resume");
                    if(sport=="tennis") {
                        var game=Object.FindFirstObjectByType<TennisGame>(); Assert.IsNotNull(game);
                        Assert.IsTrue(game.Initialized);
                        Assert.AreEqual(female,game.FemalePlayer);
                        var model=game.Player.transform.GetChild(0);
                        Assert.AreEqual(left?-1:1,Mathf.Sign(model.localScale.x));
                        Assert.IsNotNull(game.Player.SweetSpot);
                        game.RequestSwing(.8f); game.Player.Tick(.18f,0);
                        Assert.IsFalse(float.IsNaN(game.Player.SweetSpot.position.x));
                    } else {
                        var game=Object.FindFirstObjectByType<GolfGame>(); Assert.IsNotNull(game);
                        Assert.AreEqual(GolfGame.State.Aim,game.Current,"Start paused at address, not during the flyover");
                        var golfer=Object.FindFirstObjectByType<GolferView>(FindObjectsInactive.Include);
                        Assert.IsNotNull(golfer);
                        Assert.IsTrue(golfer.UsesStandardCharacter);
                    }
                    bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message {version=1,session=id,action="end"}));
                    Assert.IsFalse(NativeSportsSession.Active);
                }
            } finally { Object.DestroyImmediate(host); Time.timeScale=1; }
        }
    }
}
