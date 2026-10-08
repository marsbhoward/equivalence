// PLACEHOLDER. Build web/connector/equivalence-chain.ts over this file:
//
//   npx esbuild web/connector/equivalence-chain.ts \
//     --bundle --format=iife --outfile=Assets/WebGLTemplates/Equivalence/connector/equivalence-chain.js
//
// Deliberately does NOT assign window.EquivalenceChain. StoreFactory checks for it once and
// falls back to local storage when it is missing, so an unbuilt connector is a playable game
// on local saves rather than a broken one - which is the state you want while the TypeScript
// half is still being written.
console.info('[Equivalence] connector placeholder - running on local storage, nothing will reach the chain');
