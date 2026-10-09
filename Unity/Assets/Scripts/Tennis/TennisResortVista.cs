using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Court-facing resort landmarks. All geometry is render-only and stays
    /// outside the live court/runoff; no collider or gameplay actor is created.
    public static class TennisResortVista
    {
        public static void Build(Transform root)
        {
            if(System.Environment.GetEnvironmentVariable("TENNIS_HORIZON2")!="0" && AuthoredHorizon(root))return;
            var green = new TennisVenueBuilder.MB { Colors=true };
            var rock = new TennisVenueBuilder.MB();
            var beach = new TennisVenueBuilder.MB();
            Island(green,rock,beach,new Vector2(-15,600),new Vector2(228,95),1);
            Island(green,rock,beach,new Vector2(285,690),new Vector2(130,72),3);
            Island(green,rock,beach,new Vector2(-340,720),new Vector2(144,64),6);
            var foliage=TennisVenueArt.Surface("Resort distant island vegetation",Color.white,.10f,0,.08f,.004f);
            var mineral=TennisVenueArt.Surface("Resort distant weathered limestone",new Color(.55f,.59f,.50f),.14f,0,.055f,.006f);
            mineral.SetTexture("_WorldMap",Resources.Load<Texture2D>("Course/Resort/Limestone_C"));
            mineral.SetFloat("_WorldMapWeight",.25f);mineral.SetFloat("_WorldMapScale",.036f);
            foliage.SetFloat("_VertexTint",1);
            Make(root,"Distant lush resort islands",green,foliage);
            Make(root,"Distant exposed island ridges",rock,mineral);
            Make(root,"Distant island beaches",beach,TennisVenueArt.Surface("Resort distant warm beaches",new Color(.78f,.76f,.64f),.12f));
        }

        // One authored render-only archipelago on the original distant envelopes.
        // No global atmosphere, court camera, collider or gameplay actor changes.
        static bool AuthoredHorizon(Transform root)
        {
            var prefab=Resources.Load<GameObject>("Tennis/Premium/TennisHorizon2");
            var shader=Resources.Load<Shader>("Tennis/Shaders/TennisHorizon2");
            if(!prefab||!shader)return false;
            var horizon=Object.Instantiate(prefab,root);
            horizon.name="Authored layered resort horizon";
            // The coast uses the same Blender -Z/Y export: Unity imports its
            // forward direction reversed. Apply the same yaw to restore world +Z.
            horizon.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,180,0)*prefab.transform.rotation);
            var material=new Material(shader){name="Resort horizon forest and limestone",enableInstancing=true};
            horizon.AddComponent<TennisHorizonMaterialOwner>().Material=material;
            material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_VertexTint",1);material.SetFloat("_Smoothness",.08f);
            material.SetFloat("_Variation",.012f);material.SetFloat("_Grain",0);
            material.SetFloat("_HorizonFogShare",.40f);
            foreach(var renderer in horizon.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterial=material;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows=false;
                Debug.Log($"[TennisHorizon2] renderer={renderer.name} active={renderer.gameObject.activeInHierarchy} enabled={renderer.enabled} layer={renderer.gameObject.layer} boundsMin={renderer.bounds.min:F2} boundsMax={renderer.bounds.max:F2} scale={renderer.transform.lossyScale:F3} shaderSupported={shader.isSupported}");
            }
            Debug.Log("[TennisHorizon2] Render-only layered horizon; original court, light/fog and physics retained");
            return true;
        }

        static void Island(TennisVenueBuilder.MB green,TennisVenueBuilder.MB rock,TennisVenueBuilder.MB beach,
                           Vector2 centre,Vector2 extent,int seed)
        {
            const int around=144,rings=38;
            var points=new Vector3[rings+1,around+1];
            for(int j=0;j<=rings;j++)for(int i=0;i<=around;i++)
            {
                float r=j/(float)rings,a=(i%around)*Mathf.PI*2/around;
                float outline=1+.075f*Mathf.Sin(a*3+seed)+.045f*Mathf.Cos(a*5+seed*.31f);
                float x=Mathf.Cos(a)*r*extent.x*outline,z=Mathf.Sin(a)*r*extent.y*outline;
                float p0=Mathf.Exp(-((x+extent.x*.46f)*(x+extent.x*.46f)/2000+(z+19)*(z+19)/650));
                float p1=Mathf.Exp(-((x+extent.x*.02f)*(x+extent.x*.02f)/900+(z-18)*(z-18)/480));
                float p2=Mathf.Exp(-((x-extent.x*.36f)*(x-extent.x*.36f)/1700+(z+8)*(z+8)/540));
                float p3=Mathf.Exp(-((x-extent.x*.63f)*(x-extent.x*.63f)/760+(z-24)*(z-24)/460));
                float fade=Mathf.SmoothStep(0,1,Mathf.Clamp01((1-r)/.22f));
                float ridge=(Mathf.PerlinNoise(x*.029f+seed*9,z*.045f+31)-.5f)*5;
                float scale=extent.x/228;
                float y=-4.1f+(p0*24+p1*32+p2*22+p3*17+ridge+4)*fade*scale;
                points[j,i]=new Vector3(centre.x+x,y,centre.y+z);
            }
            // Grid-adjacent face normals are shared before the material split.
            // Duplicated MB vertices receive these exact normals, so geology
            // boundaries and source quad edges do not form faceted cone bands.
            var normals=new Vector3[rings+1,around+1];
            for(int j=0;j<rings;j++)for(int i=0;i<around;i++)
            {
                int k=(i+1)%around;
                var a=points[j,i];var b=points[j,k];var c=points[j+1,k];var d=points[j+1,i];
                var normal=Vector3.Cross(c-a,d-a)+Vector3.Cross(b-a,c-a);
                normals[j,i]+=normal;normals[j,k]+=normal;normals[j+1,k]+=normal;normals[j+1,i]+=normal;
            }
            Vector3 centreNormal=Vector3.zero;for(int i=0;i<around;i++)centreNormal+=normals[0,i];centreNormal.Normalize();
            for(int j=0;j<=rings;j++)for(int i=0;i<=around;i++)
                normals[j,i]=j==0?centreNormal:normals[j,i%around].normalized;
            int Add(TennisVenueBuilder.MB target,int j,int i)
            {
                var point=points[j,i];var normal=normals[j,i];
                float dapple=Mathf.PerlinNoise(point.x*.028f+seed*13,point.z*.037f+seed*9);
                var colour=Color.Lerp(new Color(.16f,.32f,.20f),new Color(.36f,.43f,.25f),dapple);
                float steep=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,.90f,normal.y)))*.70f;
                // Vegetated shoulders and weathered exposed ridges use broad
                // graded geological colour; fine stripes would alias at 600m.
                colour=Color.Lerp(colour,new Color(.53f,.59f,.44f),steep);
                float haze=Mathf.Clamp01(centre.magnitude*.00027f);
                colour=Color.Lerp(colour,new Color(.45f,.65f,.69f),haze);
                int id=target.Add(point,new Vector2(point.x,point.z)*.03f,(Color32)colour);target.N.Add(normal);return id;
            }
            for(int j=0;j<rings;j++)for(int i=0;i<around;i++)
            {
                var average=(points[j,i]+points[j,i+1]+points[j+1,i+1]+points[j+1,i])*.25f;
                var target=average.y<-.15f?beach:green;
                int a=Add(target,j,i),b=Add(target,j,i+1),c=Add(target,j+1,i+1),d=Add(target,j+1,i);
                if(j==0)target.Tri(a,d,c);else target.Quad(a,d,c,b);
            }
        }

        internal static void Pergola(TennisVenueBuilder.MB limestone,TennisVenueBuilder.MB timber)
        {
            const float x=-16.6f;
            for(int i=0;i<5;i++)
            {
                float z=-13.4f+i*6.7f;
                TennisVenueArt.BevelBox(limestone,new Vector3(x,.13f,z),new Vector3(1.36f,.26f,1.36f),.10f);
                Column(limestone,new Vector3(x,.26f,z));
                TennisVenueArt.BevelBox(limestone,new Vector3(x,5.29f,z),new Vector3(1.34f,.30f,1.34f),.06f);
            }
            // Deep beam ends, a shadowed double entablature and close-cut rafters
            // carry the same warm timber craft as the resort reference portico.
            foreach(float side in new[]{-1f,1f})
                TennisVenueArt.BevelBox(timber,new Vector3(x+side*1.62f,5.65f,0),new Vector3(.25f,.44f,29.6f),.045f);
            TennisVenueArt.BevelBox(timber,new Vector3(x,5.54f,0),new Vector3(.70f,.55f,29.4f),.055f);
            for(int i=0;i<20;i++)
            {
                float z=-14.4f+i*1.52f;
                TennisVenueArt.BevelBox(timber,new Vector3(x,5.98f,z),new Vector3(4.50f,.27f,.20f),.035f);
                TennisVenueArt.BevelBox(timber,new Vector3(x+1.84f,5.80f,z),new Vector3(.44f,.27f,.22f),.030f);
            }
            // The L-shaped rear return gives the court a readable resort portico
            // while remaining beyond z=21 and keeping the ocean centre open.
            for(int i=0;i<3;i++)
            {
                float cx=-23.6f+i*4.1f;
                TennisVenueArt.BevelBox(limestone,new Vector3(cx,.13f,23.4f),new Vector3(1.36f,.26f,1.36f),.10f);
                Column(limestone,new Vector3(cx,.26f,23.4f));
                TennisVenueArt.BevelBox(limestone,new Vector3(cx,5.29f,23.4f),new Vector3(1.34f,.30f,1.34f),.06f);
            }
            TennisVenueArt.BevelBox(timber,new Vector3(-19.5f,5.65f,23.4f),new Vector3(10.6f,.44f,.65f),.045f);
            for(int n=0;n<8;n++)TennisVenueArt.BevelBox(timber,new Vector3(-24.2f+n*1.35f,5.98f,23.4f),new Vector3(.20f,.27f,3.6f),.035f);
        }

        static void Column(TennisVenueBuilder.MB mesh,Vector3 origin)
        {
            // Rounded architectural torus/scotia steps flow into the tapered
            // shaft. Shared rings produce clean radial lighting, unlike cubes.
            var profile=new Vector2[]{new(.54f,0),new(.57f,.035f),new(.57f,.14f),new(.54f,.19f),
                new(.44f,.25f),new(.41f,.32f),new(.38f,.45f),new(.375f,.58f),
                new(.33f,4.27f),new(.35f,4.43f),new(.43f,4.52f),new(.47f,4.62f),
                new(.47f,4.77f),new(.43f,4.84f),new(.43f,4.91f)};
            const int sides=48;var rings=new int[profile.Length,sides+1];
            for(int j=0;j<profile.Length;j++)for(int i=0;i<=sides;i++)
            {
                float angle=i*Mathf.PI*2/sides;
                rings[j,i]=mesh.Add(origin+new Vector3(Mathf.Cos(angle)*profile[j].x,profile[j].y,Mathf.Sin(angle)*profile[j].x),new Vector2(i/(float)sides,profile[j].y));
            }
            for(int j=0;j<profile.Length-1;j++)for(int i=0;i<sides;i++)
                mesh.Quad(rings[j,i],rings[j,i+1],rings[j+1,i+1],rings[j+1,i]);
        }

        static void Make(Transform root,string name,TennisVenueBuilder.MB geometry,Material material)
        {
            if(geometry.Count>0)TennisVenueBuilder.MeshObject(root,name,geometry.ToMesh(name),material,false);
        }
    }
}
