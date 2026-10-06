import { Blockfrost, Lucid, LucidEvolution, Network, getAddressDetails } from '@lucid-evolution/lucid';

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
