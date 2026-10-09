#ifndef GOLF_TURF_FIELD_INCLUDED
#define GOLF_TURF_FIELD_INCLUDED
float GolfTurfHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float GolfTurfNoise(float2 p) {
 float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
 return lerp(lerp(GolfTurfHash(cell),GolfTurfHash(cell+float2(1,0)),f.x),lerp(GolfTurfHash(cell+float2(0,1)),GolfTurfHash(cell+1),f.x),f.y);
}
half GolfTurfGrowth(float2 world) {
 return saturate(GolfTurfNoise(world*.14+float2(13,7))*.65+GolfTurfNoise(world*.43)*.35);
}
half GolfTurfBroad(float2 world) {
 // Two continuous meter-scale growth fields: no tile-local repetition or
 // temporal noise. Blades and their baked understory share these patches.
 return 1+(GolfTurfNoise(world*.43)-.5)*.20+(GolfTurfNoise(world*.14+float2(13,7))-.5)*.12;
}
#endif
