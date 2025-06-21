#!/usr/bin/env python3
import subprocess
import sys
import argparse
import os
import asyncio

process = None

def read_stdin():
    global process
    while True:
        line = sys.stdin.readline()
        if not line:
            break
        process.stdin.write(f"{line}\n")

def read_write_process():
    global process
    # Print output in real-time
    while True:
        stdout_line = process.stdout.readline()
        stderr_line = process.stderr.readline()

        if stdout_line:
            # Filter
            if stdout_line.startswith("{"):
                sys.stdout.write(f"{stdout_line}\n")
        if stderr_line:
            # Filter
            if stdout_line.startswith("{"):
                sys.stderr.write(stderr_line)

        if process.poll() is not None:
            break

async def run_process(path, args=None):  
    global process
    """
    Run a process at the specified path with the given arguments

    Args:
        path (str): Path to the executable
        args (list): List of arguments to pass to the process

    """
    cmd = [path]
    if args:
        cmd.extend(args)

    try:
        process = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            stdin=subprocess.PIPE,
            universal_newlines=True
        )

        os.set_blocking(process.stdout.fileno(), False)
        os.set_blocking(process.stderr.fileno(), False)
        
        await asyncio.gather(
            asyncio.to_thread(read_stdin),
            asyncio.to_thread(read_write_process)
        )
        
        return process.returncode
    except Exception as e:
        print(f"Error running process: {e}", file=sys.stderr)
        sys.exit(1)

def main():
    parser = argparse.ArgumentParser(description='Run a process with arguments')
    parser.add_argument('path', help='Path to the executable')
    parser.add_argument('args', nargs='*', help='Arguments to pass to the process')

    args, unknown = parser.parse_known_args()
    all_args = args.args + unknown + ["-mcp", "-logFile", "-"] 

    asyncio.run(run_process(args.path, all_args))

if __name__ == "__main__":
    main()
