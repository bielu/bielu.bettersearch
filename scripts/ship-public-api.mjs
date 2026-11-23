// Marks the public API as shipped when a stable version is cut: in every src/<project>/, the entries of
// PublicAPI.Unshipped.txt move into PublicAPI.Shipped.txt, and the unshipped file is left with only its header.
// An unshipped `*REMOVED*<entry>` line removes <entry> from the shipped file instead.
//
// Run automatically by `npm run version`, after scripts/apply-nuget-version.mjs. A prerelease version (the beta
// line of changesets pre mode) ships nothing, so the files keep tracking everything added since the last stable
// release. Idempotent: re-running after a stable version leaves nothing to move.
import { readFileSync, writeFileSync, readdirSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const srcDir = join(repoRoot, "src");
const header = "#nullable enable";
const removedPrefix = "*REMOVED*";

const version = JSON.parse(
  readFileSync(join(repoRoot, "build", "changeset", "nuget-suite", "package.json"), "utf8"),
).version;
if (version.includes("-")) {
  console.log(`${version} is a prerelease; the public API stays unshipped.`);
  process.exit(0);
}

const entries = (path) =>
  readFileSync(path, "utf8")
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line && line !== header);

const write = (path, lines) => writeFileSync(path, [header, ...lines].join("\n") + "\n");

for (const project of readdirSync(srcDir, { withFileTypes: true })) {
  if (!project.isDirectory()) continue;
  const shippedPath = join(srcDir, project.name, "PublicAPI.Shipped.txt");
  const unshippedPath = join(srcDir, project.name, "PublicAPI.Unshipped.txt");
  if (!existsSync(shippedPath) || !existsSync(unshippedPath)) continue;

  const unshipped = entries(unshippedPath);
  if (unshipped.length === 0) continue;

  const shipped = new Set(entries(shippedPath));
  for (const entry of unshipped) {
    if (entry.startsWith(removedPrefix)) {
      shipped.delete(entry.slice(removedPrefix.length));
    } else {
      shipped.add(entry);
    }
  }

  // Ordinal order, as the analyzer's code fix writes it, so the diff of a release shows only what moved.
  write(shippedPath, [...shipped].sort((a, b) => (a < b ? -1 : a > b ? 1 : 0)));
  write(unshippedPath, []);
  console.log(`${project.name}: shipped ${unshipped.length} public API entries.`);
}
