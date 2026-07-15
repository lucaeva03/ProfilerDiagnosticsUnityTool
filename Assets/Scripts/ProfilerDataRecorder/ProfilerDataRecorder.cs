using System;
using System.Collections;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

public class ProfilerDataRecorder : MonoBehaviour
{
    [Header("Output Settings")]

    [Tooltip("Base name for the CSV file. Scenario name and timestamp are appended automatically.")]
    public string fileNameBase = "profiler_data";

    [Tooltip("Output directory. Leave empty to use Application.persistentDataPath.")]
    public string outputDirectory = "";

    [Header("Recording Settings")]

    [Tooltip("Start recording automatically on Awake. Disable for manual control via StartRecording().")]
    public bool autoStart = true;

    [Tooltip("Current test scenario name. Included in the file name and CSV header.")]
    public string scenarioName = "Scenario_1";

    [Tooltip("Maximum frames to record. Set to 0 for unlimited.")]
    public int maxFrames = 0;

    [Header("UI ADV")]

    [Tooltip("Background Panel for the status message.")]
    public GameObject statusPanel;

    [Tooltip("Text component that displays the status message.")]
    public TMPro.TextMeshProUGUI statusText;

    // Main thread time per frame, in nanoseconds — converted to ms in SampleAndWrite().
    private ProfilerRecorder _frameTimeRecorder;

    // Draw calls per frame. Named "Batches Count" in Unity 6.
    private ProfilerRecorder _drawCallsRecorder;

    // Managed heap allocations per frame, in bytes — converted to KB in SampleAndWrite().
    private ProfilerRecorder _gcAllocRecorder;

    // Triangle count per frame.
    private ProfilerRecorder _trianglesRecorder;

    // Total memory reserved by the Unity runtime, in bytes — converted to MB in SampleAndWrite().
    private ProfilerRecorder _totalMemoryRecorder;

    private StreamWriter _csvWriter;
    private bool _isRecording;
    private int _frameCount;
    private string _outputFilePath;
    private Coroutine _hideCoroutine;

    private void Awake()
    {
        _frameTimeRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Internal, "Main Thread",
            options: ProfilerRecorderOptions.Default);

        _drawCallsRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Render, "Batches Count",
            options: ProfilerRecorderOptions.Default);

        _gcAllocRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Memory, "GC Allocated In Frame",
            options: ProfilerRecorderOptions.Default);

        _trianglesRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Render, "Triangles Count",
            options: ProfilerRecorderOptions.Default);

        _totalMemoryRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Memory, "Total Reserved Memory",
            options: ProfilerRecorderOptions.Default);

        if (statusPanel != null)
            statusPanel.SetActive(false);

        if (autoStart)
            StartRecording();
    }

    private void LateUpdate()
    {
        // LateUpdate ensures all systems have completed before sampling.
        if (!_isRecording)
            return;

        if (maxFrames > 0 && _frameCount >= maxFrames)
        {
            StopRecording();
            return;
        }

        SampleAndWrite();
        _frameCount++;
    }

    private void OnDestroy()
    {
        StopRecording();
        DisposeRecorders();
    }

    private void OnApplicationQuit()
    {
        StopRecording();
        DisposeRecorders();
    }

    public void StartRecording()
    {
        if (_isRecording)
        {
            Debug.LogWarning("[ProfilerDataRecorder] Recording already in progress. Stop it before starting a new one.");
            ShowStatus($"Recording already in progress\nStop it before starting a new one\n{_outputFilePath}");
            return;
        }

        _outputFilePath = BuildOutputPath();
        _frameCount = 0;

        try
        {
            _csvWriter = new StreamWriter(
                new FileStream(_outputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read),
                Encoding.UTF8);

            WriteHeader();
            _isRecording = true;

            Debug.Log($"[ProfilerDataRecorder] Recording started. File: {_outputFilePath}");
            ShowStatus($"Recording started\n{_outputFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ProfilerDataRecorder] Failed to open output file: {e.Message}");
            _isRecording = false;
        }
    }

    public void StopRecording()
    {
        if (!_isRecording)
        {
            Debug.LogWarning("[ProfilerDataRecorder] No recording in progress.");
            ShowStatus("No recording in progress");
            return;
        }

        _isRecording = false;

        if (_csvWriter != null)
        {
            _csvWriter.Flush();
            _csvWriter.Close();
            _csvWriter.Dispose();
            _csvWriter = null;
        }

        Debug.Log($"[ProfilerDataRecorder] Recording stopped. Frames recorded: {_frameCount}. File: {_outputFilePath}");
        ShowStatus($"Recording stopped\n{_outputFilePath}");
    }

    private void ShowStatus(string message)
    {
        if (statusPanel == null || statusText == null)
            return;

        statusText.text = message;
        statusPanel.SetActive(true);

        if (_hideCoroutine != null)
            StopCoroutine(_hideCoroutine);

        _hideCoroutine = StartCoroutine(HideAfterDelay(5f));
    }

    private IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        statusPanel.SetActive(false);
    }

    private void WriteHeader()
    {
        // Metadata lines prefixed with # so CSV parsers can skip them.
        _csvWriter.WriteLine($"# ProfilerDataRecorder — Session: {scenarioName}");
        _csvWriter.WriteLine($"# Start timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _csvWriter.WriteLine($"# Unity version: {Application.unityVersion}");
        _csvWriter.WriteLine($"# Platform: {Application.platform}");
        _csvWriter.WriteLine();

        _csvWriter.WriteLine(
            "Frame;" +
            "FrameTime_ms;" +
            "DrawCalls;" +
            "GCAlloc_KB;" +
            "Triangles;" +
            "TotalMemory_MB");
    }

    private void SampleAndWrite()
    {
        // -1 means the metric is unavailable on this platform, not zero.
        double frameTimeMs = _frameTimeRecorder.Valid
            ? _frameTimeRecorder.LastValue / 1_000_000.0
            : -1.0;

        long drawCalls = _drawCallsRecorder.Valid
            ? _drawCallsRecorder.LastValue
            : -1;

        double gcAllocKB = _gcAllocRecorder.Valid
            ? _gcAllocRecorder.LastValue / 1024.0
            : -1.0;

        long triangles = _trianglesRecorder.Valid
            ? _trianglesRecorder.LastValue
            : -1;

        double totalMemoryMB = _totalMemoryRecorder.Valid
            ? _totalMemoryRecorder.LastValue / (1024.0 * 1024.0)
            : -1.0;

        // InvariantCulture ensures dot as decimal separator regardless of system locale.
        // Semicolon as column separator avoids ambiguity with the decimal dot.
        _csvWriter.WriteLine(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0};{1:F3};{2};{3:F2};{4};{5:F2}",
            _frameCount,
            frameTimeMs,
            drawCalls,
            gcAllocKB,
            triangles,
            totalMemoryMB));
    }

    private string BuildOutputPath()
    {
        string dir = string.IsNullOrWhiteSpace(outputDirectory)
            ? Application.streamingAssetsPath
            : outputDirectory;

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{fileNameBase}_{scenarioName}_{timestamp}.csv";

        return Path.Combine(dir, fileName);
    }

    private void DisposeRecorders()
    {
        _frameTimeRecorder.Dispose();
        _drawCallsRecorder.Dispose();
        _gcAllocRecorder.Dispose();
        _trianglesRecorder.Dispose();
        _totalMemoryRecorder.Dispose();
    }
}