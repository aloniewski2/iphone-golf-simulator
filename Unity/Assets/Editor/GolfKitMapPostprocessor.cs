using UnityEditor;
namespace GolfArcade.EditorTools {
 public sealed class GolfKitMapPostprocessor : AssetPostprocessor {
  void OnPreprocessTexture() {
   if(!assetPath.StartsWith("Assets/Resources/Golf/HeroDetail/")||!assetPath.EndsWith(".png"))return;
   var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;t.sRGBTexture=false;t.mipmapEnabled=true;t.wrapMode=UnityEngine.TextureWrapMode.Clamp;t.filterMode=UnityEngine.FilterMode.Trilinear;t.anisoLevel=4;t.alphaSource=TextureImporterAlphaSource.FromInput;t.alphaIsTransparency=false;
   var p=t.GetPlatformTextureSettings("iPhone");p.name="iPhone";p.overridden=true;p.maxTextureSize=2048;p.format=assetPath.Contains("Atlas")?TextureImporterFormat.ASTC_4x4:TextureImporterFormat.ASTC_8x8;p.compressionQuality=100;t.SetPlatformTextureSettings(p);
  }
 }
}
