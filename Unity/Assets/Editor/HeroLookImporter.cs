using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// Narrow opt-in route; legacy GolferModelImporter and live actors are unchanged.
    public sealed class HeroLookImporter : AssetPostprocessor
    {
        public const string Root="Assets/ArtDirection/Hero01/";
        public static HumanBone[] Mapping()
        {
            var names=new Dictionary<string,string>{{"Hips","Hips"},{"Spine","Spine"},{"Chest","Chest"},{"Neck","Neck"},{"Head","Head"}};
            foreach(var side in new[]{"L","R"}) {
                string human=side=="L"?"Left":"Right";
                foreach(var pair in new[]{("Shoulder","Shoulder"),("UpperArm","UpperArm"),("LowerArm","LowerArm"),("Hand","Hand"),("UpperLeg","UpperLeg"),("LowerLeg","LowerLeg"),("Foot","Foot"),("Toes","Toes")}) names[pair.Item1+"."+side]=human+pair.Item2;
            }
            var result=new List<HumanBone>(); foreach(var pair in names)result.Add(new HumanBone{boneName=pair.Key,humanName=pair.Value,limit=new HumanLimit{useDefaultValues=true}});
            return result.ToArray();
        }
        void OnPreprocessModel()
        {
            if(!assetPath.StartsWith(Root)) return;
            var m=(ModelImporter)assetImporter;
            string name=Path.GetFileNameWithoutExtension(assetPath);
            // HeroBodyProxy samples these meshes at runtime for body/racket clearance.
            if(name=="Hero_01_FingerBody" || name=="Hero_01_Mixamo_Bind" || name=="Hero_01_Shorts_Default" || name=="Hero_01_Hair_Default") m.isReadable=true;
            bool body=name=="Hero_01_Mixamo_Bind",clip=name=="Idle" || name=="Ready" || name=="Hero_Backhand_v3" || name=="Hero_Backhand_v4" || name=="Hero_Volley_v3" || (name.StartsWith("Hero_") && (name.EndsWith("_v1") || name.EndsWith("_v5")));
            m.animationType=body || clip ? ModelImporterAnimationType.Human : ModelImporterAnimationType.Generic;
            m.preserveHierarchy=true;m.weldVertices=false;m.optimizeMeshPolygons=false;m.optimizeMeshVertices=false;m.bakeAxisConversion=false;
            m.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            var hd=m.humanDescription; hd.human=Mapping();hd.upperArmTwist=.5f;hd.lowerArmTwist=.5f;hd.upperLegTwist=.5f;hd.lowerLegTwist=.5f;hd.armStretch=.05f;hd.legStretch=.05f; m.humanDescription=hd;
            if(clip) {
                var avatar=System.Array.Find(AssetDatabase.LoadAllAssetsAtPath(Root+"Models/Hero_01_Mixamo_Bind.fbx"),o=>o is Avatar) as Avatar;
                if(avatar) {m.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;m.sourceAvatar=avatar;}
            }
            if(!body && !clip)m.avatarSetup=ModelImporterAvatarSetup.NoAvatar;
            m.importAnimation=clip;
            if(name=="Hero_01_GripBody") m.importBlendShapeNormals=ModelImporterNormals.Calculate;
            m.animationCompression=ModelImporterAnimationCompression.Off;
            m.importCameras=false;m.importLights=false;m.importVisibility=false;m.optimizeGameObjects=false;
            m.importNormals=ModelImporterNormals.Import;m.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            m.skinWeights=ModelImporterSkinWeights.Custom;m.maxBonesPerVertex=4;m.minBoneWeight=.001f;
            m.meshCompression=ModelImporterMeshCompression.Off;
        }
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(Root)) return;
            var t=(TextureImporter)assetImporter; t.maxTextureSize=2048;t.mipmapEnabled=true;
            t.sRGBTexture=!assetPath.Contains("Mask") && !assetPath.Contains("Normal");
            t.textureCompression=TextureImporterCompression.Uncompressed;
            if(assetPath.Contains("Normal")) t.textureType=TextureImporterType.NormalMap;
            if(assetPath.Contains("Mask"))t.alphaSource=TextureImporterAlphaSource.FromInput;
        }
    }
}
