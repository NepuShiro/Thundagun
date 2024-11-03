# Thundagun

**Thundagun** is a Resonite mod that decouples the game logic and rendering loops by running Resonite and Unity on separate threads. This enhancement improves throughput and reduces latency, offering smoother gameplay and enhanced responsiveness. Thundagun operates in two modes—**Sync** and **Async**—to accommodate different performance requirements and user preferences.

## Key Features

- **Parallel Execution**: Maximizes CPU and GPU utilization by running Resonite and Unity on separate threads.
- **Sync Mode**: Ensures a consistent 1:1 ratio between game updates and frame rendering for stable visuals.
- **Async Mode**: Allows Unity to render frames independently, improving responsiveness by potentially skipping over multiple states or rendering older states when new ones aren't available.
- **Decoupled Input Handling**: Moves input processing to Unity, enabling continuous player movement and camera control even if Resonite stalls.
- **Batch Processing**: Uses a queue system for managing updates, ensuring safe and efficient change processing.

## Modes of Operation

### Sync Mode

Ideal for users prioritizing consistent frame timing and visual stability:

- **Synchronized Execution**: Resonite and Unity operate in lockstep using a flip-flop boolean lock.
- **Immediate Processing**: Changes are processed as soon as they become available.
- **Stable Visuals**: Minimizes discrepancies between game updates and frame rendering.

### Async Mode

Offers increased flexibility and responsiveness, especially beneficial for VR applications:

- **Independent Rendering**: Unity renders frames based on the latest available state without waiting for Resonite.
- **Batch Processing**: Updates are processed in batches, allowing Unity to safely redraw previous states.
- **Enhanced Responsiveness**: Input handling, movement, and IK calculations are performed on Unity's side.

## Technical Details

### Pipelining and Thread Management

- **Elimination of Idle Time**: Separate threads reduce CPU and GPU idle periods common in sequential update cycles.
- **Synchronization Mechanism**: A flip-flop boolean lock coordinates threads in Sync mode.
- **Batch Queuing**: Both modes use a batch queue for managing updates, facilitating consistent processing.

### Input Handling and Movement

- **Decoupled Input Processing**: Shifting input handling to Unity enhances responsiveness.
- **Continuous Control**: Players maintain movement and camera control even during game logic stalls.
- **State Forwarding**: Movement and IK results are forwarded to Resonite to align with the game state.

## Installation

1. **Download**: Get the latest release of Thundagun.
2. **Install**: Place the mod files into your Resonite mod directory.
3. **Configure**: Select your preferred mode (Sync or Async) via the mod's settings.
4. **Launch**: Start Resonite to apply the Thundagun patch.

## Contributions and Support

- **Issues**: Report bugs or request features via the repository's Issues section.
- **Pull Requests**: Submit code contributions through Pull Requests.

## Disclaimer

**Note**: Thundagun is experimental and may not cover all edge cases. Users should test different configurations to find optimal settings for their systems.
