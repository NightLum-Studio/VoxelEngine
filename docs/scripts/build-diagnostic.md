# BuildDiagnostic (runtime)

Location: `Assets/ActualStuff/Scripts/Voxels/BuildDiagnostic.cs`

Summary
- In-game overlay for FPS, frame time, memory usage, and compute support
- Periodic logging; checks block DB presence and streamer/material status

Inspector
- Diagnostic Settings
	- enableDebugLogs (bool)
	- enablePerformanceMetrics (bool)
	- diagnosticInterval (float seconds)
- Status Display
	- showOnScreenDebug (bool)
	- showPerformanceOverlay (bool)
- Performance Monitoring
	- enableFPSCounter, enableMemoryMonitoring, enableFrameTimeAnalysis (bool)
	- performanceSampleCount (int)

Related
- [WorldStreamer](world-streamer.md)

[Back to overview](../overview.md)
