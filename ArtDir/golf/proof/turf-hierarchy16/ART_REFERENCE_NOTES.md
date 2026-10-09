# Real-course turf direction

Official photographs guide the surface hierarchy and horticultural setting; they are not copied into game textures. All numerical albedo values in GolfTurfPalette.cs are art-directed sRGB approximations. Photographic sunlight, white balance and grading mean a pixel is not an albedo measurement.

- [Pebble Beach official daylight fairway](https://www.pebblebeach.com/content/uploads/01aPBGL21-2mm0962Hc-900x600.jpg): root visual observation of broad longitudinal light/dark muted-green bands, irregular green/yellow rough, and a fine clean green.
- [Pinehurst official turtleback green](https://www.pinehurst.com/wp-content/uploads/2025/12/turtleback-greens.webp): root visual observation of a smooth cooler sage putting surface, deeper collar, and native straw beyond.
- [USGA Defining Definition](https://www.usga.org/content/usga/home-page/course-care/green-section-record/57/22/defining-definition.html): root research notes attribute light/dark mowing response to grass grain relative to the viewer and advise avoiding complex striping in rough. This agent's direct page fetch was unavailable; no direct quote is asserted.
- [Pinehurst No.2 restoration](https://www.pinehurst.com/news/the-pinehurst-no-2-restoration-a-hole-by-hole-tour/): primary text explicitly documents elimination of rough, replacement of irrigated turf by natural sand/wiregrass/pine straw/native plants, and two cutting heights. That site's management is not a prescription for every course. Our existing dry-course physics regions remain unchanged; their native-ground colors retain the dry setting rather than borrowing coastal lushness.

## Implementation

One centralized palette applies to original managed Surface IDs: Green, Tee, Fairway, Fringe and Rough. Coastal, temperate, tropical, cold, dry and volcanic settings remain distinct. Sand, Desert, Snow, Ash, Lava, hazards, colliders and analytic lie regions are untouched. Five-meter fairway bands are broad and subdued; tees are quieter, greens are fine and almost unstriped, fringe is deeper, and rough has continuous irregular growth patches and warmer tips rather than mowing stripes.

Dense15 physical grass geometry remains accepted and unchanged: a bounded12m tee patch onhole12. Greens use tightly mown texture response and never tall Dense15 fans. Broader physical grass rollout is proposed separately, not certified by this material pass.

Actual Hierarchy16 captured six settings and passes compile/render44.09s, with hole12 full audit identical to Dense15. Parent accepts meaningful hierarchy improvement; putting texture is being refined in staged Green17 because the first muted green looked too smooth/felt-like. Green17 subsequently passed compile/render19.29s and root actual visual review: fine turf grain is visible while the muted palette remains. Material/art acceptance is bounded and not full reference equivalence.
