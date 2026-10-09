using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// Equal material state may share a visual batch. Values remain exact: no palette
    /// quantization and no mutation of the original source material or source mesh.
    static class GolfMaterialIdentity
    {
        public static string Key(Material material, Dictionary<Material,string> cache)
        {
            if(cache.TryGetValue(material,out var result))return result;
            var key=new StringBuilder();var shader=material.shader;
            key.Append(shader.GetInstanceID()).Append('/').Append(material.renderQueue)
                .Append('/').Append(material.enableInstancing).Append('/').Append(material.doubleSidedGI)
                .Append('/').Append((int)material.globalIlluminationFlags).Append('/').Append(material.GetTag("RenderType",false,""));
            var keywords=material.shaderKeywords;Array.Sort(keywords,StringComparer.Ordinal);
            foreach(var keyword in keywords)key.Append('/').Append(keyword);
            for(int pass=0;pass<material.passCount;pass++){
                var name=material.GetPassName(pass);key.Append('/').Append(name).Append(':').Append(material.GetShaderPassEnabled(name));
            }
            void number(float value)=>key.Append(':').Append(BitConverter.SingleToInt32Bits(value));
            void vector(Vector4 value){number(value.x);number(value.y);number(value.z);number(value.w);}
            for(int i=0;i<shader.GetPropertyCount();i++){
                int id=shader.GetPropertyNameId(i);key.Append('|').Append(id);
                switch(shader.GetPropertyType(i)){
                    case ShaderPropertyType.Color: vector(material.GetColor(id));break;
                    case ShaderPropertyType.Vector: vector(material.GetVector(id));break;
                    case ShaderPropertyType.Texture:
                        var texture=material.GetTexture(id);key.Append(':').Append(texture?texture.GetInstanceID():0);
                        var scale=material.GetTextureScale(id);var offset=material.GetTextureOffset(id);number(scale.x);number(scale.y);number(offset.x);number(offset.y);break;
                    default:number(material.GetFloat(id));break;
                }
            }
            result=key.ToString();cache.Add(material,result);return result;
        }
    }
}
