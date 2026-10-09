import { Blockfrost, CML, Lucid, LucidEvolution, Network, UTxO, getAddressDetails } from '@lucid-evolution/lucid';

/**
 * One Lucid instance for the life of the process, wallet already selected from the service's own
 * seed phrase. Mirrors donada-mint's initLucid() (see MintPlatform.tsx) almost exactly - the
 * Blockfrost + Lucid setup is identical whether this runs in a browser or here in Node. The one
 * thing that changes is wallet selection: donada's browser code calls
 * `lucid.selectWallet.fromAPI(cip30Api)` against a connected wallet extension; there is no browser
 * and no player here, so this calls `fromSeed` against the service's own key instead.
 */
let cached: Promise<LucidEvolution> | null = null;

export function getLucid(): Promise<LucidEvolution> {
  if (cached) return cached;

  const apiKey = requireEnv('BLOCKFROST_API_KEY');
  const url = requireEnv('BLOCKFROST_URL');
  const network = requireEnv('NETWORK') as Network;
  const seed = requireEnv('SERVICE_WALLET_SEED');

  cached = Lucid(new Blockfrost(url, apiKey), network).then((lucid) => {
    lucid.selectWallet.fromSeed(seed);
    return lucid;
  });
  return cached;
}

export async function serviceAddress(): Promise<string> {
  const lucid = await getLucid();
  return lucid.wallet().address();
}

/** The service wallet's own payment key hash - what the gear-minting native script locks to. */
export async function serviceKeyHash(): Promise<string> {
  const address = await serviceAddress();
  const hash = getAddressDetails(address).paymentCredential?.hash;
  if (!hash) {
    throw new Error(
      'service wallet address has no payment key hash - is SERVICE_WALLET_SEED a valid seed phrase?',
    );
  }
  return hash;
}

function requireEnv(name: string): string {
  const value = process.env[name];
  if (!value) throw new Error(`missing required env var ${name}`);
  return value;
}

/**
 * EVERY transaction the service wallet signs is built through here, one at a time.
 *
 * TWO JOBS, both about which of the wallet's coins a build may spend (`fundingInputs`):
 *
 * - NEVER A COIN CARRYING A TOKEN OR A DATUM. The service wallet holds every CIP-68 REFERENCE
 *   token, each with its piece's metadata as an inline datum. Left to itself, coin selection pays
 *   fees from any coin it likes - and a reference-token coin spent that way lands in change
 *   WITHOUT its datum, which erases the piece's metadata. Seen happen to the DONADA test tokens.
 *   So the builder is handed plain ADA only.
 *
 * - NEVER A COIN WE JUST SPENT. Blockfrost keeps listing a coin until the transaction spending it
 *   has been indexed - seconds after it is confirmed - so two builds close together picked the
 *   same coins ("All inputs are spent"): two players redeeming at once. Inputs of every submitted
 *   transaction are remembered for PENDING_MS and left out.
 *
 * Builds are serialised so the second sees the first's inputs. The caller gets its result as
 * soon as the transaction is submitted.
 */
const PENDING_MS = 10 * 60_000;
const pending = new Map<string, number>(); // "txhash#index" -> forget after
let walletQueue: Promise<unknown> = Promise.resolve();

const outRef = (u: { txHash: string; outputIndex: number }) => `${u.txHash}#${u.outputIndex}`;

/** The coins a build may spend: plain ADA, no datum, not already spent by us. */
export async function fundingInputs(): Promise<UTxO[]> {
  const now = Date.now();
  for (const [k, until] of pending) if (until < now) pending.delete(k);
  const lucid = await getLucid();
  const utxos = await lucid.wallet().getUtxos();
  const usable = utxos.filter(
    (u) => !pending.has(outRef(u)) && !u.datum && !u.datumHash && !u.scriptRef &&
      Object.keys(u.assets).every((unit) => unit === 'lovelace'),
  );
  if (usable.length === 0) throw new Error('the service wallet has no free plain-ADA coin to pay with');
  return usable;
}

/** Sets aside the inputs of a built transaction - for one submitted later (a two-signature open). */
export function rememberSpentCbor(cbor: string) {
  rememberSpent(cbor);
}

function rememberSpent(signedCbor: string) {
  const inputs = CML.Transaction.from_cbor_hex(signedCbor).body().inputs();
  const until = Date.now() + PENDING_MS;
  for (let i = 0; i < inputs.len(); i++) {
    const input = inputs.get(i);
    pending.set(`${input.transaction_id().to_hex()}#${input.index()}`, until);
  }
}

/** Sign with the service wallet, remember what it spends, submit. */
export async function signAndSubmit(tx: { sign: { withWallet(): { complete(): Promise<{ toCBOR(): string; submit(): Promise<string> }> } } }): Promise<string> {
  const signed = await tx.sign.withWallet().complete();
  rememberSpent(signed.toCBOR());
  return signed.submit();
}

export function serialized<T>(build: () => Promise<T>): Promise<T> {
  const run = walletQueue.then(build);
  walletQueue = run.catch(() => {});
  return run;
}
