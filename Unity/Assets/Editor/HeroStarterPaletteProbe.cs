#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Text;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using GolfArcade.Game;using GolfArcade.Tennis;using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools {
 public static class HeroStarterPaletteProbe {
  public static void Run(){int code=0;try{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);var log=new StringBuilder();
   void Check(MatchHeroLook look,string id,string role,Color expected){if(!look.TryGetKitColour(role,out var colour))throw new InvalidOperationException(id+" role missing "+role);float error=Mathf.Max(Mathf.Abs(colour.r-expected.r),Mathf.Abs(colour.g-expected.g),Mathf.Abs(colour.b-expected.b));log.AppendLine(id+" "+role+" actual="+ColorUtility.ToHtmlStringRGB(colour)+" maxError="+error);if(error>.00001f)throw new InvalidOperationException(id+" starter colour mismatch "+role);}
   foreach(bool female in new[]{false,true}){
    string sex=female?"Female":"Male";var top=MatchHeroLook.KitTint(female?new Color(.9373f,.5725f,.498f):new Color(.1608f,.7176f,.7294f));var bottom=MatchHeroLook.KitTint(new Color(.10f,.17f,.30f));var shoe=MatchHeroLook.KitTint(new Color(.96f,.93f,.85f));
    var golf=HeroGolfer.Build(null,new HeroLook{Female=female,Skin=new Color(.75f,.5f,.3f),HairColor=Color.black});var golfLook=golf.Root.GetComponent<MatchHeroLook>();
    Check(golfLook,"Golf "+sex+" nil",MatchHeroLook.RoleShirt,top);Check(golfLook,"Golf "+sex+" nil",MatchHeroLook.RoleShorts,bottom);Check(golfLook,"Golf "+sex+" nil",MatchHeroLook.RoleShoe,shoe);
    golfLook.SetKit(Color.white,Color.clear,Color.clear);Check(golfLook,"Golf "+sex+" explicit white",MatchHeroLook.RoleShirt,MatchHeroLook.KitTint(Color.white));Object.DestroyImmediate(golf.Root);
    var tennis=Object.Instantiate(Resources.Load<GameObject>("Tennis/Customization/Player"+sex));var tennisLook=tennis.GetComponent<MatchHeroLook>();tennisLook.SetKit(Color.clear,Color.clear,Color.clear);Check(tennisLook,"Tennis "+sex+" nil",MatchHeroLook.RoleShirt,top);Check(tennisLook,"Tennis "+sex+" nil",MatchHeroLook.RoleShorts,bottom);Check(tennisLook,"Tennis "+sex+" nil",MatchHeroLook.RoleShoe,shoe);Object.DestroyImmediate(tennis);
   }
   string path=Environment.GetEnvironmentVariable("HSP_OUT")??Path.GetFullPath("../proof/full-visual-overhaul/characters/starter-palette-builder-audit.txt");Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,log.ToString());Debug.Log("HERO_STARTER_PALETTE_COMPLETE "+path);
  }catch(Exception e){Debug.LogException(e);code=1;}if(Application.isBatchMode)EditorApplication.Exit(code);}
 }
}
#endif
