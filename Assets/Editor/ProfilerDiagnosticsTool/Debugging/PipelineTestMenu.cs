using ProfilerDiagnosticsTool.Analysis;
using ProfilerDiagnosticsTool.Data;
using UnityEditor;
using UnityEngine;

namespace ProfilerDiagnosticsTool.Debugging
{

	// Temporary test: runs Data + Analysis layers on a chosen CSV and logs the result.

	public static class PipelineTestMenu
	{
		[MenuItem("Tools/Profiler Diagnostics/Test Pipeline On CSV...")]
		public static void TestPipeline()
		{
			string path = EditorUtility.OpenFilePanel("Select profiler CSV", "", "csv");
			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			IDataProvider parser = new CsvSessionParser();
			CsvParseResult parseResult = parser.Parse(path);

			if (!parseResult.Success)
			{
				Debug.LogError($"Parse failed: {parseResult.ErrorMessage}");
				return;
			}

			var session = parseResult.Session;
			Debug.Log($"Parsed session '{session.Metadata.SessionName}' ({session.Metadata.Platform}, Unity {session.Metadata.UnityVersion}) - {session.Frames.Count} frames.");

			foreach (var issue in parseResult.Issues)
			{
				Debug.LogWarning(issue.ToString());
			}

			IAnalyzer analyzer = new ThresholdAnalyzer();
			ThresholdConfiguration thresholds = ThresholdConfiguration.CreateDefault();
			SessionAnalysisReport report = analyzer.Analyze(session, thresholds);

			foreach (var result in report.Results)
			{
				if (!result.IsAnalyzable)
				{
					Debug.LogWarning($"{result.MetricName}: NOT ANALYZABLE - {result.Suggestion}");
					continue;
				}

				Debug.Log(
					$"{result.MetricName}: {result.Status} | driver={result.DriverValue:F2} | " +
					$"mean={result.SupportingStats["mean"]:F2} | max={result.SupportingStats["max"]:F2} | " +
					$"%overCritical={result.SupportingStats["percentOverCritical"]:F1}% | " +
					$"invalidFrames={result.ExcludedInvalidFrames}");

				if (result.Suggestion != null)
				{
					Debug.Log($"  -> {result.Suggestion}");
				}
			}
		}
	}
}