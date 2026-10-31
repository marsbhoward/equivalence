import { defineRailway, postgres, project, service, volume } from "railway/iac";

// The deploy settings railway.json held - never applied, since no service read Config as Code (2026-10-31).
// Both services alike: node directly (SIGTERM reaches it, src/lifecycle.ts drains for 25 s inside the 30),
// /health gates a deploy, a crash restarts (ON_FAILURE is Railway's default, which it stores as
// null - naming it left a change in every plan). Variables stay unmanaged: IaC never reads or writes a secret.
const deploy = {
  startCommand: "node dist/index.js",
  healthcheckPath: "/health",
  healthcheckTimeout: 60,
  restartPolicyMaxRetries: 3,
  drainingSeconds: 30,
} as const;

export default defineRailway(() => {
  const Postgres = postgres("Postgres", { region: "sfo" });
  Postgres.networking = { privateNetworkEndpoint: "postgres" };
  const postgresVolume = volume("postgres-volume", { alerts: { usage: { "100": {}, "80": {}, "95": {} } }, allowOnlineResize: true, region: "sfo", sizeMB: 5000 });
  const equivalenceServiceVolume = volume("equivalence-service-volume", { alerts: { usage: { "100": {}, "80": {}, "95": {} } }, allowOnlineResize: true, region: "sfo", sizeMB: 5000 });
  const equivalenceShowcaseVolume = volume("equivalence-showcase-volume", { alerts: { usage: { "100": {}, "80": {}, "95": {} } }, allowOnlineResize: true, region: "sfo", sizeMB: 5000 });
  const equivalenceShowcase = service("equivalence-showcase", {
    build: { buildEnvironment: "V3", builder: "RAILPACK", buildCommand: "npm run build" },
    deploy: deploy,
    replicas: { "sfo": 1 },
    volumeMounts: { "/data": equivalenceShowcaseVolume },
  });
  const equivalenceService = service("equivalence-service", {
    build: { buildEnvironment: "V3", builder: "RAILPACK", buildCommand: "npm run build" },
    deploy: deploy,
    replicas: { "sfo": 1 },
    volumeMounts: { "/data": equivalenceServiceVolume },
  });

  return project("equivalence-service", {
    variables: { managed: false },
    resources: [equivalenceShowcase, equivalenceService, Postgres, postgresVolume, equivalenceServiceVolume, equivalenceShowcaseVolume],
  });
});
