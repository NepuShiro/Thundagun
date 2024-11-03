# Thundagun

Thundagun is a Resonite mod that decouples the game logic and rendering loops by running Resonite and Unity on separate threads.

**Warning: This mod is a prerelease. It may contain bugs and other issues. Crashes and missing/buggy visuals are possible but are generally uncommon. Use at your own risk.**

## Key Features

- **Parallel Execution**: Maximizes CPU and GPU utilization by running Resonite and Unity on separate threads.
- **Sync Mode**: Ensures a consistent 1:1 ratio between game updates and frames for stable visuals.
- **Async Mode**: Allows Unity to render frames independently, improving responsiveness by potentially skipping over multiple states to use the latest or by rendering older states when new ones aren't available.
- **Auto Mode**: Automatically switches between Sync and Async modes based on the game's performance.
- **Decoupled Input Handling**: Moves input processing to Unity, enabling continuous player movement and camera control even if Resonite stalls.
- **Batch Processing**: Uses a batch queue system for managing updates, ensuring safe and efficient change processing.
- **Thread-Safe Locking**: Ensures thread locking/unlocking behavior is thread-safe and protected against deadlocks.

## Modes of Operation

### Sync Mode

The recommended mode for new users. Ensures each frame corresponds to one game update. Ideal for users prioritizing consistent frame timing and visual stability:

- **Synchronized Execution**: Resonite and Unity run in parallel but can briefly wait on each other if needed.
- **Immediate Processing**: Changes are processed as soon as they become available.
- **Stable Visuals**: Minimizes discrepancies between game updates and frame rendering.

### Async Mode

A more experimental mode for more experienced users. Allows stale states to be redrawn from a different perspective, and uses the most recent state if the queue has accumulated two or more batches. Offers increased flexibility and responsiveness, especially beneficial for VR applications:

- **Independent Rendering**: Unity renders frames based on the latest completed state without waiting for Resonite.
- **Batch Processing**: Updates are processed in batches, allowing Unity to safely redraw previous states.
- **Enhanced Responsiveness**: Input handling, movement, and IK calculations are performed on Unity's side.

### Auto Mode

An even more experimental mode for technical users. Dynamically switches between sync and async modes depending on the frame times between Resonite and Unity. This is useful for defining stalling behavior by tweaking thresholds, giving the consistency benefits of sync mode and the stalling protection offered by async mode:

- **EMA Weighting**: Uses exponential moving averages to accurately approximate the current frame rate.
- **Ratio Thresholds**: Defines the frame ratio threshold for switching between sync and async modes; bidirectional and between 1 and 1000.

## Installation

1. **Download**: Get the latest release of Thundagun.
2. **Install**: Place the mod files into your Resonite mod directory.
3. **Configure**: Select your preferred mode (Sync or Async) via the mod's settings.
4. **Launch**: Start Resonite to apply the Thundagun patch.

## Contributions and Support

- **Issues**: Report bugs or request features via the repository's Issues section.
- **Pull Requests**: Submit code contributions through Pull Requests.
