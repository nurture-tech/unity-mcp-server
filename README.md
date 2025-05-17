# Nurture Unity MCP Server

> A set of Model Context Protocol server components for Unity

## Compatible Models
- Claude 3.7*
- Gemini 2.0, 2.5*
- Deepseek v3, r1
- gpt-4o, gpt-4.1, o1, o3, o4
- grok-3

* Supports image interpretation based on testing. Use these models to manipulate visuals where it can benefit from visual feedback of what's in the scene.

## Configuring Unity

### Install the Package in Unity Package Manager.

1. Open the Unity Package Manager window.
2. Choose **+** -> **Install package from Git URL**,
3. Enter `https://github.com/nurture-tech/unity-mcp-playground.git#main`.
4. Click **Install**.

## Configuring Cursor

### Configure `mcp.json`.

Add to or create `.cursor/mcp.json`:

```
{
  "mcpServers": {
    "unity": {
      "type": "sse",
      "url": "http://localhost:5000/sse",
      "messageEndpoint": "/message"
    }
  }
}
```


## Usage Tips

* Split your desktop with your chat agent on one side and the unity editor on the other side. The unity editor needs to be visible on screen or else the `screenshot` tool will fail to see the scene view.

* The MCP server has some overhead in the Unity Editor. To turn off MCP for some time, add `NO_MCP` to the **Scripting Define Symbols** in **Player Settings**.

* Add a rules file to `.cursor\rules\unity.mdc`:

```
- When editing an existing scene or prefab, open it first.

- After creating a new scene, open it.

- After creating or changing anything that will affect the visuals, take a screenshot and review your work to see if it looks visually correct. If it's wrong, adjust it before moving on.

- Don't use generic file tools (edit_file, apply, copy, move, etc) when working with anything in the `Assets` folder.
```