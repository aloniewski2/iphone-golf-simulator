import Foundation
import SceneKit
import simd
import CoreGraphics
import ImageIO
#if canImport(UIKit)
import UIKit
typealias HeroColor = UIColor
#else
import AppKit
typealias HeroColor = NSColor
#endif

/// LOCKER_MIRROR: what the match heroes' surfaces look like in the Swift locker mirror. SceneKit cannot run Unity's shaders, so this matches their RESULT from the numbers Unity's runtime
/// materials carry (MatchHeroLockerExport writes them into the manifest as each submesh's `look`):
///
///  * kit (Kit_Shirt, Kit_ShirtTrim, Kit_Shorts, Kit_ShortsBand, Kit_Sock, Kit_Shoe, Kit_Sole) and the racket grip = Unity's TennisCloth. The kit's own UV carries the seam / collar / hem map (multiplied into the colour);
///    a small tiling weave map adds a normal (the weave breaks the light) and a thread break (multiplied into the colour); tiled per UV unit exactly as TennisCloth does (`weaveTile`, turned by `weaveAngle`).
///    The lighting is TennisCloth's: wrapped diffuse, a faint BROAD gloss (never a dot), a soft grazing sheen that grows toward the silhouette and only where a light reaches, a weak rim, and an
///    exposure + soft shoulder so a lit white keeps its fold shading instead of clipping to one flat white.
///  * skin = TennisCharacter at smoothness 0.2 with a weak soft normal projected triplanar from the BIND pose (the body has no UVs): it rides on the skin while the body moves.
///  * eyes, the painted face decals, the racket frame and strings = TennisCharacter: flat colour, their own smoothness (eyes .85, frame .65, strings .15), no weave and no skin normal.
@MainActor enum MatchHeroSurfaces {
    // MARK: maps
    private static var imageCache: [String: CGImage] = [:]

    /// A shipped map (MatchHero_<name>.png) as a CGImage tagged for what it holds: colour maps are sRGB (SceneKit linearises them when it samples), data maps (the weave's normal and thread break, the
    /// soft skin normal) are tagged LINEAR so the sampler returns the raw numbers. `maxPixels` > 0 shrinks a bigger map (the seam maps are smooth multipliers: 1024 px is plenty on a phone).
    static func image(_ name: String, linear: Bool, maxPixels: Int = 0) -> CGImage? {
        let key = "\(name)|\(linear)|\(maxPixels)"
        if let c = imageCache[key] { return c }
        guard let url = MatchHeroData.locate(name, "png"), let src = CGImageSourceCreateWithURL(url as CFURL, nil) else { return nil }
        let decoded: CGImage?
        if maxPixels > 0, let w = (CGImageSourceCopyPropertiesAtIndex(src, 0, nil) as? [CFString: Any])?[kCGImagePropertyPixelWidth] as? Int, w > maxPixels {
            decoded = CGImageSourceCreateThumbnailAtIndex(src, 0, [kCGImageSourceCreateThumbnailFromImageAlways: true, kCGImageSourceThumbnailMaxPixelSize: maxPixels,
                                                                   kCGImageSourceShouldCacheImmediately: true] as CFDictionary)
        } else { decoded = CGImageSourceCreateImageAtIndex(src, 0, nil) }
        guard let cg = decoded else { return nil }
        // redraw into plain RGBA8 in the file's own colour space (the bytes stay what the PNG holds), then tag
        let w = cg.width, h = cg.height
        let own = cg.colorSpace ?? CGColorSpace(name: CGColorSpace.sRGB)!
        guard let ctx = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4, space: own, bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue) else { return nil }
        // Unity's v = 0 is the BOTTOM row of a texture, Metal's the TOP: the rows are stored upside down so that every (u, v) of the export (the kit's UVs, the weave tiling) reads the texel Unity reads
        ctx.translateBy(x: 0, y: CGFloat(h)); ctx.scaleBy(x: 1, y: -1)
        ctx.draw(cg, in: CGRect(x: 0, y: 0, width: w, height: h))
        guard let raw = ctx.data else { return nil }
        let bytes = Data(bytes: raw, count: w * h * 4)
        let tag = CGColorSpace(name: linear ? CGColorSpace.linearSRGB : CGColorSpace.sRGB)!
        guard let provider = CGDataProvider(data: bytes as CFData),
              let tagged = CGImage(width: w, height: h, bitsPerComponent: 8, bitsPerPixel: 32, bytesPerRow: w * 4, space: tag, bitmapInfo: CGBitmapInfo(rawValue: CGImageAlphaInfo.noneSkipLast.rawValue),
                                   provider: provider, decode: nil, shouldInterpolate: true, intent: .defaultIntent) else { return nil }
        imageCache[key] = tagged
        return tagged
    }

    static func srgbToLinear(_ c: Float) -> Float { c <= 0.04045 ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4) }
    static func linearToSRGB(_ c: Float) -> Float { c <= 0.0031308 ? c * 12.92 : 1.055 * pow(c, 1 / 2.4) - 0.055 }
    /// A flat sRGB colour for a SceneKit material (UIColor / NSColor, which SceneKit linearises itself).
    static func flatColour(_ c: SIMD3<Float>) -> HeroColor { HeroColor(red: CGFloat(c.x), green: CGFloat(c.y), blue: CGFloat(c.z), alpha: 1) }

    // MARK: the registry (what each material was made from: tests and the proof sheets read it)
    @MainActor final class Made { let look: MatchHeroData.Look?; let role: String; let colour: SIMD3<Float>; init(_ look: MatchHeroData.Look?, _ role: String, _ colour: SIMD3<Float>) { self.look = look; self.role = role; self.colour = colour } }
    private static let registry = NSMapTable<SCNMaterial, Made>(keyOptions: .weakMemory, valueOptions: .strongMemory)
    /// The look numbers and colour a hero material was built from.
    static func made(_ material: SCNMaterial) -> Made? { registry.object(forKey: material) }

    // MARK: materials
    /// SceneKit Blinn parameters for a Unity smoothness (the same mapping the locker has always used: soft plastic, no specular blobs).
    static func blinn(_ smoothness: Float) -> (specular: CGFloat, shininess: CGFloat) { (CGFloat(0.03 + 0.18 * smoothness * smoothness), CGFloat(0.12 + 0.3 * smoothness)) }

    /// One submesh's material. `colour` (sRGB) is the locker's pick for it (skin tone, racket colour, a kit role's tint), nil = the authored colour of the look.
    /// `sceneScale` is the hero's scale on the stage (a metre of the export is that many scene units): the weave's nominal tile is `tileMetres * sceneScale` scene units.
    static func material(_ sub: MatchHeroData.Sub, colour: SIMD3<Float>?, sceneScale: Float = 1) -> SCNMaterial {
        let mat: SCNMaterial
        let tint: SIMD3<Float>
        if let look = sub.look {
            tint = colour ?? SIMD3(look.color[0], look.color[1], look.color[2])
            mat = look.isCloth ? cloth(look, tint: tint, sceneScale: sceneScale) : character(look, tint: tint)
        } else {
            tint = colour ?? SIMD3(repeating: 1)
            mat = SCNMaterial(); mat.lightingModel = .blinn; mat.diffuse.contents = flatColour(tint)
        }
        mat.name = sub.material   // the Unity material name (Skin, White_Frame, Kit_Shirt ...): the role a tint is applied to
        mat.isDoubleSided = true   // a mirrored (left-handed) hero flips the winding
        registry.setObject(Made(sub.look, sub.material, tint), forKey: mat)
        return mat
    }

    // ---- TennisCharacter: skin, eyes, face decals, racket frame and strings
    private static func character(_ look: MatchHeroData.Look, tint: SIMD3<Float>) -> SCNMaterial {
        let m = SCNMaterial(); m.lightingModel = .blinn
        m.diffuse.contents = flatColour(tint)
        let b = blinn(look.smoothness)
        m.specular.contents = HeroColor(white: b.specular, alpha: 1)   // soft plastic, no specular blobs
        m.shininess = b.shininess
        guard look.bumpTriplanar > 0.5, !look.bumpMap.isEmpty, let map = image(look.bumpMap, linear: true) else { return m }
        // the soft skin normal: the body has no UVs, so it is projected along the three axes from the BIND-pose position (carried in texcoord channels 0 and 1) and blended by the skinned normal
        let prop = SCNMaterialProperty(contents: map)
        prop.wrapS = .repeat; prop.wrapT = .repeat; prop.mipFilter = .linear; prop.minificationFilter = .linear; prop.magnificationFilter = .linear
        m.setValue(prop, forKey: "skinBumpMap")
        m.setValue(SCNVector4(look.bumpScale, look.bumpTile, 0, 0), forKey: "skinBump")
        m.shaderModifiers = [.geometry: skinGeometry, .surface: skinSurface]
        return m
    }

    // ---- TennisCloth: the kit and the racket grip
    private static func cloth(_ look: MatchHeroData.Look, tint: SIMD3<Float>, sceneScale: Float) -> SCNMaterial {
        let m = SCNMaterial(); m.lightingModel = .blinn   // the per-light maths is replaced by the lighting modifier below
        // the kit's seam / collar / placket / hem map on its own UV (clamped); the grip has none (no UVs on the racket)
        if !look.baseMap.isEmpty, let seams = image(look.baseMap, linear: false, maxPixels: 1024) {
            m.diffuse.contents = seams
            m.diffuse.wrapS = .clamp; m.diffuse.wrapT = .clamp; m.diffuse.mipFilter = .linear; m.diffuse.minificationFilter = .linear; m.diffuse.magnificationFilter = .linear; m.diffuse.maxAnisotropy = 4
        } else { m.diffuse.contents = CGColor(gray: 1, alpha: 1) }
        let hasUV = !look.baseMap.isEmpty
        if hasUV {
            // the weave: a small tiling map, tiles per UV unit as TennisCloth sets them per piece (the racket has no UVs: its grip is the flat variant, with no map bound at all)
            let weave = look.weaveMap.isEmpty ? nil : image(look.weaveMap, linear: true)
            let prop = SCNMaterialProperty(contents: weave ?? CGColor(gray: 0.5, alpha: 1))
            prop.wrapS = .repeat; prop.wrapT = .repeat; prop.mipFilter = .linear; prop.minificationFilter = .linear; prop.magnificationFilter = .linear; prop.maxAnisotropy = 4
            m.setValue(prop, forKey: "weaveMap")
            let strength: Float = weave == nil ? 0 : 1
            // x tiles per UV unit, y grain angle (radians), z weave normal strength, w thread break strength
            m.setValue(SCNVector4(look.weaveTile, look.weaveAngle * .pi / 180, strength * look.weaveNormal, strength * look.weaveThread), forKey: "weaveParams")
            m.setValue(SCNVector4(weaveMipBias, weaveThreadPixelsOff, weaveThreadPixelsFull, (look.tileMetres ?? 0.026) * sceneScale), forKey: "weaveParams2")
        }
        m.setValue(SCNVector4(srgbToLinear(tint.x), srgbToLinear(tint.y), srgbToLinear(tint.z), 1), forKey: "clothTint")
        m.setValue(SCNVector4(look.rimStrength, look.rimPower, 0, 0), forKey: "rimParams")
        m.setValue(SCNVector4(rimLight.x, rimLight.y, rimLight.z, 0), forKey: "rimLight")
        m.setValue(SCNVector4(look.wrap, look.smoothness, look.sheenStrength, look.sheenPower), forKey: "clothLook")
        m.setValue(SCNVector4(look.exposure * sceneExposure, look.knee, 0, 0), forKey: "shoulder")
        m.shaderModifiers = [.surface: hasUV ? clothSurface : clothSurfaceFlat, .lightingModel: clothLighting, .fragment: clothShoulder]
        return m
    }

    /// Calibration of the locker's SceneKit light rig against the Unity court's: TennisCloth's exposure (0.7) is tuned to the court's sun, and the same number under the locker's three
    /// directional lights + ambient would read a white shirt as grey. The soft shoulder still keeps a lit white from clipping.
    static var sceneExposure: Float = 1.4
    /// The weave is 8 threads per tile (2.0 - 2.9 cm): on a phone the whole hero is ~0.6 px per mm, so a thread is about 1.5 px and can only alias. The weave is faded out by how many pixels a thread
    /// covers on screen, measured from the surface's world-space pixel footprint (not from the UVs, whose density differs from island to island): full strength from `weaveThreadPixelsFull` px per thread,
    /// gone at `weaveThreadPixelsOff`. A close-up (the locker's torso, a shoe) shows the threads; a whole body shows the seams, the trim, the sheen and the fold shading.
    static var weaveMipBias: Float = 0.6, weaveThreadPixelsOff: Float = 1.4, weaveThreadPixelsFull: Float = 3.0

    /// Colour (linear) of the main light the rim is added with (the locker's key light, warm).
    static let rimLight = SIMD3<Float>(1.0, 0.85, 0.66) * 1.05

    // MARK: shader modifiers (Metal)
    /// Skin: carry the bind-pose position (texcoord channels 0 = x, y and 1 = z) to the fragment stage.
    static let skinGeometry = """
    #pragma varyings
    float3 bindPos;
    #pragma body
    out.bindPos = float3(_geometry.texcoords[0].x, _geometry.texcoords[0].y, _geometry.texcoords[1].x);
    """
    /// Skin: TennisCharacter's triplanar soft normal, in Unity's space (the file negates z): three axis projections from the bind-pose position, weighted by the skinned object-space normal
    /// and added to it, then back to view space.
    static let skinSurface = """
    #pragma arguments
    texture2d<float> skinBumpMap;
    float4 skinBump;
    #pragma body
    constexpr sampler skinSmp(filter::linear, mip_filter::linear, address::repeat);
    float3 bp = float3(in.bindPos.x, in.bindPos.y, -in.bindPos.z);
    float3 nO = normalize((transpose(scn_node.modelViewTransform) * float4(_surface.normal, 0.0)).xyz);
    nO.z = -nO.z;
    float3 w = pow(abs(nO), float3(4.0));
    w /= (w.x + w.y + w.z + 1e-5);
    float2 tx = (skinBumpMap.sample(skinSmp, bp.zy * skinBump.y).rg * 2.0 - 1.0) * skinBump.x;
    float2 ty = (skinBumpMap.sample(skinSmp, bp.xz * skinBump.y).rg * 2.0 - 1.0) * skinBump.x;
    float2 tz = (skinBumpMap.sample(skinSmp, bp.xy * skinBump.y).rg * 2.0 - 1.0) * skinBump.x;
    nO = normalize(nO + w.x * float3(0.0, tx.y, tx.x) + w.y * float3(ty.x, 0.0, ty.y) + w.z * float3(tz.x, tz.y, 0.0));
    nO.z = -nO.z;
    _surface.normal = normalize((scn_node.normalTransform * float4(nO, 0.0)).xyz);
    """

    /// Cloth with no UVs (the racket grip): the role colour, the rim; no seam map and no weave (it would read one texel).
    static let clothSurfaceFlat = """
    #pragma arguments
    float4 clothTint;
    float4 rimParams;
    float4 rimLight;
    #pragma body
    float3 albedo = clothTint.rgb * 1.07;
    float3 n = normalize(_surface.normal);
    _surface.diffuse = float4(albedo, 1.0);
    _surface.specular = float4(1.0, 1.0, 1.0, 1.0);
    float ndv = saturate(dot(n, normalize(_surface.view)));
    float rim = pow(1.0 - ndv, rimParams.y) * rimParams.x;
    _surface.emission = float4(rimLight.rgb * rim * (0.35 + 0.65 * albedo), 0.0);
    """

    /// Cloth surface: the seam map (already in _surface.diffuse) times the role colour, the weave tilted into the normal and multiplied in as thread break; the rim goes in as emission.
    /// The tangent frame comes from screen-space derivatives of the UV (the kit's UVs are all the mesh has).
    static let clothSurface = """
    #pragma arguments
    texture2d<float> weaveMap;
    float4 clothTint;
    float4 weaveParams;
    float4 weaveParams2;
    float4 rimParams;
    float4 rimLight;
    #pragma body
    float2 uv = _surface.diffuseTexcoord;
    float3 albedo = _surface.diffuse.rgb * clothTint.rgb;
    float3 n = normalize(_surface.normal);
    float thr = 1.0;
    if (weaveParams.z > 0.001 || weaveParams.w > 0.001) {
        constexpr sampler wsmp(filter::linear, mip_filter::linear, address::repeat, max_anisotropy(4));
        float sn = sin(weaveParams.y), cs = cos(weaveParams.y);
        float2 wuv = float2(cs * uv.x - sn * uv.y, sn * uv.x + cs * uv.y) * weaveParams.x;
        float4 wv = weaveMap.sample(wsmp, wuv, bias(weaveParams2.x));
        // the weave is 8 threads per tile: where a thread is down to ~1 px (a whole body on a phone) it can only alias, and aliasing differs with each island's orientation. Fade the weave by the
        // pixels a thread covers on screen: the nominal tile (weaveParams2.w scene units) over the surface's world-space pixel footprint (the minor axis, or a quarter of the major: anisotropic x4)
        float3 dpx = dfdx(_surface.position), dpy = dfdy(_surface.position);
        float footprint = max(min(length(dpx), length(dpy)), max(length(dpx), length(dpy)) / 4.0) + 1e-7;
        float threadPixels = weaveParams2.w / footprint / 8.0;
        float fade = saturate((threadPixels - weaveParams2.y) / (weaveParams2.z - weaveParams2.y));
        wv = mix(float4(0.5, 0.5, 0.7629, 1.0), wv, fade);   // the flat weave: no slope, thread break at the map's mean (0.763 x 1.28 = 0.977)
        if (weaveParams.z > 0.001) {
            float2 txy = (wv.rg * 2.0 - 1.0) * weaveParams.z;
            txy = float2(cs * txy.x + sn * txy.y, -sn * txy.x + cs * txy.y);
            float3 ts = float3(txy, sqrt(saturate(1.0 - dot(txy, txy))));
            float3 dp1 = dfdx(_surface.position), dp2 = dfdy(_surface.position);
            float2 duv1 = dfdx(uv), duv2 = dfdy(uv);
            float3 dp2perp = cross(dp2, n), dp1perp = cross(n, dp1);
            float3 T = dp2perp * duv1.x + dp1perp * duv2.x;
            float3 B = dp2perp * duv1.y + dp1perp * duv2.y;
            float invmax = rsqrt(max(max(dot(T, T), dot(B, B)), 1e-20));
            n = normalize(T * invmax * ts.x + B * invmax * ts.y + n * ts.z);
        }
        thr = mix(1.0, wv.b * 1.28, weaveParams.w);
    }
    albedo *= 1.07 * thr;
    _surface.normal = n;
    _surface.diffuse = float4(albedo, 1.0);
    _surface.specular = float4(1.0, 1.0, 1.0, 1.0);
    float ndv = saturate(dot(n, normalize(_surface.view)));
    float rim = pow(1.0 - ndv, rimParams.y) * rimParams.x;
    _surface.emission = float4(rimLight.rgb * rim * (0.35 + 0.65 * albedo), 0.0);
    """

    /// Cloth lighting, per light: wrapped diffuse, a faint broad gloss, and the grazing sheen (tinted by sqrt of the cloth colour, gated by the light on that side).
    static let clothLighting = """
    #pragma arguments
    float4 clothLook;
    #pragma body
    float3 n = _surface.normal;
    float3 v = normalize(_surface.view);
    float ndl = dot(n, _light.direction);
    float wrapped = saturate((ndl + clothLook.x) / (1.0 + clothLook.x));
    float3 h = normalize(_light.direction + v);
    float spec = pow(saturate(dot(n, h)), clothLook.y * 28.0 + 2.0) * clothLook.y * 0.35 * saturate(ndl * 4.0);
    float ndv = saturate(dot(n, v));
    float graze = pow(1.0 - ndv, clothLook.w);
    float3 sheenTint = sqrt(max(_surface.diffuse.rgb, float3(0.0)));
    _lightingContribution.diffuse += _light.intensity.rgb * wrapped;
    _lightingContribution.specular += _light.intensity.rgb * (float3(spec) + sheenTint * (graze * saturate(ndl * 1.5) * clothLook.z));
    """

    /// Exposure, then roll the brightest channel off above the knee (hue kept): a lit white keeps a gradient instead of clipping.
    static let clothShoulder = """
    #pragma arguments
    float4 shoulder;
    #pragma body
    float3 c = _output.color.rgb * shoulder.x;
    float peak = max(max(c.r, c.g), max(c.b, 1e-4));
    float k = shoulder.y;
    float rolled = peak <= k ? peak : k + (1.0 - k) * (1.0 - exp(-(peak - k) / (1.0 - k)));
    _output.color.rgb = c * (rolled / peak);
    """
}
