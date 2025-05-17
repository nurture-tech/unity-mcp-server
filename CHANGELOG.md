# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## UNRELEASED

### Added

- `get_state` returns whether the current stage is in prefab isolation mode or not.


## [0.2.0] - 2025-05-16

### Added

- `UnityMCPSettings` Scriptable Object.

### Changed

- Add `NO_MCP` to disable MCP instead of requiring adding `USE_MCP` to enable MCP.

- `execute_code` executes the action function synchronously. This makes the function signature simpler and more likely for the tool call to succeed.

### Fixed

- Removed references to non-builtin packages

- Added missing required `System.Runtime.CompilerServices.Unsafe.dll`.

- Added `using System` to editor script template.

## [0.1.3] - 2025-05-08

### Added

- `get_state` tool returns all loaded scenes, playing mode, and unity editor version.

### Changed

- Renamed `search_objects` tool to `search`.

### Removed

- `get_active_scene` tool. It is superseded by the `get_state` tool.

- `unpack_asset` tool. With the `search` tool it's fairly redundant.

### Fixed

- Some of the tool descriptions referred to other tools by the wrong name.

- Changed examples for the `search` tool filters to avoid searching by `t:Model` or using `h:` as a suffix.

- Use `""` as default value for argument to the `search` tool and `screenshot` tool. This avoids some calls erroneously using `"null"` as the value.


## [0.1.2] - 2025-05-06

### Added

- Added some more examples on how to use search filters.

- The `execute_code` tool allows specifying the assemblies to include.

- Added `Nurture` assembly to the default list of included assemblies for script compilation.

### Changed

- When creating an editor script, we ask the LLM to generate the entire class. This way the line numbers for compile errors line up to the input and it makes fewer mistakes with `using`.

- Allow setting search filters as a string to allow for conditional logic (or and and)

### Fixed



- Wait for the user to say when script compilation is complete before proceeding. This helps avoid the issue where a tool is trying to run when the MCP server is restarted during a Domain Reload.

### Removed

- Disabled Behavior Graph tools for now until they mature.

## [0.1.1] - 2025-05-05

### Added

- Automatically exit play mode before performing destructive edit operations.

- Testing a scene will return a screenshot for each second of test from the main camera.

- The `focus_game_object` tool has an `isolate` parameter which will isolate the objects in the scene to just what is selected so it can be visible even if it's inside another object.

- The `README.md` is updated to include recommended Cursor custom agent settings.

- The `execute_code` module can access the physics and ui modules.

- For all write actions, the editor is focused so that the changes are visible as they happen.

### Changed

- Renamed all tools to be lower-snake-case to match other MCPs in the ecosystem. Removed `Unity` prefix as this is already added by Cursor.

- Testing a scene returns all logs, not just errors. This allows the agent to note debug logs.

- Passing filters to `search_objects` is a seperate parameter. This makes sure that filters are added to the query before the name term.

### Fixed

- Compatible with Gemini models.

- When searching objects, will wait up to 5 seconds for the search index to be ready.

- When searching objects, throws error if no search index is created. Unity does this by default so this should only happen in rare circumstances.

- The `execute_code` module can properly access scripts added to `Assets` in the default assembly.

- The `screenshot` tool will show exactly what is in the scene view.



## [0.1.0]

Initial version.