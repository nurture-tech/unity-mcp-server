#!/usr/bin/env python3
import subprocess
import sys
import argparse
import os


def run_process(path, args=None):
    log = open("log.txt", "w")
    log.write("====================\n")
    log.flush()
    
    
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
            stdin=subprocess.PIPE,
            universal_newlines=True
        )

        os.set_blocking(process.stdout.fileno(), False)
        os.set_blocking(process.stderr.fileno(), False)
        os.set_blocking(sys.stdin.fileno(), False)
        
        stdin_queue = []

        server_ready = False

        # Print output in real-time
        while True:
            stdout_line = process.stdout.readline()
            stderr_line = process.stderr.readline()

            my_stdin_line = sys.stdin.readline()

            if my_stdin_line:
                log.write(f"[IN] {my_stdin_line}\n")
                log.flush()
            
                if server_ready:
                    process.stdin.write(f"{my_stdin_line}\n")
                else:
                    stdin_queue.append(my_stdin_line)
        
            if stdout_line:
                log.write(f"[Unity OUT] {stdout_line}\n")
                log.flush()
                # TODO: Filter
                process.stdout.write(f"{stdout_line}\n")
                if "[MCP] Server started" in stdout_line:
                    server_ready = True
                    for line in stdin_queue:
                        process.stdin.write(f"{line}\n")
                    stdin_queue.clear()
            if stderr_line:
                log.write(f"[Unity ERR] {stderr_line}\n")
                log.flush()
                # TODO: Filter
                process.stderr.write(stderr_line)

            if process.poll() is not None:
                # Process has terminated
                # Print any remaining output
                for line in process.stdout.readlines():
                    log.write(f"[Unity OUT] {line}\n")
                for line in process.stderr.readlines():
                    log.write(f"[Unity ERR] {line}\n")
                    
                log.close()
                break

        return process.returncode
    except Exception as e:
        print(f"Error running process: {e}", file=sys.stderr)
        log.close()
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
