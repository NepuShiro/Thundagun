# Thundagun

Thundagun is a Resonite mod that decouples the FrooxEngine and Unity loops by running FrooxEngine and Unity on separate threads.

**Warning: This mod is a prerelease. It may contain bugs and other issues. Crashes and missing/buggy visuals are possible but are generally uncommon. Use at your own risk.**

## Key Features

- **Parallel Execution**: Maximizes CPU and GPU utilization by running Resonite and Unity on separate threads.
- **Sync Mode**: Ensures a consistent 1:1 ratio between game updates and frames for stable visuals.
- **Async Mode**: Allows Unity to render frames independently, improving responsiveness by potentially processing multiple finished states to use the latest or by rendering older finished states when new ones aren't available.
- **Desync Mode**: Allows Unity process incremental changes from Resonite during long engine stalls.
- **Stall Prevention**: Uses a configurable timeout to ensure frames continue to be rendered even during massive change queuing, like world loading.
- **Auto Switching**: Enables thresholds to be defined for switching dynamically between sync, async, and desync modes based on game performance.

## Installation

1. **Download**: Get the latest release of Thundagun.
2. **Install**: Place the mod files into your Resonite mod directory.
3. **Configure**: Customize the behavior via the mod's settings.
4. **Launch**: Start Resonite to apply the Thundagun patch.

## Contributions and Support

- **Issues**: Report bugs or request features via the repository's Issues section.
- **Pull Requests**: Submit code contributions through Pull Requests.
