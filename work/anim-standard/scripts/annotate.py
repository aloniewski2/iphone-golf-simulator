"""Add circles to breakdown_annotations.json in DISPLAY coordinates of a review page (as I read it), converted to page pixels.
  python3 annotate.py <context>/<page_key> <scale> "x,y,r,label" ["x,y,r,label" ...]       (page_key = file name without .jpg)
  python3 annotate.py <context>/<page_key> none        -> page reviewed, no issues"""
import json, os, sys
A = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'breakdown_annotations.json')
ann = json.load(open(A))
key, scale = sys.argv[1], sys.argv[2]
items = ann.get(key, [])
if scale != 'none':
    k = float(scale)
    for a in sys.argv[3:]:
        x, y, r, label = a.split(',', 3)
        items.append([round(float(x) * k), round(float(y) * k), round(float(r) * k), label.strip()])
ann[key] = items
json.dump(ann, open(A, 'w'), indent=0)
print(key, len(items), 'circles')
