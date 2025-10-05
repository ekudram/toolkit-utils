# ToolkitUtils

A community-maintained collection of tweaks and commands for TwitchToolkit.

## 🚀 What's Included?

- A collection of [events](https://sirrandoo.github.io/toolkit-utils/events.html)
- A collection of [commands](https://sirrandoo.github.io/toolkit-utils/commands.html)
- A collection of [tweaks](https://sirrandoo.github.io/toolkit-utils/tweaks/)
- The ability to individually set trait prices
- The ability to clean up old viewers from your viewers file
- Additional [mod compatibility](https://sirrandoo.github.io/toolkit-utils/modcompat.html)

## 🔧 Major Updates & Fixes

### Critical Stability Improvements
- **Threading Overhaul**: Replaced .NET threading with RimWorld's internal threading system
- **Null Reference Fixes**: Added comprehensive error catching and null checks
- **Game State Protection**: Fixed save file corruption issues from thread racing conditions
- **Command Reliability**: Resolved issues with commands not executing properly

### TwitchLib Modernization (v3.1 → 3.4+)
- **Message System Update**: Migrated from `ITwitchMessage` to `TwitchMessageWrapper`
- **API Compatibility**: Updated all command and message parsing systems
- **Database Integration**: Fixed viewer database references to point to correct mod

### Code Quality & Maintenance
- **Redundant Code Removal**: Eliminated patches made obsolete by ToolkitCore/TwitchToolkit updates
- **Debugging Tools**: Added toggleable debug logging button
- **Performance**: Optimized threading and resource usage

## 📋 Compatibility

- **Requires**: Updated TwitchToolkit 2.0e+
- **TwitchLib**: v3.4.0+ compatible
- **RimWorld**: Maintains threading safety with native systems

## 📄 License

GNU Affero General Public License v3

*This is a community preservation fork maintaining and improving upon SirRandoo's original work.*
