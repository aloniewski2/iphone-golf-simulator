// Read-only provenance/resource audit; does not submit generation jobs.
import {readFileSync, existsSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const root=fileURLToPath(new URL('../',import.meta.url));
const read=path=>readFileSync(root+path);
const json=path=>JSON.parse(read(path));
const hash=path=>createHash('sha256').update(read(path)).digest('hex');
const manifest=json('art/Sunward/integration-manifest.json');
const ledger=json('art/Sunward/generation-ledger.json');
const rebuild=json('art/Sunward/rebuild-ledger.json');
assert.equal(rebuild.jobs.length,3);
assert.equal(new Set(rebuild.jobs.map(j=>j.job_id)).size,3);
assert.equal(rebuild.startingBalance-rebuild.verifiedBalanceAfter,rebuild.verifiedNewCredits);
assert.equal(manifest.credits.lastVerifiedBalance,rebuild.verifiedBalanceAfter);
assert.equal(manifest.credits.spentOnGeneration,851+rebuild.verifiedNewCredits);
assert(manifest.credits.spentOnGeneration<=manifest.credits.ceiling);
for(const name of rebuild.runtimeAssets) assert(existsSync(root+'GolfArcade/Resources/Sunward/'+name));
assert.equal(manifest.generations.length,32);
const submitted=new Set(ledger.entries.map(e=>e.job_id??e.jobId));
const recorded=new Set(manifest.generations.map(e=>e.jobId));
assert.deepEqual(recorded,submitted);
assert.equal(recorded.size,32);
assert.equal(manifest.generations.reduce((s,e)=>s+e.estimatedCredits,0),851);
for(const entry of manifest.generations) {
  assert(existsSync(root+entry.source),entry.source);
  for(const consumer of entry.consumers) assert(existsSync(root+consumer),consumer);
  assert(entry.note && entry.disposition,entry.jobId);
}
const video=manifest.generations.find(e=>e.disposition==='runtime-video');
assert.equal(hash(video.source),hash(video.consumers[0]),'Bundled menu film must be the accepted source');
const motion=json('GolfArcade/Resources/Sunward/SunwardMotion.json');
assert.equal(motion.clips.length,13);
for(const entry of manifest.generations.filter(e=>e.clip)) {
  assert(motion.clips.some(c=>c.name===entry.clip),entry.clip);
}
for(const source of manifest.canonicalAuthoring) assert(existsSync(root+source),source);
for(let hole=1;hole<=9;hole++) assert(existsSync(root+'GolfArcade/Resources/Sunward/Cards/SunwardHole-'+hole+'.png'));
console.log(JSON.stringify({
  generations:32+rebuild.jobs.length,creditsAccountedFor:851+rebuild.verifiedNewCredits,
  newGenerationSpending:rebuild.verifiedNewCredits,motionClips:13,
  menuFilmSHA256:hash(video.consumers[0]),
  golferSHA256:hash('GolfArcade/Resources/Sunward/SunwardGolfer.golfmesh'),
  motionSHA256:hash('GolfArcade/Resources/Sunward/SunwardMotion.json'),
  dispositions:Object.fromEntries([...new Set(manifest.generations.map(e=>e.disposition))].map(d=>
    [d,manifest.generations.filter(e=>e.disposition===d).length]))
},null,2));
