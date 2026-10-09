import { createHash } from 'node:crypto';
import { Constr, Data, fromText, toLabel } from '@lucid-evolution/lucid';

/**
 * CIP-68 pieces: a REFERENCE token (label 100) carrying the metadata as an inline datum, held by
 * the service so only it can rewrite that datum (a dye changes the image), and the USER token
 * (label 222) in the player's wallet. Both share one 28-byte name body.
 */
export const REF_LABEL = 100;
export const USER_LABEL = 222;

const POLICY = /^[0-9a-f]{56}$/;

/**
 * A piece's name body: the first 28 bytes of sha256(instance id), so the game can work out a
 * piece's on-chain unit from its own record (`MintedGearRecord.InstanceId`) with no lookup. A
 * CIP-68 asset name is the 4-byte label + this, exactly the 32-byte cap.
 */
export function nameBody(instanceId: string): string {
  return createHash('sha256').update(instanceId, 'utf8').digest('hex').slice(0, 56);
}

export const unitOf = (policyId: string, body: string, label: number) =>
  policyId + toLabel(label) + body;

/** A label-222 unit's matching reference (label-100) unit - where its datum lives. */
export function referenceOf(userUnit: string): string | null {
  const userPrefix = toLabel(USER_LABEL);
  if (userUnit.length !== 56 + 8 + 56 || !POLICY.test(userUnit.slice(0, 56))) return null;
  if (userUnit.slice(56, 64) !== userPrefix) return null;
  return userUnit.slice(0, 56) + toLabel(REF_LABEL) + userUnit.slice(64);
}

export const isUserUnit = (unit: string) => referenceOf(unit) !== null;

/**
 * JSON-ish metadata -> Plutus data the way CIP-68 wants it: strings as UTF-8 bytes, objects as
 * maps keyed by UTF-8 bytes, arrays as lists, integers as integers. No null in Plutus data - the
 * callers write `-` for "none" (the dye tag's own convention), never null.
 */
type Json = string | number | Json[] | { [k: string]: Json };

function toPlutus(value: Json): Data {
  if (typeof value === 'string') return fromText(value);
  if (typeof value === 'number') {
    if (!Number.isInteger(value)) throw new Error(`metadata number ${value} is not an integer`);
    return BigInt(value);
  }
  if (Array.isArray(value)) return value.map(toPlutus);
  const map = new Map<Data, Data>();
  for (const [k, v] of Object.entries(value)) map.set(fromText(k), toPlutus(v));
  return map;
}

/** The CIP-68 datum: Constr 0 [metadata, version, extra]. `extra.schema` is OUR shape's version. */
export function referenceDatum(metadata: { [k: string]: Json }, schema: number): string {
  return Data.to(
    new Constr(0, [toPlutus(metadata), 1n, toPlutus({ schema })]),
  );
}
