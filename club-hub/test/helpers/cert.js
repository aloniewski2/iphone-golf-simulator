// Builds a minimal self-signed X.509 certificate (DER) around a given RSA key pair, so the Apple-signature tests exercise the
// real X509Certificate parsing path. Node has no certificate generator; this is just enough ASN.1 for the parser to accept it.
import { sign } from 'node:crypto';

const len = (n) => (n < 128 ? Buffer.from([n]) : n < 256 ? Buffer.from([0x81, n]) : Buffer.from([0x82, n >> 8, n & 255]));
const tlv = (tag, ...parts) => { const body = Buffer.concat(parts); return Buffer.concat([Buffer.from([tag]), len(body.length), body]); };
const seq = (...p) => tlv(0x30, ...p);
const set = (...p) => tlv(0x31, ...p);
const int = (n) => tlv(0x02, Buffer.from([n]));
const oid = (s) => {
  const a = s.split('.').map(Number);
  const out = [40 * a[0] + a[1]];
  for (const x of a.slice(2)) {
    const chunk = [x & 127];
    for (let v = x >> 7; v; v >>= 7) chunk.unshift((v & 127) | 128);
    out.push(...chunk);
  }
  return tlv(0x06, Buffer.from(out));
};
const utc = (d) => tlv(0x17, Buffer.from(d.toISOString().replace(/[-:T]/g, '').slice(2, 14) + 'Z'));

export function selfSignedCert(privateKey, publicKey, { notBefore = new Date(Date.now() - 86_400_000), notAfter = new Date(Date.now() + 365 * 86_400_000) } = {}) {
  const alg = seq(oid('1.2.840.113549.1.1.11'), tlv(0x05));
  const name = seq(set(seq(oid('2.5.4.3'), tlv(0x0c, Buffer.from('club-hub test')))));
  const tbs = seq(tlv(0xa0, int(2)), int(1), alg, name, seq(utc(notBefore), utc(notAfter)), name, publicKey.export({ type: 'spki', format: 'der' }));
  const sig = sign('sha256', tbs, privateKey);
  return seq(tbs, alg, tlv(0x03, Buffer.concat([Buffer.from([0]), sig])));
}
