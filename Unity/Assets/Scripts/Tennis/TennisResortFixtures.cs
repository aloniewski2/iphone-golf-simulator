using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Tennis
{
    /// Crafted terrace furnishings in the old render-only prop envelopes.
    /// Original transforms/colliders and the real court/net stay authoritative.
    public static class TennisResortFixtures
    {
        public static void Build(Transform root,GameObject arena)
        {
            if(Environment.GetEnvironmentVariable("TENNIS_FIXTURES_OFF")=="1")return;
            var stone=new TennisVenueBuilder.MB();var masonry=new TennisVenueBuilder.MB();var ceramic=new TennisVenueBuilder.MB();var soil=new TennisVenueBuilder.MB();var bronze=new TennisVenueBuilder.MB();var fabric=new TennisVenueBuilder.MB();var timber=new TennisVenueBuilder.MB();var diffuser=new TennisVenueBuilder.MB();var drains=new TennisVenueBuilder.MB();int changed=0;
            foreach(var renderer in arena.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!renderer.enabled||!renderer.sharedMaterial)continue;
                var key=renderer.sharedMaterial.name;var b=renderer.bounds;var size=b.size;var c=b.center;var foot=new Vector3(c.x,b.min.y,c.z);bool replace=true;
                if(key.StartsWith("TropicalV3_024")) // promenade drainage: real slotted metal, not 5,700 scan triangles each
                {
                    bool alongX=size.x>size.z;float length=Mathf.Max(size.x,size.z),width=Mathf.Min(size.x,size.z);float y=b.max.y+.001f;int bars=Mathf.Clamp(Mathf.CeilToInt(length/.15f),4,80);
                    for(int i=0;i<bars;i++)
                    {
                        float at=-length*.5f+(i+.5f)*length/bars;var p=c+new Vector3(alongX?at:0,y-c.y,alongX?0:at);
                        Box(drains,p,new Vector3(alongX?Mathf.Min(.032f,length/bars*.35f):width,.012f,alongX?width:Mathf.Min(.032f,length/bars*.35f)),.003f);
                    }
                    foreach(int side in new[]{-1,1})Box(drains,new Vector3(c.x+(alongX?0:side*width*.5f),y,c.z+(alongX?side*width*.5f:0)),new Vector3(alongX?length:.035f,.016f,alongX?.035f:length),.004f);
                }
                else if(key.StartsWith("TropicalV3_027")) // cut limestone retaining wall and a separate coping edge
                {
                    bool oceanBay=c.z>21.5f && size.x>4f && size.x>size.z*3;
                    float wallHeight=oceanBay ? (Mathf.Abs(c.x)<10 ? .64f : Mathf.Abs(c.x)<20 ? .82f : 1.12f) : size.y;
                    float cap=Mathf.Min(.12f,wallHeight*.15f);
                    var wallCentre=new Vector3(c.x,b.min.y+wallHeight*.5f,c.z);
                    Box(masonry,wallCentre-Vector3.up*cap*.25f,new Vector3(size.x,wallHeight-cap*.5f,size.z),.028f);
                    Box(stone,new Vector3(c.x,b.min.y+wallHeight-cap*.5f,c.z),new Vector3(size.x+.04f,cap,size.z+.04f),.025f);
                    if(oceanBay && Mathf.Abs(c.x)>11)
                    {
                        float h=.45f;
                        TennisVenueArt.QueuePlant("FERN",new Vector3(c.x,b.min.y+wallHeight-.05f,c.z),changed*47,h,.78f);
                        TennisVenueArt.QueuePlant("SHRUB_0",new Vector3(c.x+1.2f,b.min.y+wallHeight-.04f,c.z),changed*67,.55f,.85f);
                        TennisVenueArt.QueuePlant("FLOWER_CORAL",new Vector3(c.x-.90f,b.min.y+wallHeight+.10f,c.z-.17f),changed*13,.40f,.55f);
                    }
                }
                else if(key.StartsWith("TropicalV3_031")) // understated turf border
                    Box(stone,c,size,.012f);
                else if(key.StartsWith("TropicalV3_017")) // original bounds contain both pot and foliage
                {
                    float radius=Mathf.Max(.15f,Mathf.Min(size.x,size.z)*.43f),h=size.y*.52f;
                    Profile(ceramic,foot,radius,h);
                    soil.Cyl(foot+Vector3.up*h*.88f,radius*.86f,radius*.86f,.018f,36,Vector2.zero,Vector2.zero);
                    // The new potted plants fill the old composite envelope, with
                    // ceramic below and real layered leaves above its rolled rim.
                    float plantHeight=Mathf.Max(.25f,size.y-h*.86f);
                    TennisVenueArt.QueuePlant("FERN",foot+Vector3.up*h*.89f,changed*71,plantHeight,radius*.94f);
                    TennisVenueArt.QueuePlant("SHRUB_0",foot+Vector3.up*h*.90f,changed*53+21,plantHeight*.67f,radius*.77f);
                    TennisVenueArt.QueuePlant("FLOWER_CORAL",foot+Vector3.up*(h*.92f+plantHeight*.20f),changed*47,plantHeight*.32f,radius*.55f);
                }
                else if(key.StartsWith("TropicalV3_033")) // garden lamp: slim stem, opaque warm diffuser and framed housing
                {
                    float h=size.y,w=Mathf.Min(size.x,size.z);float head=Mathf.Min(h*.28f,w*.98f),stem=h-head;
                    bronze.Cyl(foot,Mathf.Max(.05f,w*.12f),Mathf.Max(.025f,w*.08f),stem,16,Vector2.zero,Vector2.zero);
                    bronze.Cyl(foot,.15f,.10f,.08f,20,Vector2.zero,Vector2.zero);
                    var lamp=foot+Vector3.up*(stem+head*.5f);Box(diffuser,lamp,new Vector3(w*.70f,head*.80f,w*.70f),.02f);
                    foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})Box(bronze,lamp+new Vector3(sx*w*.38f,0,sz*w*.38f),new Vector3(.035f,head,.035f),.008f);
                    Box(bronze,foot+Vector3.up*(h-head*.08f),new Vector3(w*.94f,head*.16f,w*.94f),.025f);
                    Box(bronze,foot+Vector3.up*stem,new Vector3(w*.88f,head*.10f,w*.88f),.015f);
                }
                else if(key.StartsWith("TropicalV3_032")) // curved fabric parasol, continuous canopy and slender teak pole
                {
                    float h=size.y,rx=size.x*.49f,rz=size.z*.49f;timber.Cyl(foot,.055f,.038f,h*.96f,20,Vector2.zero,Vector2.zero);
                    bronze.Cyl(foot,.26f,.22f,.08f,24,Vector2.zero,Vector2.zero);int segments=40;
                    for(int ring=0;ring<5;ring++)for(int n=0;n<segments;n++)
                    {
                        Vector3 Point(float u,float theta){float radius=u;return foot+new Vector3(Mathf.Cos(theta)*rx*radius,h*(.99f-.19f*Mathf.Pow(radius,.62f)),Mathf.Sin(theta)*rz*radius);}
                        float a=n*Mathf.PI*2/segments,bb=(n+1)*Mathf.PI*2/segments,u=ring/5f,v=(ring+1)/5f;
                        // Upward front winding, with a second sealed underside for consistent silhouette shadows.
                        var p0=Point(u,a);var p1=Point(u,bb);var p2=Point(v,bb);var p3=Point(v,a);
                        fabric.Face4(p0,p3,p2,p1,new Vector2(u,n/(float)segments),new Vector2(v,n/(float)segments),new Vector2(v,(n+1)/(float)segments),new Vector2(u,(n+1)/(float)segments));
                        fabric.Face4(p0-Vector3.up*.008f,p1-Vector3.up*.008f,p2-Vector3.up*.008f,p3-Vector3.up*.008f,Vector2.zero,Vector2.right,Vector2.one,Vector2.up);
                    }
                }
                else if(key.StartsWith("TropicalV3_020")) // crafted low sofas, retained actual placement/envelope
                {
                    float h=size.y;Box(timber,foot+Vector3.up*h*.18f,new Vector3(size.x*.98f,h*.30f,size.z*.94f),.05f);
                    Box(fabric,foot+Vector3.up*h*.50f,new Vector3(size.x*.96f,h*.25f,size.z*.94f),.06f);
                    // Every fixture faces the live court, matching the seating bank purpose.
                    float toward=c.x<0?1:-1;Box(fabric,foot+new Vector3(-toward*size.x*.39f,h*.75f,0),new Vector3(size.x*.19f,h*.50f,size.z*.97f),.06f);
                }
                else replace=false;
                if(replace){renderer.enabled=false;changed++;}
            }
            var wall=TennisVenueArt.Surface("Crafted resort ashlar walls",new Color(.97f,.97f,.94f),.15f,0,.016f,.006f);
            wall.SetTexture("_MasonryMap",Resources.Load<Texture2D>("Tennis/Premium/LimestoneAshlar_C"));
            wall.SetTexture("_MasonryNormal",Resources.Load<Texture2D>("Tennis/Premium/LimestoneAshlar_N"));
            wall.SetVector("_MasonrySize",new Vector4(2.88f,1.28f,0,0));wall.SetFloat("_MasonryWeight",1);
            Mesh(root,"Crafted resort ashlar walls",masonry,wall,true);
            Mesh(root,"Crafted resort ceramic planters",ceramic,TennisVenueArt.Surface("Crafted resort ivory ceramic",new Color(.84f,.82f,.72f),.28f,0,.018f,.004f),true);
            Mesh(root,"Crafted resort planting soil",soil,TennisVenueArt.Surface("Crafted resort potting soil",new Color(.105f,.075f,.047f),.08f,0,.04f,.013f),false);
            Mesh(root,"Crafted resort limestone fixtures",stone,TennisVenueArt.Surface("Crafted resort limestone",new Color(.76f,.74f,.66f),.16f,0,.032f,.007f),true);
            Mesh(root,"Crafted resort bronze fixtures",bronze,TennisVenueArt.Surface("Crafted resort matte bronze",new Color(.095f,.14f,.13f),.30f,.32f,.015f,.004f),true);
            Mesh(root,"Crafted resort woven canvas",fabric,TennisVenueArt.Surface("Crafted resort sail canvas",new Color(.76f,.81f,.75f),.08f,0,.028f,.008f),true);
            Mesh(root,"Crafted resort teak",timber,TennisVenueArt.Surface("Crafted resort oiled teak",new Color(.30f,.18f,.10f),.18f,0,.04f,.006f),true);
            Mesh(root,"Crafted resort lamp diffusion",diffuser,TennisVenueArt.Surface("Crafted resort warm diffuser",new Color(.85f,.75f,.53f),.12f,0,.012f,.004f),false);
            Mesh(root,"Crafted resort drainage",drains,TennisVenueArt.Surface("Crafted resort drainage grille",new Color(.12f,.18f,.17f),.26f,.28f,.01f,.003f),false);
            Debug.Log("[TennisResortFixtures] replaced "+changed+" dense render-only scanned fixtures in their measured envelopes; court/net/actor physics unchanged.");
        }
        static void Profile(TennisVenueBuilder.MB mb,Vector3 foot,float radius,float height)
        {
            // Continuous lathed outer wall, rounded lip and inside return.
            // Ring vertices are shared and carry analytic smooth profile normals.
            var profile=new Vector2[]{new(.69f,.015f),new(.72f,.025f),new(.74f,.07f),new(.76f,.10f),new(.82f,.30f),new(.90f,.67f),new(.94f,.87f),new(.995f,.92f),new(1,.96f),new(.985f,.995f),new(.94f,1),new(.885f,.985f),new(.86f,.94f),new(.86f,.87f)};
            const int sides=48;int[,] rings=new int[profile.Length,sides+1];
            for(int j=0;j<profile.Length;j++)
            {
                var tangent=profile[Mathf.Min(j+1,profile.Length-1)]-profile[Mathf.Max(0,j-1)];
                float dr=tangent.x*radius,dy=tangent.y*height;
                for(int i=0;i<=sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,c=Mathf.Cos(a),s=Mathf.Sin(a);
                    rings[j,i]=mb.Add(foot+new Vector3(c*profile[j].x*radius,profile[j].y*height,s*profile[j].x*radius),new Vector2(i/(float)sides,profile[j].y));
                    mb.N.Add(new Vector3(c*dy,-dr,s*dy).normalized);
                }
            }
            for(int j=0;j<profile.Length-1;j++)for(int i=0;i<sides;i++)mb.Quad(rings[j,i],rings[j,i+1],rings[j+1,i+1],rings[j+1,i]);
        }
        static void Box(TennisVenueBuilder.MB mb,Vector3 at,Vector3 size,float bevel)=>TennisVenueArt.BevelBox(mb,at,size,bevel);
        static void Mesh(Transform root,string name,TennisVenueBuilder.MB mb,Material material,bool shadow)
        {if(mb.Count>0)TennisVenueBuilder.MeshObject(root,name,mb.ToMesh(name),material,shadow);}
    }
}
