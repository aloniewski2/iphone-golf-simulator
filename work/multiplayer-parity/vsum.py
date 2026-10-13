import json,sys,os
d0=sys.argv[1]
S=[json.load(open(os.path.join(d0,f'seat{s}_summary.json'))) for s in (0,1)]
for d in S:
    print('SEAT',d['seat'],'complete',d['complete'],'final',d['final'],'secs',round(d['seconds']))
    print('  rival strokes',d['rivalStrokes'],'| launch gap mean',round(d['rivalGapMean'],2),'| VISIBLE gap mean/max',round(d.get('visibleGapMean',0),2),round(d.get('visibleGapMax',0),2),'holds',d.get('holds'),'held s',round(d.get('heldSeconds',0),2),'rivalHits',d['rivalHits'])
    print('  calls made/taken',d['callsMade'],d['callsTaken'],'| errors',d['errors'],'exc',d['exceptions'],'| worst frame',round(d['worstFrame'],3),'slow',d['slowFrames'],'| me swings',d['mySwings'],'hits',d['myHits'],'serves',d['myServes'],'dives',d['myDives'],'emotes',d['myEmotes'],'honest misses',d['honestMisses'])
    for e in d['events']:
        if 'Error' in e or 'Exception' in e or 'Versus]' in e: print('   ',e)
a=[x.split(' ',1)[1] for x in S[0]['scores']]; b=[x.split(' ',1)[1] for x in S[1]['scores']]
print('points', len(a), len(b))
def flip(t): return t.replace('YOUR SERVE','X').replace('OPPONENT SERVES','YOUR SERVE').replace('X','OPPONENT SERVES')
import re
agree = len(a)==len(b)
print('SCORE SEQUENCE AGREES' if agree else 'SCORE SEQUENCE DIFFERS')
for i,(x,y) in enumerate(zip(a,b)): print('  ',i,x,'|',y)
