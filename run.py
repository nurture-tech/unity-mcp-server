#!/usr/bin/env python3
import subprocess
import sys
import argparse

def run_process(path, args=None):
    """
    Run a process at the specified path with the given arguments

    Args:
        path (str): Path to the executable
        args (list): List of arguments to pass to the process

    Returns:
        int: Return code from the process
    """
    cmd = [path]
    if args:
        cmd.extend(args)

    try:
        process = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            universal_newlines=True
        )

        # Print output in real-time
        while True:
            stdout_line = process.stdout.readline()
            stderr_line = process.stderr.readline()

            if stdout_line:
                print(stdout_line.strip())
            if stderr_line:
                print(stderr_line.strip(), file=sys.stderr)

            if process.poll() is not None:
                # Process has terminated
                # Print any remaining output
                for line in process.stdout.readlines():
                    print(line.strip())
                for line in process.stderr.readlines():
                    print(line.strip(), file=sys.stderr)
                break

        return process.returncode
    except Exception as e:
        print(f"Error running process: {e}", file=sys.stderr)
        return 1

def main():
    parser = argparse.ArgumentParser(description='Run a process with arguments')
    parser.add_argument('path', help='Path to the executable')
    parser.add_argument('args', nargs='*', help='Arguments to pass to the process')

    args, unknown = parser.parse_known_args()
    all_args = args.args + unknown

    return run_process(args.path, all_args)

if __name__ == "__main__":
    sys.exit(main())
