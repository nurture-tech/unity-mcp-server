import { readFileSync } from "node:fs";
import { execSync, exec } from "node:child_process";
import { platform } from "node:process";
import { ArgumentParser } from "argparse";

const parser = new ArgumentParser();
parser.add_argument("-projectPath", { type: String, required: true });
const args = parser.parse_args();
const projectPath = args.projectPath;
const projectVersion = readFileSync(`${projectPath}/ProjectSettings/ProjectVersion.txt`, "utf8").match(/m_EditorVersion:\s*(.*)/)?.[1] || "";

const editorPathCommand =
  platform === "darwin"
    ? `/Applications/Unity\\ Hub.app/Contents/MacOS/Unity\\ Hub -- --headless editors -i ${projectVersion}`
    : `"${process.env.PROGRAMFILES}\\Unity Hub\\Unity Hub.exe" -- --headless editors -i  ${projectVersion}`;

const editorPaths = String(execSync(editorPathCommand)).trim();

const editorPath =
  `${editorPaths}`
    .split("\n")
    .find((line) => line.includes(projectVersion))
    ?.split(" , installed at ")[1] || "";

const proc = exec(`${editorPath} ${process.argv.slice(1).join(" ")} -mcp -logFile -`);

await new Promise((resolve) => {
  process.stdin.pipe(proc.stdin!);
  proc.stdout?.pipe(process.stdout);
  proc.on("exit", (code) => {
    resolve(code);
  });
});
