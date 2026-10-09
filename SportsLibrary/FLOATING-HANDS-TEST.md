# Floating-hands standard (original experiment notes)

Promoted by the owner to the permanent multi-sport visual standard. Enabled by default in the shared `StandardCharacterArms` component for both identities and every imported sport rig. The original character identities are preserved. Visibility can still be reversed for technical inspection, but visible arms are no longer the product default. Historical source Blender masters remain unchanged.

Hidden: the continuous upper-arm/forearm surfaces, shoulder fabric, short sleeves and sleeve piping. Original disconnected arm meshes remain hidden by the existing arm correction. Visible: hands, torso, head, legs and equipment.

The complete skeleton, golf grip correction and arm solver continue evaluating, so the hands and club retain the previous swing motion. No bones, source meshes or Blender assets are deleted.

Call `golfer.SetFloatingHandsPreview(false)` to restore arms and shoulder/sleeve renderers; `true` enables the experiment again. The shared `StandardCharacterArms.SetFloatingHandsPreview` method is also available to future sport controllers. Other-sport gameplay is not yet integrated.

Verified in Unity 6000.3.24f1: all 3 PlayMode tests pass. Both golfers are checked for hidden arm/shoulder/sleeve renderers, visible hand meshes, successful visibility restoration, unchanged grip stability and successful shots. Captures use the real Golf scene at 60 fps offline.

## Shorter, farther-out club revision

`StandardGolfGrip.ClubLengthScale` is now 0.88. The equipment assembly is uniformly reduced by 12% (including head and handle), while the character and hand meshes retain their size. The club head is anchored before shortening; both hands travel with the handle toward the head. An additional 10 cm outward target bias before reach limiting moves the grip away from the chest through backswing/delivery, easing out during the finish. Actual displacement is pose-dependent and constrained by arm reach and contact preservation—not a fixed 10 cm translation in every pose.

The golf-specific correction applies to both standard characters, independently of whether the floating-hands preview is enabled. Source FBXs and Blender masters are unchanged by this sizing revision. The 54 EditMode tests pass, including a per-frame check for the 0.88 club-length ratio and preserved address/impact contact.
