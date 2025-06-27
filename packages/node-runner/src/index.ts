import { spawn } from "node:child_process";
import { exit } from "node:process";
import { ArgumentParser, BooleanOptionalAction } from "argparse";
import { readPackageUp } from "read-package-up";
import path from "node:path";
import fs from "node:fs/promises";
import { fileURLToPath } from "node:url";

const parser = new ArgumentParser();
parser.add_argument("-unityPath", { type: String, required: true });
parser.add_argument("-projectPath", { type: String, required: true });
parser.add_argument("-dev", { type: Boolean, action: BooleanOptionalAction, required: false });
const args = parser.parse_args();
const unityPath = args.unityPath;
const devMode = args.dev;

let log: fs.FileHandle | undefined = undefined;

if (devMode) {
  log = await fs.open(path.join(args.projectPath, "mcp.log"), "w");
}

// Load the package.json for the current package we are running in and retrieve the version.
const currentDir = path.dirname(fileURLToPath(import.meta.url));
const packageData = await readPackageUp({
  cwd: currentDir,
});

let packageUrl;

if (devMode) {
  const unityPackagePath = path.resolve(path.dirname(packageData!.path), "..", "unity");
  packageUrl = `file:${unityPackagePath}`;
} else {
  packageUrl = `https://github.com/nurture-tech/unity-mcp.git?path=packages/unity#v${packageData!.packageJson.version}`;
}

await log?.write(`Package URL: ${packageUrl}\n`);

// Load Packages/package.json and add the is.nurture.mcp package to the project.
// Use `https://github.com/nurture-tech/unity-mcp.git?path=packages/unity#v[VERSION]`.
// TODO: Use published package version

const packageJsonPath = path.join(args.projectPath, "Packages", "manifest.json");
const packageJson = JSON.parse(await fs.readFile(packageJsonPath, "utf8"));
packageJson.dependencies["is.nurture.mcp"] = packageUrl;
await fs.writeFile(packageJsonPath, JSON.stringify(packageJson, null, 2));

const proc = spawn(unityPath, [...process.argv.slice(1), "-mcp", "-logFile", "-"]);

try {
  const code = await new Promise<number | null>((resolve, reject) => {
    let buffer = ""; // Buffer to accumulate partial lines

    process.stdin.on("data", async (data) => {
      await log?.write(data.toString());
      proc.stdin?.write(data);
    });
    proc.stdout?.on("data", async (data) => {
      // Add new data to buffer
      buffer += data.toString();

      // Split buffer into lines
      const lines = buffer.split("\n");

      // Keep the last line in buffer (it might be incomplete)
      buffer = lines.pop() || "";

      // Process complete lines
      for (const line of lines) {
        if (line.startsWith("{")) {
          process.stdout.write(line + "\n");
          await log?.write(line + "\n");
        }
      }
    });
    proc.on("exit", (code) => {
      resolve(code);
    });
    proc.on("error", (err) => {
      reject(err.message);
    });
  });

  exit(code ?? 0);
} finally {
  log?.close();
}
