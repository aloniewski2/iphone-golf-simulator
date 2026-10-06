// The cup's mouth, drawn into the stencil only: where it is seen (not behind the ball, the
// golfer or a rise in the green) it marks the pixels the cup's inside may show through. Drawn
// straight after the course, a hair in front of the green it lies on. See HoleInside.
Shader "GolfArcade/HoleMask"
{
    SubShader
    {
        Tags { "Queue" = "Geometry+1" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        ColorMask 0
        ZWrite Off
        ZTest LEqual
        Offset -1, -1
        Cull Back
        Stencil { Ref 64 ReadMask 64 WriteMask 64 Comp Always Pass Replace }
        Pass { }
    }
}
