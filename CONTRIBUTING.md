# Contributing to Nurture Unity MCP Server

First off, thank you for considering contributing to the Nurture Unity MCP Server (NUPS)!
We welcome any type of contribution, from reporting bugs and suggesting enhancements to improving documentation and submitting new features.

## Getting Started

To develop locally, fork the NUPS repository and clone it in your local machine. The Unity MCP Server repo is a monorepo with 
several packages inside the `packages` directory:

`node-runner`: A nodejs script which automatically installs the Unity package in the project and then runs it - sanitizing the stdout.

`unity`: A Unity C# package containing actual MCP server logic w/ the tools

To develop and test the `node-runner` package:

1. Run `npm i`.

2. Run `npm run dev --ws`.

To develop and test the `unity` package:

1. Create or choose a Unity project to develop with outside the repository.

2. Configure the mcp server via `mcp.json` and add an additional `-dev` argument. This will run your MCP server with a reference to the package in your local checkout that is editable.

When you commit your work, husky will run a pre-commit hook and lint your code.

### Commit Messages

We use conventional commits to keep the commit history clean and readable. Please follow this format for your commit messages:

```
feat: Add a new feature
fix: Fix a bug
docs: Update documentation
style: Make styling changes
refactor: Refactor code without changing functionality
test: Add or improve tests
chore: Update build tasks, package manager configs, etc.
```

For example: `feat: Add support for custom camera in screenshot tool`

## Submitting Your Changes

1. Push your changes to your forked repository: `git push origin my-feature-branch`.
2. Open a pull request on the main repository.
3. In your pull request description, please explain the changes you made and why.
4. We will review your pull request as soon as possible. We may suggest some changes or improvements.

## Reporting Bugs

If you find a bug, please open an issue on GitHub. Please include the following information in your bug report:

- A clear and descriptive title.
- A detailed description of the bug.
- Steps to reproduce the bug.
- Your operating system, Unity version, and any other relevant environment information.
- Any error messages or logs.

## Suggesting Enhancements

If you have an idea for a new feature or an improvement to an existing one, please open an issue on GitHub. Please include the following information in your enhancement suggestion:

- A clear and descriptive title.
- A detailed description of the proposed enhancement.
- Any mockups, examples, or links that might be helpful.

## Code of Conduct

This project and everyone participating in it is governed by our [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior. (Note: A `CODE_OF_CONDUCT.md` file will need to be created).

Thank you again for your contribution! 