# Nurture Unity MCP Server

> A set of Model Context Protocol server components for Unity

## Setup

### Install the Package in Unity Package Manager.

1. Open the Unity Package Manager window.
2. Choose **+** -> **Install package from Git URL**,
3. Enter `https://github.com/nurture-tech/unity-mcp-playground.git#main`.
4. Click **Install**.

### Configure `mcp.json`

```
{
  "mcpServers": {
    "unity": {
      "command": "C:/path/to/Unity.exe",
      "args": [
        "-projectPath", 
        ".",
        "-mcp",
        "-logFile",
        "-"
      ]
    }
  }
}
```

## Usage Tips

* Split your desktop with your chat agent on one side and the unity editor on the other side. The unity editor needs to be visible on screen or else the `screenshot` tool will fail to see the scene view.

* The MCP server has some overhead in the Unity Editor. To turn off MCP for some time, add `NO_MCP` to the **Scripting Define Symbols** in **Player Settings**.