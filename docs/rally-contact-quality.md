> **Partly superseded by `docs/tennis-feel-pass-2026-10-10.md`**: aiming is now by swing timing, and movement and CPU pace were retuned. The numbers below also lag the code (reach 1.8 m, window 340 ms, quality 80/15/5).

# Rally contact and pace — 2026-09-29

- Assisted reach is 1.6 m at all swing powers (previously 1.05–1.6 m). Swing must have started for at least 40 ms; active assisted-contact window extends to 280 ms from the authored sweet time. Distant balls still require movement/dive; string-to-ball visual contact validation retained.
- Quality grades retain the existing Perfect/Excellent/Great/Good boundaries. The weak-contact tail extends from 210 to 280 ms. Serving rules, aiming and movement speeds unchanged.
- Contact quality: 65% timing, 25% centering, 10% positioning. Pace curve: 13–34 m/s before the existing modest effort/stamina factors. Poor contact produces a slower return.
- Fixed assisted-hit speed being calculated with perfect timing and never recomputed after assigning the real timing/quality. AssistedHit now calculates quality and speed together from measured lateness.
- Verification: 73 EditMode tests passed (rules, scoring, polish), including forgiving hard swings, weak late contacts, bounded reach, monotonically decreasing speed with lateness, and clean soft swings outrunning mistimed hard swings.
- Not installed on phone as part of this change.
