# Nurture Unity MCP Server (NUPS)

> A Model Context Protocol server for Unity

## Compatibility

| Name                  | Compatible | Notes                                                                 |
| --------------------- | ---------- | --------------------------------------------------------------------- |
| **Models**            |            |                                                                       |
| GPT-4.1               | ✅         |                                                                       |
| Claude 4 Sonnet       | ✅         |                                                                       |
| Claude 4 Opus         | ✅         |                                                                       |
| Gemini 2.5 Pro        | ✅         |                                                                       |
| Gemini 2.5 Flash      | ✅         |                                                                       |
| o3                    | ✅         | No image understanding                                                |
| o4-mini               | ✅         |                                                                       |
| **Unity Versions**    |            |                                                                       |
| Unity 6000.0.x        | ✅         | Higher versions should be fine. Lower versions may work but untested. |
| **Agents**            |            |                                                                       |
| Cursor                | ✅         |                                                                       |
| Rider AI              | ✅         |                                                                       |
| Claude Desktop        | ✅         |                                                                       |
| Claude Code           | ❌         | Hangs on tool calls                                                   |
| **Operating Systems** |            |                                                                       |
| Windows               | ✅         |                                                                       |
| Mac                   | ✅         |                                                                       |
| Ubuntu                | ❔         | Untested                                                              |

## Setup

### 1. Install [node.js](https://nodejs.org/en/download)

### 2. Configure `mcp.json`

```
{
  "mcpServers": {
    "unity": {
      "command": "npx -y @nurture-tech/unity-mcp-runner",
      "args": [
        "-unityPath",
        "<path to unity editor>",
        "-projectPath",
        "<path to unity project>"
      ]
    }
  }
}
```

The working directory is different for different agents. So your mileage may vary using relative paths for `-projectPath`. Absolute paths will always work.

This will automatically install the `is.nurture.mcp` package in your unity project. Feel free to commit those changes to source control.

## Known Issues

- The Google External Dependency Manager (EDMU) causes Unity to hang forever on startup when launched via Cursor on Windows. This is under investigation.

- Claude Code hangs on tool calls.

## Adding Project-Specific Tools

NUPS uses the official [C# MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk).

Create a static class to hold your tools. Add the `[McpServerToolType]` annotation to the class.

Declare static methods to implement each tool. Use the `[McpServerTool]` annotation to each method.

Reference the [Services](./packages/unity/Editor/Services) directory for examples.

You will likely need to quit unity and restart your agent in order for it to see the new tools.

## Usage Tips

> Do not launch the Unity project manually from the Unity hub when working with this MCP. This will cause the MCP client to fail to connect.

> Install the MCP server in per-project settings if your agent supports this. That way you can switch between different codebases and it will launch the corresponding unity project.

> You can add additional arguments to the unity command line — such as running in `-batchmode` or `-nographics` in order to run with background agents or inside of CI/CD pipelines. Use this format:

```
{
  "mcpServers": {
    "unity": {
      "command": "npx -y @nurture-tech/unity-mcp-runner",
      "args": [
        "-unityPath",
        "<path to unity editor>,
        "-projectPath",
        "."
        "--"
        "-batchmode"
        ...
      ]
    }
  }
}
```

> _Do not add the -logFile parameter_. The mcp server requires that the log file is redirected to stdout in order to operate.

> Split your desktop with your chat agent on one side and the unity editor on the other side. The unity editor needs to be visible on screen or else the `screenshot` tool will fail to see the scene view.
