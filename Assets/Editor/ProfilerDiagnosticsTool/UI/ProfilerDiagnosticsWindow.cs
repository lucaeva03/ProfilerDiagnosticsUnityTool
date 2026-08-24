using System.Collections.Generic;
using System.Linq;
using ProfilerDiagnosticsTool.Analysis;
using ProfilerDiagnosticsTool.Data;
using UnityEditor;
using UnityEngine;

namespace ProfilerDiagnosticsTool.UI
{
	public class ProfilerDiagnosticsWindow : EditorWindow
	{
		private static readonly string[] TabNames = { "Load", "Results", "Suggestions", "Threshold Configuration" };
		private int _selectedTab;

		private readonly IDataProvider _dataProvider = new CsvSessionParser();
		private readonly IAnalyzer _analyzer = new ThresholdAnalyzer();

		private string _csvPath = "";
		private ProfilerSession _session;
		private IReadOnlyList<CsvParseIssue> _parseIssues = new List<CsvParseIssue>();
		private string _parseError;

		private SessionAnalysisReport _report;
		private ThresholdEditableSet _editableThresholds;

		private Vector2 _issuesScroll;
		private Vector2 _resultsScroll;
		private Vector2 _suggestionsScroll;
		private Vector2 _thresholdsScroll;

		private readonly Dictionary<MetricName, bool> _graphExpanded = new Dictionary<MetricName, bool>();

		[MenuItem("Tools/Profiler Diagnostics/Open Profiler Diagnostics Window")]
		public static void ShowWindow()
		{
			var window = GetWindow<ProfilerDiagnosticsWindow>("Profiler Diagnostics");
			window.minSize = new Vector2(560, 420);
		}

		private void OnEnable()
		{
			_editableThresholds = ThresholdPersistence.Load();
		}

		private void OnGUI()
		{
			_selectedTab = GUILayout.Toolbar(_selectedTab, TabNames);
			EditorGUILayout.Space(8);

			switch (_selectedTab)
			{
				case 0:
					DrawLoadTab();
					break;
				case 1:
					DrawResultsTab();
					break;
				case 2:
					DrawSuggestionsTab();
					break;
				case 3:
					DrawThresholdsTab();
					break;
			}
		}

		// --- Tab: Load ---------------------------------------------

		private void DrawLoadTab()
		{
			EditorGUILayout.LabelField("CSV File", EditorStyles.boldLabel);

			EditorGUILayout.BeginHorizontal();
			_csvPath = EditorGUILayout.TextField(_csvPath);
			if (GUILayout.Button("Browse...", GUILayout.Width(80)))
			{
				string picked = EditorUtility.OpenFilePanel("Select profiler CSV", "", "csv");
				if (!string.IsNullOrEmpty(picked))
				{
					_csvPath = picked;
				}
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(6);

			GUI.enabled = !string.IsNullOrEmpty(_csvPath);
			if (GUILayout.Button("Analyze", GUILayout.Height(28)))
			{
				RunPipeline();
			}
			GUI.enabled = true;

			EditorGUILayout.Space(10);

			if (_parseError != null)
			{
				EditorGUILayout.HelpBox(_parseError, MessageType.Error);
				return;
			}

			if (_session == null)
			{
				EditorGUILayout.HelpBox("Select a CSV file and press Analyze.", MessageType.Info);
				return;
			}

			EditorGUILayout.LabelField("Session metadata", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("Session name", _session.Metadata.SessionName);
			EditorGUILayout.LabelField("Platform", _session.Metadata.Platform);
			EditorGUILayout.LabelField("Unity version", _session.Metadata.UnityVersion);
			EditorGUILayout.LabelField("Frames read", _session.Frames.Count.ToString(MetricPresentation.DisplayCulture));

			if (_parseIssues.Count == 0)
			{
				return;
			}

			EditorGUILayout.Space(8);
			EditorGUILayout.LabelField($"Parsing warnings ({_parseIssues.Count})", EditorStyles.boldLabel);

			_issuesScroll = EditorGUILayout.BeginScrollView(_issuesScroll, GUILayout.Height(120));
			foreach (var issue in _parseIssues)
			{
				MessageType type = issue.Severity == CsvParseIssueSeverity.Warning ? MessageType.Warning : MessageType.Info;
				EditorGUILayout.HelpBox(issue.ToString(), type);
			}
			EditorGUILayout.EndScrollView();
		}

		private void RunPipeline()
		{
			_parseError = null;
			_session = null;
			_report = null;

			CsvParseResult parseResult = _dataProvider.Parse(_csvPath);
			_parseIssues = parseResult.Issues;

			if (!parseResult.Success)
			{
				_parseError = parseResult.ErrorMessage;
				return;
			}

			_session = parseResult.Session;
			_report = _analyzer.Analyze(_session, _editableThresholds.ToConfiguration());
		}

		// --- Tab: Results -------------------------------------------------

		private void DrawResultsTab()
		{
			if (_report == null)
			{
				EditorGUILayout.HelpBox("No analysis available. Go to the Load tab and press Analyze.", MessageType.Info);
				return;
			}

			_resultsScroll = EditorGUILayout.BeginScrollView(_resultsScroll);

			DrawResultsSummary();
			DrawResultsHeader();
			foreach (var result in _report.Results)
			{
				DrawMetricRow(result);

				bool expanded = result.IsAnalyzable
					&& _graphExpanded.TryGetValue(result.MetricName, out bool isExpanded)
					&& isExpanded;

				if (expanded)
				{
					MetricThreshold threshold = _report.ThresholdsUsed.Get(result.MetricName);
					DrawMetricGraph(result.MetricName, threshold);
					EditorGUILayout.Space(6);
				}
			}

			EditorGUILayout.Space(14);
			if (GUILayout.Button("Export HTML"))
			{
				ExportHtml();
			}

			EditorGUILayout.EndScrollView();
		}

		private void ExportHtml()
		{
			string defaultName = $"ProfilerReport_{_session.Metadata.SessionName}.html";
			string path = EditorUtility.SaveFilePanel("Export HTML report", "", defaultName, "html");

			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			HtmlReportExporter.Save(_report, path);
			EditorUtility.RevealInFinder(path);
		}

		private void DrawResultsSummary()
		{
			int ok = 0, warning = 0, critical = 0, notAnalyzable = 0;

			foreach (var result in _report.Results)
			{
				if (!result.IsAnalyzable)
				{
					notAnalyzable++;
					continue;
				}

				switch (result.Status.Value)
				{
					case SemaphoreStatus.Ok:
						ok++;
						break;
					case SemaphoreStatus.Warning:
						warning++;
						break;
					case SemaphoreStatus.Critical:
						critical++;
						break;
				}
			}

			string summary = $"{critical} Critical · {warning} Warning · {ok} Ok";
			if (notAnalyzable > 0)
			{
				summary += $" · {notAnalyzable} N/A";
			}

			EditorGUILayout.LabelField(summary, EditorStyles.boldLabel);
			EditorGUILayout.Space(6);
		}

		private void DrawResultsHeader()
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			GUILayout.Label("Metric", EditorStyles.boldLabel, GUILayout.Width(120));
			GUILayout.Label("Status", EditorStyles.boldLabel, GUILayout.Width(65));
			GUILayout.Label("Driver", EditorStyles.boldLabel, GUILayout.Width(80));
			GUILayout.Label("Mean", EditorStyles.boldLabel, GUILayout.Width(80));
			GUILayout.Label("Max", EditorStyles.boldLabel, GUILayout.Width(80));
			GUILayout.Label("% over critical", EditorStyles.boldLabel, GUILayout.Width(95));
			GUILayout.Label("Invalid frames", EditorStyles.boldLabel, GUILayout.Width(95));
			GUILayout.Label("", GUILayout.Width(80));
			EditorGUILayout.EndHorizontal();
		}

		private void DrawMetricRow(MetricAnalysisResult result)
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label(MetricPresentation.MetricDisplayName(result.MetricName), GUILayout.Width(120));

			if (!result.IsAnalyzable)
			{
				GUILayout.Label("N/A", GUILayout.Width(65));
				GUILayout.Label("-", GUILayout.Width(80));
				GUILayout.Label("-", GUILayout.Width(80));
				GUILayout.Label("-", GUILayout.Width(80));
				GUILayout.Label("-", GUILayout.Width(95));
				GUILayout.Label(result.ExcludedInvalidFrames.ToString(MetricPresentation.DisplayCulture), GUILayout.Width(95));
				GUILayout.Label("", GUILayout.Width(80));
				EditorGUILayout.EndHorizontal();
				return;
			}

			DrawStatusLabel(result.Status.Value);
			GUILayout.Label(MetricPresentation.FormatNumber(result.DriverValue.Value), GUILayout.Width(80));
			GUILayout.Label(MetricPresentation.FormatNumber(result.SupportingStats["mean"]), GUILayout.Width(80));
			GUILayout.Label(MetricPresentation.FormatNumber(result.SupportingStats["max"]), GUILayout.Width(80));
			GUILayout.Label(MetricPresentation.FormatNumber(result.SupportingStats["percentOverCritical"]) + "%", GUILayout.Width(95));
			GUILayout.Label(result.ExcludedInvalidFrames.ToString(MetricPresentation.DisplayCulture), GUILayout.Width(95));

			bool expanded = _graphExpanded.TryGetValue(result.MetricName, out bool current) && current;
			if (GUILayout.Button(expanded ? "Close Chart" : "Open Chart", GUILayout.Width(80)))
			{
				_graphExpanded[result.MetricName] = !expanded;
			}

			EditorGUILayout.EndHorizontal();
		}

		private void DrawStatusLabel(SemaphoreStatus status)
		{
			Color previous = GUI.color;
			GUI.color = MetricPresentation.StatusColor(status);
			GUILayout.Label(MetricPresentation.StatusText(status), GUILayout.Width(65));
			GUI.color = previous;
		}

		// --- Per-frame chart (Phase 3.1, Results tab) -----------------------

		private void DrawMetricGraph(MetricName metric, MetricThreshold threshold)
		{
			List<float> values = MetricValueExtractor.ExtractValues(_session, metric);

			if (values.Count < 2)
			{
				EditorGUILayout.HelpBox("Not enough data to draw the chart.", MessageType.Info);
				return;
			}

			Rect rect = GUILayoutUtility.GetRect(10, 110, GUILayout.ExpandWidth(true));
			EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

			float min = Mathf.Min(0f, values.Min());
			float max = Mathf.Max(values.Max(), threshold.CriticalValue) * 1.05f;
			if (Mathf.Approximately(max, min))
			{
				max = min + 1f;
			}

			Handles.BeginGUI();

			DrawThresholdLine(rect, min, max, threshold.WarningValue, new Color(0.95f, 0.75f, 0.15f));
			DrawThresholdLine(rect, min, max, threshold.CriticalValue, new Color(0.9f, 0.3f, 0.3f));
			DrawValuesLine(rect, min, max, values);

			Handles.EndGUI();

			EditorGUILayout.LabelField(
				$"Valid frames shown: {values.Count} (yellow line = Warning, red line = Critical)",
				EditorStyles.miniLabel);
		}

		// Values in frame order, invalid (-1 -> null) samples already excluded
		// by the Data Layer. Anomalous real values are never filtered here.
		private static void DrawValuesLine(Rect rect, float min, float max, List<float> values)
		{
			var points = new Vector3[values.Count];

			for (int i = 0; i < values.Count; i++)
			{
				float x = rect.x + rect.width * i / (values.Count - 1);
				float t = Mathf.InverseLerp(min, max, values[i]);
				float y = rect.yMax - t * rect.height;
				points[i] = new Vector3(x, y);
			}

			Handles.color = new Color(0.3f, 0.7f, 1f);
			Handles.DrawAAPolyLine(2f, points);
		}

		private static void DrawThresholdLine(Rect rect, float min, float max, float value, Color color)
		{
			if (value < min || value > max)
			{
				return;
			}

			float t = Mathf.InverseLerp(min, max, value);
			float y = rect.yMax - t * rect.height;

			Handles.color = color;
			Handles.DrawAAPolyLine(2f, new Vector3(rect.x, y), new Vector3(rect.xMax, y));
		}

		// --- Tab: Suggestions -----------------------------------------------

		private void DrawSuggestionsTab()
		{
			if (_report == null)
			{
				EditorGUILayout.HelpBox("No analysis available. Go to the Load tab and press Analyze.", MessageType.Info);
				return;
			}

			_suggestionsScroll = EditorGUILayout.BeginScrollView(_suggestionsScroll);

			bool anySuggestion = false;
			foreach (var result in _report.Results)
			{
				if (string.IsNullOrEmpty(result.Suggestion))
				{
					continue;
				}

				anySuggestion = true;
				MessageType type = result.IsAnalyzable && result.Status == SemaphoreStatus.Critical
					? MessageType.Error
					: MessageType.Warning;

				EditorGUILayout.LabelField(MetricPresentation.MetricDisplayName(result.MetricName), EditorStyles.boldLabel);
				EditorGUILayout.HelpBox(result.Suggestion, type);
				EditorGUILayout.Space(6);
			}

			if (!anySuggestion)
			{
				EditorGUILayout.HelpBox("No issues detected: all analyzable metrics are in the Ok state.", MessageType.Info);
			}

			EditorGUILayout.EndScrollView();
		}

		// --- Tab: Threshold Configuration ---------------------------------------

		private void DrawThresholdsTab()
		{
			_thresholdsScroll = EditorGUILayout.BeginScrollView(_thresholdsScroll);

			EditorGUILayout.HelpBox(
				"Default values are derived from the empirical analysis on the Infotainment project. They are fully editable and persisted across Editor sessions.",
				MessageType.Info);
			EditorGUILayout.Space(8);

			DrawThresholdHeader();
			DrawThresholdRow("Frame Time (ms)", ref _editableThresholds.frameTimeWarning, ref _editableThresholds.frameTimeCritical);
			DrawThresholdRow("Draw Calls", ref _editableThresholds.drawCallsWarning, ref _editableThresholds.drawCallsCritical);
			DrawThresholdRow("GC Alloc (KB)", ref _editableThresholds.gcAllocWarning, ref _editableThresholds.gcAllocCritical);
			DrawThresholdRow("Triangles", ref _editableThresholds.trianglesWarning, ref _editableThresholds.trianglesCritical);
			DrawThresholdRow("Memory (MB)", ref _editableThresholds.memoryWarning, ref _editableThresholds.memoryCritical);

			EditorGUILayout.Space(12);

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Save configuration"))
			{
				ApplyThresholds();
			}
			if (GUILayout.Button("Reset to defaults"))
			{
				_editableThresholds = ThresholdEditableSet.CreateDefault();
				ApplyThresholds();
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndScrollView();
		}

		private void DrawThresholdHeader()
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			GUILayout.Label("Metric", EditorStyles.boldLabel, GUILayout.Width(120));
			GUILayout.Label("Warning", EditorStyles.boldLabel, GUILayout.Width(90));
			GUILayout.Label("Critical", EditorStyles.boldLabel, GUILayout.Width(90));
			EditorGUILayout.EndHorizontal();
		}

		private void DrawThresholdRow(string label, ref float warning, ref float critical)
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label(label, GUILayout.Width(120));
			warning = EditorGUILayout.FloatField(warning, GUILayout.Width(90));
			critical = EditorGUILayout.FloatField(critical, GUILayout.Width(90));
			EditorGUILayout.EndHorizontal();
		}

		private void ApplyThresholds()
		{
			ThresholdPersistence.Save(_editableThresholds);

			if (_session != null)
			{
				_report = _analyzer.Analyze(_session, _editableThresholds.ToConfiguration());
			}
		}
	}
}