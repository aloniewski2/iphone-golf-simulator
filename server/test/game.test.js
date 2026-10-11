import assert from 'node:assert/strict';
import test from 'node:test';
import { COURSES, addRound, cleanName, coursePar, emptyStats, matchResults, toPar, validCard, validLook } from '../src/game.js';

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
  assert.deepEqual(COURSES.cliffside.pars, [4, 4, 5, 3, 3]);
  assert.deepEqual(COURSES['cliffside-12'].pars, [3]);
  assert.deepEqual(COURSES.wildisles.pars, [5, 4, 5, 3, 4, 4]);
  assert.deepEqual(COURSES['wildisles-18'].pars, [3]);
  assert.deepEqual(COURSES.magma.pars, [4, 4, 3, 4, 5]);
  assert.deepEqual(COURSES['magma-23'].pars, [5]);
  assert.equal(COURSES['cliffside-99'], undefined);
});

test('three courses own the holes: 7, 8, 9, 12, 15 / 13, 14, 17–20 / 10, 16, 21–23', () => {
  assert.deepEqual(Object.keys(COURSES).filter((id) => !id.includes('-')), ['cliffside', 'wildisles', 'magma']);
  const holesOf = (key) => Object.keys(COURSES).filter((id) => id.startsWith(`${key}-`)).map((id) => Number(id.split('-')[1]));
  assert.deepEqual(holesOf('cliffside'), [7, 8, 9, 12, 15]);
  assert.deepEqual(holesOf('wildisles'), [13, 14, 17, 18, 19, 20]);
  assert.deepEqual(holesOf('magma'), [10, 16, 21, 22, 23]);
  assert.deepEqual(['cliffside', 'wildisles', 'magma'].map(coursePar), [19, 25, 20]);
  assert.equal(COURSES['magma-10'].pars[0], 4, 'hole 10 moved to Magma Open');
  assert.equal(COURSES['cliffside-10'], undefined);
  assert.equal(COURSES['cliffside-13'], undefined, 'hole 13 moved to Wild Isles');
  assert.deepEqual(COURSES['wildisles-13'].pars, [5]);
  assert.equal(COURSES['wildisles-16'], undefined, 'hole 16 moved to Magma Open');
  assert.deepEqual(COURSES['magma-16'].pars, [4]);
  assert.equal(COURSES.postcards, undefined, 'the retired Postcards course is like any unknown course');
  assert.ok(!validCard('postcards', [4]));
});

test('a card has one score per hole of a known course', () => {
  assert.ok(validCard('cliffside-7', [4]));
  assert.ok(validCard('cliffside', [4, 4, 5, 3, 3]));
  assert.ok(validCard('wildisles', [5, 4, 5, 3, 4, 4]));
  assert.ok(!validCard('wildisles', [5, 4, 5, 3, 4]), 'Wild Isles has six holes');
  assert.ok(validCard('magma', [4, 4, 3, 4, 5]));
  assert.ok(!validCard('magma', [4, 4, 3]), 'Magma Open has five holes');
  assert.ok(!validCard('cliffside', [4, 3]));
  assert.ok(!validCard('cliffside', [4, 0, 5, 4, 3]));
  assert.ok(!validCard('nowhere', [4]));
  assert.equal(toPar('cliffside', [3, 3, 5, 4, 3]), -1);
});

test('stats add up the way the app counts them', () => {
  let s = addRound(emptyStats(), 'cliffside', [3, 4, 5, 4, 1]); // birdie, par, par, bogey, ace
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
