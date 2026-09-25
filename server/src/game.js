// The game's rules the server needs to check what a phone sends: the courses (pars per hole,
// the same as Unity/Assets/Scripts/Course/Hole.cs), profile limits, and the stats a finished
// card adds up to (the same arithmetic as ProfileStats.RecordRound in the app).

/** Each course's holes (number, par), in the order they're played: Hole.cs Course.All(). */
const COURSE_HOLES = {
  cliffside: { name: 'Cliffside', holes: [[7, 4], [12, 3], [13, 5], [14, 4], [15, 3]] },
  wildisles: { name: 'Wild Isles', holes: [[19, 4], [20, 5], [21, 3], [22, 4], [23, 4]] },
};

/** What a round can be: a course's key for its whole round ("cliffside"), or key-number for
 * one hole of it ("cliffside-12"): GameSetup.CourseIdFor in the app. */
export const COURSES = Object.fromEntries(Object.entries(COURSE_HOLES).flatMap(([key, { name, holes }]) => [
  [key, { name, pars: holes.map(([, par]) => par) }],
  ...holes.map(([number, par]) => [`${key}-${number}`, { name: `${name} · hole ${number}`, pars: [par] }]),
]));

export const MAX_NAME_LENGTH = 16;
export const BODY_KINDS = 2; // GolferStyle.BodyKind: male, female
export const COLOURS = 6; // GolferStyle.KitColors, GolferStyle.ShirtColors
export const MAX_STROKES_PER_HOLE = 30;

export function coursePar(courseId) {
  return COURSES[courseId].pars.reduce((a, b) => a + b, 0);
}

/** Trimmed, control characters removed, at most MAX_NAME_LENGTH; '' when nothing is left. */
export function cleanName(name) {
  if (typeof name !== 'string') return '';
  // eslint-disable-next-line no-control-regex
  const clean = name.replace(/[\u0000-\u001f\u007f]/g, '').trim();
  return [...clean].slice(0, MAX_NAME_LENGTH).join('').trim();
}

/** The golfer (0 male, 1 female), the kit colour and the shirt colour. */
export function validLook(body, kit, shirt) {
  const colour = (c) => Number.isInteger(c) && c >= 0 && c < COLOURS;
  return Number.isInteger(body) && body >= 0 && body < BODY_KINDS && colour(kit) && colour(shirt);
}

/** A finished card for a course: one whole number of strokes per hole, each in range. */
export function validCard(courseId, strokes) {
  const course = COURSES[courseId];
  return !!course && Array.isArray(strokes) && strokes.length === course.pars.length
    && strokes.every((s) => Number.isInteger(s) && s >= 1 && s <= MAX_STROKES_PER_HOLE);
}

export function toPar(courseId, strokes) {
  return strokes.reduce((a, b) => a + b, 0) - coursePar(courseId);
}

export function emptyStats() {
  return {
    roundsPlayed: 0, holesPlayed: 0, totalStrokes: 0,
    bestToPar: null, holesInOne: 0, eagles: 0, birdies: 0, pars: 0,
    matchesWon: 0, matchesTied: 0, matchesLost: 0,
  };
}

/** The stats after one more finished card. `result` is 'won' | 'tied' | 'lost' | null (solo). */
export function addRound(stats, courseId, strokes, result = null) {
  const s = { ...emptyStats(), ...stats };
  const pars = COURSES[courseId].pars;
  s.roundsPlayed += 1;
  strokes.forEach((n, i) => {
    s.holesPlayed += 1;
    s.totalStrokes += n;
    if (n === 1) s.holesInOne += 1;
    else if (n - pars[i] <= -2) s.eagles += 1;
    else if (n - pars[i] === -1) s.birdies += 1;
    else if (n === pars[i]) s.pars += 1;
  });
  const score = toPar(courseId, strokes);
  if (s.bestToPar === null || score < s.bestToPar) s.bestToPar = score;
  if (result === 'won') s.matchesWon += 1;
  else if (result === 'tied') s.matchesTied += 1;
  else if (result === 'lost') s.matchesLost += 1;
  return s;
}

/** Each player's result against the others who finished: lowest to par wins, equal best ties. */
export function matchResults(cards) {
  const results = new Map();
  if (cards.length < 2) {
    for (const c of cards) results.set(c.playerId, null);
    return results;
  }
  for (const c of cards) {
    const others = cards.filter((o) => o !== c).map((o) => o.toPar);
    const best = Math.min(...others);
    results.set(c.playerId, c.toPar < best ? 'won' : c.toPar === best ? 'tied' : 'lost');
  }
  return results;
}
