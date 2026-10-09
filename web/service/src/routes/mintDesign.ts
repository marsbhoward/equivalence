import type { Request, Response } from 'express';
import type { TxBuilder } from '@lucid-evolution/lucid';
import { fundingInputs, getLucid, serialized, serviceAddress, signAndSubmit } from '../chain.js';
import { getGearPolicy } from '../gearPolicy.js';
import { ART_KEY, ArtEntry } from '../artIndex.js';
import { bf } from '../blockfrost.js';
import { REF_LABEL, USER_LABEL, isUserUnit, nameBody, referenceDatum, unitOf } from '../cip68.js';
import { resolveArt } from './art.js';

/**
 * Mints Diamond / Black Diamond DESIGNS as CIP-68 pairs: per piece, the reference token (label
 * 100, the metadata in its inline datum) to the SERVICE's own address, the user token (label
 * 222) to the player. Service-signed; the player signs nothing. Several pieces go in ONE
 * transaction - a Black Diamond weapon and its relic are one payout and land together or not at
 * all.
 *
 * Only designs mint. Bronze/Silver/Gold are ROLLS that do not trade - they live in the character's
 * own datum (`CharacterProfile.MintedGear`), never as tokens of their own. Designs carry no stats,
 * so nothing here is a roll: a design id, its dyes, its art, and who minted it.
 *
 * Two callers: the operator route below (`POST /op/mint-design`, WRITE_TOKEN), and the player's
 * box redemption (routes/player.ts), which rolls the design on the server first.
 *
 * Bumping SCHEMA means the datum's shape changed - readers branch on `extra.schema`.
 */
const SCHEMA = 1;

const TIERS = new Set(['Diamond', 'BlackDiamond']);
const SLOTS = new Set([
  'Head', 'Shoulders', 'Torso', 'Back', 'Neck', 'Ring', 'Legs', 'Trinket', 'Boots', 'Belt',
  'Gloves', 'Weapon', 'Relic',
]);
const ID = /^[a-z0-9_]{1,64}$/i;
/** A swatch id as `DyeCatalog` names it (without `dye.`), or `-` for a channel left as authored. */
const DYE = /^([a-z0-9_]{1,32}|-)$/i;

export interface MintPiece {
  /** `MintedGearRecord.InstanceId` - names the token (see cip68.nameBody). */
  instanceId: string;
  /** The authored design id (`MintedGearRecord.Design`). */
  design: string;
  /** What wallets show (`MintedGearRecord.DisplayName`). */
  name: string;
  tier: string;
  slot: string;
  /** Weapons only (`WeaponClass`): a marketplace trait. */
  class?: string;
  /** Black Diamond relics only: the locked finisher, the relic's mechanic. */
  signature?: string;
  /** Swatch id per dye channel, `-` = as authored. Omitted = undyed. */
  dyes?: string[];
  /** The drawn item's id - see artIndex.ts. Every design has a picture. */
  artKey: string;
}

export interface Minted {
  instanceId: string;
  design: string;
  unit: string;
  reference: string;
}

export class MintRefused extends Error {
  constructor(readonly status: number, readonly body: Record<string, unknown>) {
    super(String(body.error));
  }
}

export function invalidPiece(p: Partial<MintPiece>): string | null {
  if (!p.instanceId || p.instanceId.length > 128) return 'instanceId is required (<= 128 chars)';
  if (!p.design || !ID.test(p.design)) return 'design must be an authored design id';
  if (!p.name || Buffer.byteLength(p.name) > 64) return 'name is required (<= 64 bytes)';
  if (!p.tier || !TIERS.has(p.tier)) return 'tier must be Diamond or BlackDiamond - only designs mint';
  if (!p.slot || !SLOTS.has(p.slot)) return 'slot must be a GearSlot name';
  if (p.class !== undefined && (p.slot !== 'Weapon' || !ID.test(p.class))) return 'class is for weapons only';
  if (p.signature !== undefined && (p.tier !== 'BlackDiamond' || !ID.test(p.signature)))
    return 'signature is for Black Diamond pieces only';
  if (p.dyes !== undefined && (!Array.isArray(p.dyes) || p.dyes.length > 2 || !p.dyes.every((d) => DYE.test(d))))
    return 'dyes must be up to two swatch ids (or -)';
  if (!p.artKey || !ART_KEY.test(p.artKey)) return 'artKey is required';
  return null;
}

/**
 * THE PFP GATE. Minting gear needs a PFP, and the PFP mint is what sends the character's user
 * token to the player - so the player's address holding that token proves both that the
 * character has a PFP and that this player owns the character they name as `mintedBy`.
 *
 * `PFP_GATE=off` skips it for testing BEFORE character tokens exist, and is refused outright on
 * mainnet.
 */
export async function pfpGate(address: string, character: string): Promise<string | null> {
  if (!isUserUnit(character)) return 'character must be a CIP-68 user-token (222) unit';
  if (process.env.PFP_GATE === 'off') {
    if ((process.env.NETWORK ?? '').toLowerCase() === 'mainnet') return 'PFP_GATE=off is refused on mainnet';
    console.warn(`[mint] PFP gate is OFF - not checking ${address} holds ${character}`);
    return null;
  }
  const utxos = await bf<unknown[]>(`/addresses/${address}/utxos/${character}`);
  return utxos && utxos.length > 0 ? null : 'this address holds no PFP for that character';
}

/**
 * The checks that cost nothing on chain, for every piece, BEFORE a caller spends anything: art
 * pinned, instance not already minted. Throws MintRefused.
 */
export async function preflight(pieces: MintPiece[]): Promise<Map<string, ArtEntry>> {
  const { policyId } = await getGearPolicy();
  const art = new Map<string, ArtEntry>();
  for (const p of pieces) {
    const entry = art.get(p.artKey) ?? (await resolveArt(p.artKey));
    if (!entry) throw new MintRefused(409, { error: 'art_needed', artKey: p.artKey });
    art.set(p.artKey, entry);
    // The policy is "signed by the service" and nothing else, so it would happily mint a second
    // copy of the same instance - this check is what keeps a design instance one of one.
    const user = unitOf(policyId, nameBody(p.instanceId), USER_LABEL);
    if (await bf(`/assets/${user}`)) throw new MintRefused(409, { error: 'already_minted', unit: user });
  }
  return art;
}

/** Builds, signs and submits one transaction minting every piece. Run `preflight` first. */
/**
 * Adds every piece's CIP-68 pair to `tx`: the reference token (metadata datum) to the service, the
 * user token to `address`. Shared by a plain mint (below) and a box opening, which burns the box
 * in the same transaction (routes/openBoxes.ts). The policy script is attached here.
 */
export async function addPieces(
  tx: TxBuilder,
  address: string,
  character: string,
  pieces: MintPiece[],
  art: Map<string, ArtEntry>,
): Promise<{ tx: TxBuilder; minted: Minted[] }> {
  const { policyId, script } = await getGearPolicy();
  const service = await serviceAddress();

  tx = tx.attach.MintingPolicy(script);
  const minted: Minted[] = [];
  const mint: Record<string, bigint> = {};
  for (const p of pieces) {
    const body = nameBody(p.instanceId);
    const ref = unitOf(policyId, body, REF_LABEL);
    const user = unitOf(policyId, body, USER_LABEL);
    const a = art.get(p.artKey)!;
    mint[ref] = 1n;
    mint[user] = 1n;

    const metadata = {
      name: p.name,
      image: `ipfs://${a.image.cid}`,
      mediaType: 'image/png',
      files: [{ name: 'native', mediaType: 'image/png', src: `ipfs://${a.native.cid}` }],
      design: p.design,
      dyes: p.dyes ?? ['-', '-'],
      art: p.artKey,
      instance: p.instanceId,
      tier: p.tier,
      slot: p.slot,
      ...(p.class ? { class: p.class } : {}),
      ...(p.signature ? { signature: p.signature } : {}),
      // WRITE-ONCE: the minting character, by id. Its name is read live from that character's
      // PFP, so a rename shows on everything it minted. Any later datum rewrite (a dye) copies
      // this forward and never takes it from a request.
      mintedBy: character,
    };
    tx = tx
      .pay.ToAddressWithData(service, { kind: 'inline', value: referenceDatum(metadata, SCHEMA) }, { [ref]: 1n })
      .pay.ToAddress(address, { [user]: 1n });
    minted.push({ instanceId: p.instanceId, design: p.design, unit: user, reference: ref });
  }
  return { tx: tx.mintAssets(mint), minted };
}

/** Builds, signs and submits one transaction minting every piece. Run `preflight` first. */
export async function mintPieces(
  address: string,
  character: string,
  pieces: MintPiece[],
  art: Map<string, ArtEntry>,
): Promise<{ hash: string; minted: Minted[] }> {
  return serialized(async () => {
    const lucid = await getLucid();
    const { tx, minted } = await addPieces(lucid.newTx(), address, character, pieces, art);
    const built = await tx.complete({ presetWalletInputs: await fundingInputs() });
    const hash = await signAndSubmit(built);
    return { hash, minted };
  });
}

/** POST /op/mint-design - the OPERATOR route (WRITE_TOKEN): one piece, fully specified. */
export async function mintDesign(req: Request, res: Response): Promise<void> {
  const b = (req.body ?? {}) as Partial<MintPiece> & { address?: string; character?: string };
  const bad = !b.address?.startsWith('addr') ? 'address is required' : invalidPiece(b);
  if (bad) {
    res.status(400).json({ error: bad });
    return;
  }
  try {
    const refused = await pfpGate(b.address!, b.character ?? '');
    if (refused) {
      res.status(403).json({ error: refused });
      return;
    }
    const piece = b as MintPiece;
    const art = await preflight([piece]);
    const { hash, minted } = await mintPieces(b.address!, b.character!, [piece], art);
    res.json({ hash, unit: minted[0].unit, reference: minted[0].reference });
  } catch (err) {
    if (err instanceof MintRefused) {
      res.status(err.status).json(err.body);
      return;
    }
    // Rejects with a message, never a sentinel - an empty hash is a failure, not a success.
    res.status(500).json({ error: err instanceof Error ? err.message : String(err) });
  }
}
