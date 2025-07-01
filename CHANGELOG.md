# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [UNRELEASED]

## Fixed

- Check if `UnityLockFile` is still open in windows. Avoids erroneous `Unity project is already open` messages.

## [0.3.1] - 2025-06-30

### Fixed

- Added shebang to node-runner script so that `npx` works correctly.

- `mcp.json` reference now uses the cmd `npx` by itself for Claude compatibility.

## [0.3.0] - 2025-06-30

### Added

- In `get_state`, return information about the opened prefab, if any.

- In `get_selection` and `search` in hierarchy mode, return whether a game object is in the prefab isolation hierarchy or the scene hierarchy.

- If the `srcPath` and the `dstPath` are the same in `import_asset` we still import it without copying it.

- Automatically install the Unity package.

- Community documentation (CONTRIBUTING, CODE_OF_CONDUCT, SECURITY, CODEOWNERS, etc).

- A few seed cursor rules files.

### Changed

- Document that opening a prefab will activate isolation mode.

- When retrieving the hierarchy path for a gameobject in prefab isolation mode, treat "/" as the root gameobject instead of "/GameObjectName".

- _BREAKING_ switched to stdio transport.

- Updated to C# MCP SDK v0.3.0-preview-1

### Fixed

- Only return one level of game objects in the results for `open_prefab`.

- Compound search filters now work properly with `h:` and `p:` filters. These do not support grouping so they are now controlled by an additional parameter to the `search` tool.

- Connection to MCP client no longer breaks when compiling scripts due to new stdio transport.

- Removed erroneous instruction in `create_script`.

- Progress is correctly calculated during `test_active_scene` runs.

## [0.2.2] - 2025-06-03

### Added

- Throw a more specific exception if source file is missing when using `copy_asset` and `import_asset`.

### Changed

- `supportsImages` parameter renamed to `showThumbnails`.

### Fixed

- Correct exception and stack trace is returned when `execute_code` throws an exception to allow for correction.

- `showThumbnails` parameter is respected when retrieving mesh and texture assets.

- Remove language about when to use the `screenshot` tool to avoid LLMs from erroneously avoiding using it.

## [0.2.1] - 2025-05-19

### Added

- `get_state` returns whether the current stage is in prefab isolation mode or not.

- Description of `screenshot` tool specifies reuqirements of the MCP client in order to consume it.

- Taking screenshots during `test_active_scene` is now optional and specified by a parameter.

- Returning an image preview of a model in `get_asset_contents` is now optional and specifieid by a parameter.

- Define `UNITY_EDITOR` to increase likelyhood of certain generated editor scripts compiling.

### Fixed

- Wait for preview to load before returning model asset so that the preview is returned reliably.

- Return acknowledgement for `copy_asset` so that Cursor doesn't think the tool call failed when it actually succeeded.

- If no logs are returned from `Task<string> WithLogs(AsyncAction action, bool includeStackTrace = true)` it no longer raises an exception.

- Surround `search` filters with `()` so that a compound filter doesn't end up ignoring the asset name query.

- `test_active_scene` actually runs the specified amount of time.

- Fix occasional crashes focusing the window in `test_active_scene`.

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
