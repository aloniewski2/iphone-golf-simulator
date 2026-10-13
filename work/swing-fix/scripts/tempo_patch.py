"""Apply the swing-fix tempo to a GolferView.cs in place (only these lines; the rest of the file is left as it is)."""
import sys
p = sys.argv[1]; s = open(p).read()
old_c = '''                // again made the club hang at the top and then snap, or stop short of the finish. 0.38 s down is about twice the studio pace at the ball, and still readable at 60 fps.
                "HalfSwing" => (0.34f, 1.0f, 1.0f),
                _ => (0.38f, 1.0f, 1.0f),'''
new_c = '''                // again made the club hang at the top and then snap, or stop short of the finish. Tour players come down from the top in 0.19-0.23 s (measured on Adnan's reference
                // swings, work/swing-compare); 0.25 s is that pace with a frame to spare at 60 fps (it was 0.38 s, and the swing read as floaty). A pitch comes down a touch slower.
                "HalfSwing" => (0.26f, 1.0f, 1.0f),
                _ => (0.25f, 1.0f, 1.0f),'''
if new_c in s: print("already patched"); sys.exit(0)
assert s.count(old_c) == 1, "tempo lines not found"
open(p, "w").write(s.replace(old_c, new_c)); print("patched", p)
