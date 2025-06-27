# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 0.3.x   | :white_check_mark: |

## Reporting a Vulnerability

Given the nature of an MCP server that is designed to allow arbitrary code generation and execution, there is a very limited set of vulnerability reports that we will accept. By way of example:

Covered:

✅ A tool can be made to act contrary to its [security-relevant annotations](https://modelcontextprotocol.io/docs/concepts/tools#available-tool-annotations).

Not Covered:

❌ `execute_code` tool can be made to run malicious code via prompt injection in the agent conversation.