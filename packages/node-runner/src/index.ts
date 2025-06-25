import { spawn } from "node:child_process";
import { exit } from "node:process";
import { ArgumentParser } from "argparse";

const parser = new ArgumentParser();
parser.add_argument("-unityPath", { type: String, required: true });
parser.add_argument("", { nargs: "*" });
const args = parser.parse_args();
const unityPath = args.unityPath;

const proc = spawn(unityPath, [...process.argv.slice(1), "-mcp", "-logFile", "-"]);

const code = await new Promise<number | null>((resolve, reject) => {
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
  proc.on("error", (err) => {
    reject(err.message);
  });
});

exit(code ?? 0);
