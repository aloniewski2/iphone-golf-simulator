# Actual Unity gameplay — permanent standard golfers

- [Male standard, 60 fps](standard-male-unity-60fps.mp4)
- [Female standard, 60 fps](standard-female-unity-60fps.mp4)

Each video is 6 seconds, 360 frames, 720×480, decoded and verified at 60 fps. These are captures of the actual Golf scene in Unity 6000.3.24f1, not Blender renders. Simulated phone input passes through the normal swing detector and ball-flight logic. The standard golfers replace the earlier runtime character models, with the V4 drive and held driver.

The review uses deterministic simulation time to render each frame. It is not a real-time frame-rate benchmark or a physical-iPhone motion test. Only the right-handed drive and first golf kit are integrated here; the rest of the V4 library remains source material pending integration. Animation/clothing polish remains ongoing.

Validation: 42 EditMode tests and 3 PlayMode tests passed. The standard-character test checks both real skinned models, contact marker presence, geometry bounds, and a transition into ball flight. A non-deterministic offline capture attempt failed because render time affected the synthetic input clock; the verified capture uses a fixed simulation clock without changing live phone-input timing.
