import json, sys
r = json.load(open(sys.argv[1]))
for piece, clips in r.items():
    main = [k for k in clips if any(t in k for t in ('ReadyIdle', 'Forehand', 'Backhand', 'Serve', 'Volley', 'Overhead', 'Run', 'Walk', 'Return', 'Slice', 'Split', 'Recover'))]
    print(piece, 'sum of worst-frame poke points: all clips %d, main tennis clips %d; clips with > 20 points: %d/%d' % (
        sum(v['verts'] for v in clips.values()), sum(clips[k]['verts'] for k in main), sum(1 for v in clips.values() if v['verts'] > 20), len(clips)))
