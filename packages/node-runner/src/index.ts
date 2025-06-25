#!/usr/bin/env node

import { readFileSync } from "node:fs";
import { execSync, exec } from "node:child_process";
import { platform, exit } from "node:process";
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

const code = await new Promise<number | null>((resolve) => {
  process.stdin.pipe(proc.stdin!);
  proc.stdout?.on("data", (data) => {
    const lines = data.toString().split("\n");
    for (const line of lines) {
      if (line.startsWith("{")) {
        process.stdout.write(line + "\n");
      }
    }
  });
  proc.on("exit", (code) => {
    resolve(code);
  });
});

exit(code ?? 0);
