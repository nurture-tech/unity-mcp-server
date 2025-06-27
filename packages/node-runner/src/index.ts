import { spawn } from "node:child_process";
import { exit } from "node:process";
import { ArgumentParser, BooleanOptionalAction } from "argparse";
import { readPackageUp } from "read-package-up";
import path from "node:path";
import fs from "node:fs/promises";

const parser = new ArgumentParser();
parser.add_argument("-unityPath", { type: String, required: true });
parser.add_argument("-projectPath", { type: String, required: true });
parser.add_argument("-dev", { type: Boolean, action: BooleanOptionalAction, required: false });
const args = parser.parse_args();
const unityPath = args.unityPath;
const devMode = args.dev;

// Load the package.json for the current package we are running in and retrieve the version.

const packageData = await readPackageUp();

let tag = "main";

if (!devMode && packageData?.packageJson.version) {
  tag = `v${packageData.packageJson.version}`;
}

// Load Packages/package.json and add the is.nurture.mcp package to the project.
// Use `https://github.com/nurture-tech/unity-mcp.git?path=packages/unity#v[VERSION]`.
// TODO: Use published package version

const packageJsonPath = path.join(args.projectPath, "Packages", "manifest.json");
const packageJson = JSON.parse(await fs.readFile(packageJsonPath, "utf8"));
packageJson.dependencies["is.nurture.mcp"] = `https://github.com/nurture-tech/unity-mcp.git?path=packages/unity#${tag}`;
await fs.writeFile(packageJsonPath, JSON.stringify(packageJson, null, 2));

const proc = spawn(unityPath, [...process.argv.slice(1), "-mcp", "-logFile", "-"]);

let log: fs.FileHandle | undefined = undefined;
let buffer = ""; // Buffer to accumulate partial lines

if (devMode) {
  log = await fs.open(path.join(args.projectPath, "mcp.log"), "w");
}

try {
  const code = await new Promise<number | null>((resolve, reject) => {
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
