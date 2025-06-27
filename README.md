# Nurture Unity MCP Server

> A set of Model Context Protocol server components for Unity

## Setup

### Configure `mcp.json`

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

The first startup of the MCP will take a while because it has to install the package and compile scripts.

## Known Issues

- The Google External Dependency Manager (EDMU) causes Unity to hang forever on startup when launched via Cursor. This is under investigation.

## Usage Tips

> Do not launch the Unity project manually from the Unity hub when working with this MCP. This will cause the MCP client to fail to connect.

> Install the MCP server in per-project settings if your agent supports this. That way you can switch between different codebases and it will launch the corresponding unity project.

> You can add additional arguments to the unity command line. Such as running in `-batchmode` or `-nographics` in order to run with background agents or inside of CI/CD pipelines.Use this format:

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

> _Do not add the -logFile parameter_. The mcp server requires that the logFile is redirected to stdout in order to operate.

> Split your desktop with your chat agent on one side and the unity editor on the other side. The unity editor needs to be visible on screen or else the `screenshot` tool will fail to see the scene view.