from pathlib import Path
p=Path('Unity/Assets/Editor/MatchHeroLockerExport.cs');s=p.read_text()
s=s.replace('public float[] boundsMin, boundsMax; }','public float[] boundsMin, boundsMax, times; }')
a='''                // loop seam: how far the last frame is from the first (positions in mm, rotation in degrees)'''
b='''                // Keep the 60 Hz base samples. Abrupt authored cuts need extra guard samples so interpolation
                // does not pull the racket off its socket early or smear a one-frame hand transition.
                float baseFps = n / clip.length; float[] sampleTimes = null;
                if (!loop && id != "serve") {
                    var times = new List<float>(); var refined = new List<float>();
                    float[] Sample(float time) {
                        clip.SampleAnimation(root, time); var pose = new float[rig.trackCount * 10];
                        for (int tr = 0; tr < rig.trackCount; tr++) {
                            var m = tr < bones.Count ? FlipZ * (inv * bones[tr].localToWorldMatrix) * FlipZ
                                : FlipZ * (inv * rigid[tr - bones.Count].renderer.transform.localToWorldMatrix) * FlipZ * rigidRef[tr - bones.Count];
                            Decompose(m, out var t, out var q, out var scale); int o = tr * 10;
                            pose[o]=t.x; pose[o+1]=t.y; pose[o+2]=t.z; pose[o+3]=q.x; pose[o+4]=q.y; pose[o+5]=q.z; pose[o+6]=q.w; pose[o+7]=scale.x; pose[o+8]=scale.y; pose[o+9]=scale.z;
                        }
                        return pose;
                    }
                    bool Accurate(float[] a, float[] b, float[] actual, float u) {
                        for (int tr = 0; tr < rig.trackCount; tr++) { int o=tr*10;
                            var ta=new Vector3(a[o],a[o+1],a[o+2]); var tb=new Vector3(b[o],b[o+1],b[o+2]); var t=new Vector3(actual[o],actual[o+1],actual[o+2]);
                            var qa=new Quaternion(a[o+3],a[o+4],a[o+5],a[o+6]); var qb=new Quaternion(b[o+3],b[o+4],b[o+5],b[o+6]); var q=new Quaternion(actual[o+3],actual[o+4],actual[o+5],actual[o+6]);
                            if (Vector3.Distance(Vector3.Lerp(ta,tb,u),t)>.001f || Quaternion.Angle(Quaternion.Slerp(qa,qb,u),q)>.7f) return false;
                        }
                        return true;
                    }
                    void Append(float time, float[] pose) { times.Add(time); refined.AddRange(pose); }
                    void Refine(float aTime, float[] aPose, float bTime, float[] bPose, int depth) {
                        float mid=(aTime+bTime)*.5f; var m=Sample(mid);
                        bool accurate=Accurate(aPose,bPose,m,.5f) && Accurate(aPose,bPose,Sample(Mathf.Lerp(aTime,bTime,.25f)),.25f) && Accurate(aPose,bPose,Sample(Mathf.Lerp(aTime,bTime,.75f)),.75f);
                        if (accurate || depth >= 8) { Append(bTime,bPose); return; }
                        Refine(aTime,aPose,mid,m,depth+1); Refine(mid,m,bTime,bPose,depth+1);
                    }
                    var previous=Sample(0); Append(0,previous);
                    for(int k=1;k<frames;k++) { float at=k*clip.length/n; var next=Sample(at); Refine((k-1)*clip.length/n,previous,at,next,0); previous=next; }
                    sampleTimes=times.ToArray(); data=refined.ToArray(); frames=sampleTimes.Length; n=frames-1;
                    sb.AppendLine($"  clip {id}: {frames} total samples including 60 Hz base + cut guards");
                }
                // loop seam: how far the last frame is from the first (positions in mm, rotation in degrees)'''
assert a in s;s=s.replace(a,b)
s=s.replace('fps = n / clip.length, contact = slot.contact, frames = frames, offset = blob.Count,','fps = baseFps, contact = slot.contact, frames = frames, offset = blob.Count, times = sampleTimes,')
s=s.replace('float t = Mathf.Min(clip.length, k * clip.length / (ci.frames - 1));','float t = ci.times != null ? ci.times[k] : Mathf.Min(clip.length, k * clip.length / (ci.frames - 1));')
p.write_text(s)
p=Path('GolfArcade/Unity/MatchHeroData.swift');s=p.read_text();s=s.replace('let boundsMin: [Float], boundsMax: [Float]\n    }','let boundsMin: [Float], boundsMax: [Float]\n        let times: [Float]?\n    }');s=s.replace('''            let i0 = min(n, Int(f.rounded(.down))), i1 = min(n, i0 + 1), w = Float(f - Double(i0))''','''            var i0 = min(n, Int(f.rounded(.down))), i1 = min(n, i0 + 1), w = Float(f - Double(i0))
            if let times = info.times, times.count == frames {
                let t = Float(min(max(time,0),length))
                var low = 0, high = n
                while low < high { let mid = (low+high+1)/2; if times[mid] <= t { low=mid } else { high=mid-1 } }
                i0=low; i1=min(n,low+1); w=i0 == i1 ? 0 : (t-times[i0])/max(0.000001,times[i1]-times[i0])
            }''');p.write_text(s)
p=Path('GolfArcadeTests/LockerMirrorTests.swift');s=p.read_text().replace('XCTAssertEqual(clip.info.fps,60)','XCTAssertEqual(clip.info.fps,60,accuracy:0.001)');s=s.replace('''                for time in [0,clip.length] {
                    for (p,r) in zip(clip.pose(at:time,loop:false),ready) {''','''                let motion = MenuMotion(kind:.clipOnce(id),lead:0,fadeIn:0.12,fadeOut:0.2)
                for time in [0,clip.length+0.2] {
                    let readyAtTime = try XCTUnwrap(data.clips["ready"]).pose(at:time,loop:true)
                    for (p,r) in zip(motion.pose(data,at:time),readyAtTime) {''').replace('            let ready = try XCTUnwrap(data.clips["ready"]).pose(at:0,loop:false)\n            for id','            for id');p.write_text(s)
