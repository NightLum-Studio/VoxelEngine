using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using HyperVoxel;
using System.Collections;

namespace HyperVoxel
{
    /// <summary>
    /// Diagnostic component to help identify build-specific world generation issues
    /// </summary>
    public class BuildDiagnostic : MonoBehaviour
    {
        [Header("Diagnostic Settings")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool enablePerformanceMetrics = true;
        [SerializeField] private float diagnosticInterval = 2.0f;
        
        [Header("Status Display")]
        [SerializeField] private bool showOnScreenDebug = true;
        [SerializeField] private bool showPerformanceOverlay = true;
        
        [Header("Performance Monitoring")]
        [SerializeField] private bool enableFPSCounter = true;
        [SerializeField] private bool enableMemoryMonitoring = true;
        [SerializeField] private bool enableFrameTimeAnalysis = true;
        [SerializeField] private int performanceSampleCount = 60;
        
        private WorldStreamer _worldStreamer;
        private float _lastDiagnosticTime;
        private int _totalChunksGenerated;
        private int _chunksGeneratedThisSession;
        private string _statusText = "";
        
        // Performance monitoring variables
        private float[] _fpsHistory;
        private float[] _frameTimeHistory;
        private int _performanceIndex = 0;
        private float _lastFrameTime;
        private float _avgFPS = 0f;
        private float _minFPS = float.MaxValue;
        private float _maxFPS = 0f;
        private float _avgFrameTime = 0f;
        private long _initialMemory = 0;
        private int _frameCount = 0;
        
        private void Start()
        {
            _worldStreamer = FindObjectOfType<WorldStreamer>();
            if (_worldStreamer == null)
            {
                Debug.LogError("BuildDiagnostic: No WorldStreamer found in scene!");
                enabled = false;
                return;
            }
            
            // Initialize performance monitoring
            InitializePerformanceMonitoring();
            
            StartCoroutine(PeriodicDiagnostic());
            
            // Initial diagnostic
            RunDiagnostic();
        }
        
        private void InitializePerformanceMonitoring()
        {
            if (enableFPSCounter || enableFrameTimeAnalysis)
            {
                _fpsHistory = new float[performanceSampleCount];
                _frameTimeHistory = new float[performanceSampleCount];
                
                // Initialize with current values
                float currentFPS = 1.0f / Time.deltaTime;
                for (int i = 0; i < performanceSampleCount; i++)
                {
                    _fpsHistory[i] = currentFPS;
                    _frameTimeHistory[i] = Time.deltaTime * 1000f;
                }
            }
            
            if (enableMemoryMonitoring)
            {
                try
                {
                    _initialMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory();
                }
                catch
                {
                    _initialMemory = 0;
                }
            }
            
            _lastFrameTime = Time.realtimeSinceStartup;
        }
        
        private void Update()
        {
            UpdatePerformanceMetrics();
        }
        
        private void UpdatePerformanceMetrics()
        {
            if (!enablePerformanceMetrics) return;
            
            _frameCount++;
            
            if (enableFPSCounter || enableFrameTimeAnalysis)
            {
                float currentFPS = 1.0f / Time.deltaTime;
                float currentFrameTime = Time.deltaTime * 1000f; // Convert to milliseconds
                
                // Update circular buffer
                _fpsHistory[_performanceIndex] = currentFPS;
                _frameTimeHistory[_performanceIndex] = currentFrameTime;
                _performanceIndex = (_performanceIndex + 1) % performanceSampleCount;
                
                // Calculate statistics
                CalculatePerformanceStats();
            }
        }
        
        private void CalculatePerformanceStats()
        {
            float fpsSum = 0f;
            float frameTimeSum = 0f;
            _minFPS = float.MaxValue;
            _maxFPS = 0f;
            
            for (int i = 0; i < performanceSampleCount; i++)
            {
                float fps = _fpsHistory[i];
                float frameTime = _frameTimeHistory[i];
                
                fpsSum += fps;
                frameTimeSum += frameTime;
                
                if (fps < _minFPS) _minFPS = fps;
                if (fps > _maxFPS) _maxFPS = fps;
            }
            
            _avgFPS = fpsSum / performanceSampleCount;
            _avgFrameTime = frameTimeSum / performanceSampleCount;
        }
        
        private IEnumerator PeriodicDiagnostic()
        {
            while (enabled)
            {
                yield return new WaitForSeconds(diagnosticInterval);
                RunDiagnostic();
            }
        }
        
        private void RunDiagnostic()
        {
            if (!enableDebugLogs) return;
            
            var status = new System.Text.StringBuilder();
            status.AppendLine("=== VOXEL ENGINE BUILD DIAGNOSTIC ===");
            status.AppendLine($"Time: {Time.time:F1}s");
            status.AppendLine($"Platform: {Application.platform}");
            status.AppendLine($"Build: {(Application.isEditor ? "EDITOR" : "BUILD")}");
            status.AppendLine();
            
            // Check WorldStreamer status
            if (_worldStreamer != null)
            {
                CheckWorldStreamerStatus(status);
                CheckResourcesStatus(status);
                CheckJobSystemStatus(status);
                CheckComputeShaderStatus(status);
                CheckPerformanceMetrics(status);
            }
            
            _statusText = status.ToString();
            
            if (enableDebugLogs)
            {
                Debug.Log(_statusText);
            }
        }
        
        private void CheckWorldStreamerStatus(System.Text.StringBuilder status)
        {
            status.AppendLine("--- WorldStreamer Status ---");
            status.AppendLine($"Enabled: {_worldStreamer.enabled}");
            status.AppendLine($"GameObject Active: {_worldStreamer.gameObject.activeInHierarchy}");
            
            // Check streaming center
            var center = GetStreamingCenter();
            if (center != null)
            {
                status.AppendLine($"Streaming Center: {center.name} at {center.position}");
            }
            else
            {
                status.AppendLine("⚠️  NO STREAMING CENTER FOUND!");
            }
            
            status.AppendLine($"View Distance: {_worldStreamer.viewDistanceChunks}");
            status.AppendLine($"Max Generate/Frame: {_worldStreamer.maxGeneratePerFrame}");
            status.AppendLine($"Seed: {_worldStreamer.seed}");
        }
        
        private void CheckResourcesStatus(System.Text.StringBuilder status)
        {
            status.AppendLine();
            status.AppendLine("--- Resources Status ---");
            
            // Check compute shader
            var computeShader = _worldStreamer.voxelMesher;
            if (computeShader != null)
            {
                status.AppendLine($"✅ Compute Shader: {computeShader.name}");
            }
            else
            {
                status.AppendLine("❌ Compute Shader: NULL");
            }
            
            // Check material
            var material = _worldStreamer.voxelMaterial;
            if (material != null)
            {
                status.AppendLine($"✅ Voxel Material: {material.name}");
                status.AppendLine($"   Shader: {material.shader.name}");
            }
            else
            {
                status.AppendLine("❌ Voxel Material: NULL");
            }
            
            // Check block database integration
            var blockDB = _worldStreamer.blockDatabaseIntegration;
            if (blockDB != null)
            {
                status.AppendLine($"✅ Block Database Integration: {blockDB.name}");
            }
            else
            {
                status.AppendLine("⚠️  Block Database Integration: NULL");
            }
        }
        
        private void CheckJobSystemStatus(System.Text.StringBuilder status)
        {
            status.AppendLine();
            status.AppendLine("--- Job System Status ---");
            
            // Test job system functionality
            var testArray = new NativeArray<int>(1, Allocator.TempJob);
            var testJob = new TestJob { data = testArray };
            
            try
            {
                var handle = testJob.Schedule();
                handle.Complete();
                
                if (testArray[0] == 42)
                {
                    status.AppendLine("✅ Job System: Working");
                }
                else
                {
                    status.AppendLine("❌ Job System: Data corruption detected");
                }
            }
            catch (System.Exception e)
            {
                status.AppendLine($"❌ Job System: Exception - {e.Message}");
            }
            finally
            {
                testArray.Dispose();
            }
        }
        
        private void CheckComputeShaderStatus(System.Text.StringBuilder status)
        {
            status.AppendLine();
            status.AppendLine("--- Compute Shader Status ---");
            
            var computeShader = _worldStreamer.voxelMesher;
            if (computeShader != null)
            {
                // Check if compute shaders are supported
                if (SystemInfo.supportsComputeShaders)
                {
                    status.AppendLine("✅ Compute Shaders: Supported");
                }
                else
                {
                    status.AppendLine("❌ Compute Shaders: NOT SUPPORTED");
                }
                
                status.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
                status.AppendLine($"Graphics API: {SystemInfo.graphicsDeviceType}");
                status.AppendLine($"Max Compute Work Groups: {SystemInfo.maxComputeWorkGroupSizeX}x{SystemInfo.maxComputeWorkGroupSizeY}x{SystemInfo.maxComputeWorkGroupSizeZ}");
            }
        }
        
        private void CheckPerformanceMetrics(System.Text.StringBuilder status)
        {
            if (!enablePerformanceMetrics) return;
            
            status.AppendLine();
            status.AppendLine("--- Performance Metrics ---");
            
            // FPS Analysis
            if (enableFPSCounter)
            {
                float currentFPS = 1.0f / Time.deltaTime;
                status.AppendLine($"Current FPS: {currentFPS:F1}");
                status.AppendLine($"Average FPS: {_avgFPS:F1} (over {performanceSampleCount} frames)");
                status.AppendLine($"FPS Range: {_minFPS:F1} - {_maxFPS:F1}");
                
                // Performance categories
                string perfCategory = GetPerformanceCategory(_avgFPS);
                status.AppendLine($"Performance: {perfCategory}");
            }
            
            // Frame Time Analysis
            if (enableFrameTimeAnalysis)
            {
                float currentFrameTime = Time.deltaTime * 1000f;
                status.AppendLine($"Current Frame Time: {currentFrameTime:F2}ms");
                status.AppendLine($"Average Frame Time: {_avgFrameTime:F2}ms");
                
                // Frame time spikes detection
                if (currentFrameTime > _avgFrameTime * 2.0f)
                {
                    status.AppendLine("⚠️  Frame time spike detected!");
                }
            }
            
            // Memory Analysis
            if (enableMemoryMonitoring)
            {
                try
                {
                    long currentMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory();
                    long memoryDelta = currentMemory - _initialMemory;
                    
                    status.AppendLine($"Allocated Memory: {currentMemory / (1024 * 1024)}MB");
                    status.AppendLine($"Memory Delta: {(memoryDelta >= 0 ? "+" : "")}{memoryDelta / (1024 * 1024)}MB");
                    
                    // Memory leak detection
                    if (memoryDelta > 100 * 1024 * 1024) // 100MB increase
                    {
                        status.AppendLine("⚠️  Potential memory leak detected!");
                    }
                }
                catch
                {
                    status.AppendLine("Allocated Memory: N/A (API not available)");
                }
                
                // GC Analysis
                status.AppendLine($"GC Total Memory: {System.GC.GetTotalMemory(false) / (1024 * 1024)}MB");
            }
            
            // System Performance
            status.AppendLine($"Time Scale: {Time.timeScale:F2}");
            status.AppendLine($"Fixed Timestep: {Time.fixedDeltaTime * 1000:F2}ms");
            status.AppendLine($"Target Frame Rate: {Application.targetFrameRate} FPS");
            
            // Voxel Engine Performance
            status.AppendLine($"Chunks Generated This Session: {_chunksGeneratedThisSession}");
            status.AppendLine($"Total Chunks Generated: {_totalChunksGenerated}");
            status.AppendLine($"Frame Count: {_frameCount}");
            status.AppendLine($"Session Time: {Time.time:F1}s");
        }
        
        private string GetPerformanceCategory(float avgFPS)
        {
            if (avgFPS >= 60) return "🟢 Excellent (60+ FPS)";
            else if (avgFPS >= 45) return "🟡 Good (45+ FPS)";
            else if (avgFPS >= 30) return "🟠 Fair (30+ FPS)";
            else if (avgFPS >= 15) return "🔴 Poor (15+ FPS)";
            else return "🔴 Critical (<15 FPS)";
        }
        
        private Transform GetStreamingCenter()
        {
            // Mirror the logic from WorldStreamer
            Transform centerT = _worldStreamer.player;
            if (centerT == null)
            {
                var cam = Camera.main;
                if (cam != null)
                    centerT = cam.transform;
                else
                    centerT = _worldStreamer.transform;
            }
            return centerT;
        }
        
        private void OnGUI()
        {
            // Full diagnostic overlay
            if (showOnScreenDebug && !string.IsNullOrEmpty(_statusText))
            {
                var style = new GUIStyle(GUI.skin.box);
                style.alignment = TextAnchor.UpperLeft;
                style.fontSize = 10;
                style.normal.textColor = Color.white;
                
                var screenRect = new Rect(10, 10, 450, 350);
                GUI.Box(screenRect, _statusText, style);
            }
            
            // Compact performance overlay
            if (showPerformanceOverlay && enablePerformanceMetrics)
            {
                DrawPerformanceOverlay();
            }
        }
        
        private void DrawPerformanceOverlay()
        {
            var perfStyle = new GUIStyle(GUI.skin.box);
            perfStyle.alignment = TextAnchor.UpperLeft;
            perfStyle.fontSize = 14;
            perfStyle.fontStyle = FontStyle.Bold;
            
            // Position in top-right corner
            float width = 250f;
            float height = 120f;
            var perfRect = new Rect(Screen.width - width - 10, 10, width, height);
            
            var perfText = new System.Text.StringBuilder();
            perfText.AppendLine("🎯 PERFORMANCE MONITOR");
            
            if (enableFPSCounter)
            {
                float currentFPS = 1.0f / Time.deltaTime;
                perfStyle.normal.textColor = GetFPSColor(currentFPS);
                perfText.AppendLine($"FPS: {currentFPS:F1} (avg: {_avgFPS:F1})");
                perfText.AppendLine($"Range: {_minFPS:F1} - {_maxFPS:F1}");
            }
            
            if (enableFrameTimeAnalysis)
            {
                float currentFrameTime = Time.deltaTime * 1000f;
                perfText.AppendLine($"Frame: {currentFrameTime:F1}ms");
            }
            
            if (enableMemoryMonitoring)
            {
                try
                {
                    long currentMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory();
                    perfText.AppendLine($"Memory: {currentMemory / (1024 * 1024)}MB");
                }
                catch
                {
                    perfText.AppendLine("Memory: N/A");
                }
            }
            
            perfText.AppendLine($"Chunks: {_chunksGeneratedThisSession}");
            
            GUI.Box(perfRect, perfText.ToString(), perfStyle);
        }
        
        private Color GetFPSColor(float fps)
        {
            if (fps >= 60) return Color.green;
            else if (fps >= 45) return Color.yellow;
            else if (fps >= 30) return new Color(1f, 0.5f, 0f); // Orange
            else return Color.red;
        }
        
        [System.Serializable]
        public struct TestJob : IJob
        {
            public NativeArray<int> data;
            
            public void Execute()
            {
                data[0] = 42; // Test value
            }
        }
    }
}
