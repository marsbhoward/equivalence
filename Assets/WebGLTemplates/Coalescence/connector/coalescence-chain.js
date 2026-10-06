// PLACEHOLDER. Build web/connector/coalescence-chain.ts over this file:
//
//   npx esbuild web/connector/coalescence-chain.ts \
//     --bundle --format=iife --outfile=Assets/WebGLTemplates/Coalescence/connector/coalescence-chain.js
//
// Deliberately does NOT assign window.CoalescenceChain. StoreFactory checks for it once and
// falls back to local storage when it is missing, so an unbuilt connector is a playable game
// on local saves rather than a broken one - which is the state you want while the TypeScript
// half is still being written.
console.info('[Coalescence] connector placeholder - running on local storage, nothing will reach the chain');
