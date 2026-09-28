# Party mechanics and character direction — research bookmark

Date: 2026-09-27. Status: research/proposals, not approved implementation.
User request: bookmark lob/smash opportunities; investigate signature mechanics, pre-match ultimates/perks, and human-like versus fictional characters.
No game code, character assets, animations, or art-bible rules changed.

## Bookmarked: earned overhead-smash opportunities

Core loop: place a strong shot → force a scramble → opponent lifts a short defensive lob → advance into position → overhead smash.

Situations to add later:
- Defensive lob when stretched wide or pushed deep. Good execution escapes deep; poor execution can land short.
- Intentional lob against a net approach. A successful lob clears the attacker; an underhit lob becomes an overhead opportunity.
- Occasional lifted low-ball scramble return. This is a game-design inference from racket angle and lifting mechanics, not a rule that every late contact pops up.
- Topspin lobs as a later intentional option.

Eligibility: predict overhead contact above and slightly in front of the player, with enough time to reach it. Height alone is insufficient. Deep lobs should force retreat; shoulder-height balls remain volleys/groundstrokes. Suggested readable landing marker and contextual SMASH prompt.

Sources:
- [USTA tennis definitions](https://www.usta.com/en/home/improve/tips-and-instruction/national/tennis-terms-definitions.html)
- [USTA overhead positioning](https://www.usta.com/en/home/improve/tips-and-instruction/national/improve-your-tennis-game--tips-for-mastering-the-overhead.html)
- [Katrina Adams on offensive/defensive lobs](https://www.tennis.com/news/articles/master-every-stroke-lob)
- [ActiveSG lob technique](https://www.activesgcircle.gov.sg/learn/tennis/how-to-execute-a-tennis-lob)

## Observed reference patterns

The grab/dive reference is Fall Guys, not the standard Among Us loop. Among Us centers on teamwork, hidden opposition, and social deduction.

| Reference | Documented pattern | Proposed lesson for this game |
|---|---|---|
| Fall Guys | Dive/grab actions; later piggyback cooperation; recovery changes to reduce excessive ragdoll interruption | One expressive action should make saves, failures, and social stories; funny failure needs quick recovery |
| Mario Tennis Aces | Rally/trick-shot energy, special attacks and defensive energy spending | A visible meter creates decisions when defense and attack compete for it |
| Rocket League Rumble | Timed randomized power-ups | Modifier events can add chaos to a familiar sport; randomness is a design choice, not a prerequisite |
| Among Us | Teamwork and betrayal among identifiable players | Make it clear who caused a memorable event; deception does not need importing into tennis |

Sources:
- [Fall Guys Free for All release notes: dive grabs](https://www.fallguys.com/news/fall-guys-free-for-all-release-notes)
- [Fall Guys Fall Forever: piggyback and recovery changes](https://www.fallguys.com/news/fall-forever-update)
- [Nintendo Mario Tennis Aces gameplay](https://www.nintendo.com/en-gb/games/oms/mario-tennis-aces/gameplay/?a=gameplay)
- [Nintendo Mario Tennis Aces tips](https://play.nintendo.com/news-tips/tips-tricks/mario-tennis-aces-tips-tricks/)
- [Epic: Rumble mode](https://www.epicgames.com/help/c-37599050/c-32343914/a24486133?lang=en-US)
- [Innersloth: Among Us](https://www.innersloth.com/games/among-us/)

## Recommended prototype: Scramble → Showboat

These are original proposals assembled from the patterns above. Novelty/exclusivity is not established.

### Universal move: Scramble

A contextual emergency reach/dive with a short cooldown. Player deliberately activates it; successful contact still requires timing. Use a thumb button on the phone; the physical player does not need to dive or throw the phone.

- Extends coverage briefly but creates a weak high return, connecting directly to the bookmarked lob/smash loop.
- A miss becomes a short slide/tumble; a successful save leaves a short recovery commitment.
- Do not allow repeated automatic rescues. Avoid rewarding whiffs or deliberate sandbagging with free meter.
- Later doubles variation: consenting teammate boost or help-up, only after the solo action proves fun. No grabbing opponents across a tennis net.

### Resource: shared-cost Showboat meter

Charge through capped meaningful rally contributions and successful difficult saves. Avoid making perfect-hit skill the only source, which would amplify the stronger player's lead. No charge from repeated empty gestures.

Choose one ultimate before the match; spend the full meter deliberately. All options must be available equally and have an obvious tell/counter. Starting candidates:

| Ultimate | Behavior | Counter/tradeoff |
|---|---|---|
| Skybreaker | Enhances an earned overhead with a dramatic jump and more aggressive angle | Requires a reachable high ball; target direction telegraphed; defender can position and return |
| Rescue Lob | Extends a single desperate reach and launches a high recovery lob | Contact still required; underhit return remains smashable; consumes offensive meter opportunity |
| Curveball | One clearly signaled stronger-curving shot | Predictable curve direction shown before contact; sacrifice raw pace |

Prototype Skybreaker + Rescue Lob first so there is a meaningful attack/defense choice. Numerical charge rates, speed caps, and cooldowns need playtesting. Avoid mid-rally cinematic cuts that hide opponent decisions.

### One pre-match perk initially

Start with visible sidegrades rather than flat stat upgrades:

| Perk | Benefit | Cost |
|---|---|---|
| Long Reach | Longer Scramble travel | Longer landing recovery |
| Quick Feet | Faster recovery after Scramble | Shorter Scramble distance |
| Lob Artist | More controllable defensive lob placement | Reduced drive pace |

All three are unvalidated balance hypotheses. Same option access for every player, no purchase/level upgrades, and no character/body-size gameplay advantage. Keep loadouts visible in the lobby.

This extends earlier cosmetics-only gameplay direction with equal-access selectable abilities, while retaining identity-only cosmetics and no power progression. The user requested research into this extension; it is not an approved shipped design.

Existing local brief `PLAN2B_CinematicSpectacle_Ultimate.md` already proposes equal-access ultimates, contact-honest presentation, and short cinematics. This research adds a proposed interactive role/loadout choice. The brief's existence does not establish runtime implementation status.

## Character direction: human avatars vs fictional mascots

Evidence:
- Nintendo Switch Sports supports Sportsmates and Miis, with personal-style customization.
- Nintendo's 2026 Tomodachi developer interview describes preserving familiar Mii features, emotional attachment, and deliberately stylized movements. This is evidence about that team's design intent, not proof of conversion or retention lift for our game.
- Fall Guys supports highly fantastical costumes on a recognizable bean body; food-themed costumes demonstrate the range.
- Among Us uses its recognizable crewmates for a social-deduction game. Mascot simplicity and that game's success do not establish a causal relationship.

| Direction | Expected strengths (design judgment) | Risks/costs (design judgment) |
|---|---|---|
| Human-inspired caricature | Recognize yourself/friends; face/hair/skin customization; readable racket technique | Hands, joints, and facial quality draw scrutiny; generic athletes may lack a signature silhouette |
| Entirely fictional mascot | Distinct silhouette; absurd costumes and exaggerated reactions fit naturally | Less facial resemblance to friends; short/absent arms can make two-hand sports grips harder; redesign/rig cost |
| Personalized toy athlete | Human identity cues plus a consistent exaggerated body and comic behavior | Needs strong shape language to avoid generic chibi presentation |

Recommendation: retain the locked toy-athlete V4 for the mechanic prototype. Long-term favor personalized toy athletes: recognizable faces/hair/skin plus a stronger original silhouette, oversized gear, expressive reactions, and optional mascot costumes. This is a recommendation for evaluation, not permission to change V4 or reopen art.

Sources:
- [Nintendo on Sportsmates/Miis](https://www.nintendo.com/us/whatsnew/use-your-mii-characters-in-these-games/)
- [Nintendo developer interview: Mii identity and movement](https://www.nintendo.com/us/whatsnew/ask-the-developer-vol-21-tomodachi-life-living-the-dream-part-1/)
- [Fall Guys food-themed costumes](https://www.fallguys.com/news/fall-guys-tool-up-update)

No reviewed source establishes that fictional mascots outperform human-like avatars overall, or that either visual category caused virality.

## Small validation plan, before production scope expands

1. Compare baseline tennis with tennis + Scramble, using the same V4 hero.
2. Add Skybreaker/Rescue Lob only if Scramble produces readable, fun save→smash exchanges.
3. Add one perk slot after the core action reads without explaining a build system.
4. Observe: deliberate repeat use, successful recovery, involuntary downtime, ability counter comprehension, voluntary rematches, and remembered funny moments. Include new players and test the phone/controller setup.
5. For a later character-direction study, show matched gameplay/framing with different character concepts; ask both self-expression preference and silhouette recognition. Do not compare a polished mascot to an unfinished human and infer a category winner.

Next decision: choose the first mechanic prototype; no implementation authorized by this research note.
