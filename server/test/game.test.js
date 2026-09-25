import assert from 'node:assert/strict';
import test from 'node:test';
import { COURSES, addRound, cleanName, emptyStats, matchResults, toPar, validCard, validLook } from '../src/game.js';

test('names are trimmed, single-line and at most 16 characters', () => {
  assert.equal(cleanName('  Alex  '), 'Alex');
  assert.equal(cleanName('Al\nex'), 'Alex');
  assert.equal(cleanName('A very long golfer name'), 'A very long golf');
  assert.equal(cleanName('   '), '');
  assert.equal(cleanName(42), '');
});

test('looks are one of the two golfers in one of six kits and six shirts', () => {
  assert.ok(validLook(0, 0, 0));
  assert.ok(validLook(1, 5, 5));
  assert.ok(!validLook(2, 0, 0));
  assert.ok(!validLook(0, 6, 0));
  assert.ok(!validLook(0, 0, 6));
  assert.ok(!validLook(0.5, 1, 1));
});

test('the courses are the round and each of its holes', () => {
  assert.deepEqual(COURSES.cliffside.pars, [4, 3, 5, 4, 3]);
  assert.deepEqual(COURSES['cliffside-12'].pars, [3]);
  assert.deepEqual(COURSES.maplebay.pars, [4, 3, 5]);
  assert.deepEqual(COURSES['maplebay-17'].pars, [3]);
  assert.equal(COURSES['cliffside-99'], undefined);
});

test('a card has one score per hole of a known course', () => {
  assert.ok(validCard('cliffside-7', [4]));
  assert.ok(validCard('cliffside', [4, 3, 5, 4, 3]));
  assert.ok(!validCard('cliffside', [4, 3]));
  assert.ok(!validCard('cliffside', [4, 0, 5, 4, 3]));
  assert.ok(!validCard('nowhere', [4]));
  assert.equal(toPar('cliffside', [3, 3, 5, 4, 3]), -1);
});

test('stats add up the way the app counts them', () => {
  let s = addRound(emptyStats(), 'cliffside', [3, 3, 5, 5, 1]); // birdie, par, par, bogey, ace
  assert.deepEqual(
    [s.roundsPlayed, s.holesPlayed, s.totalStrokes, s.birdies, s.pars, s.holesInOne, s.bestToPar],
    [1, 5, 17, 1, 2, 1, -2]);
  s = addRound(s, 'cliffside', [6, 6, 7, 5, 5], 'lost');
  assert.equal(s.bestToPar, -2, 'a worse round keeps the best');
  assert.equal(s.matchesLost, 1);
  s = addRound(s, 'cliffside-7', [2], 'won');
  assert.equal(s.eagles, 1);
  assert.equal(s.matchesWon, 1);
});

test('the lowest score against par wins and an equal best ties', () => {
  const r = matchResults([{ playerId: 'a', toPar: 1 }, { playerId: 'b', toPar: 1 }, { playerId: 'c', toPar: 3 }]);
  assert.equal(r.get('a'), 'tied');
  assert.equal(r.get('b'), 'tied');
  assert.equal(r.get('c'), 'lost');
  const solo = matchResults([{ playerId: 'a', toPar: 0 }]);
  assert.equal(solo.get('a'), null);
});
