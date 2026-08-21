using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ProfilerDiagnosticsTool.Analysis;
using ProfilerDiagnosticsTool.Data;

namespace ProfilerDiagnosticsTool.UI
{
	// Builds a standalone HTML report from a SessionAnalysisReport: no
	// external resources, readable and printable without Unity. Reuses
	// MetricPresentation and MetricValueExtractor so labels, numbers and
	// chart data match the EditorWindow exactly. Charts are hand-built
	// inline SVG (no JS), so the file stays a single self-contained
	// artifact and remains valid when printed.

	public static class HtmlReportExporter
	{
		public static void Save(SessionAnalysisReport report, string filePath)
		{
			string html = Build(report);
			File.WriteAllText(filePath, html, Encoding.UTF8);
		}

		public static string Build(SessionAnalysisReport report)
		{
			var sb = new StringBuilder();

			sb.AppendLine("<!DOCTYPE html>");
			sb.AppendLine("<html lang=\"en\">");
			sb.AppendLine("<head>");
			sb.AppendLine("<meta charset=\"UTF-8\">");
			sb.AppendLine($"<title>Profiler Diagnostics Report — {Escape(report.SourceSession.Metadata.SessionName)}</title>");
			sb.AppendLine(BuildStyle());
			sb.AppendLine("</head>");
			sb.AppendLine("<body>");
			sb.AppendLine("<div class=\"container\">");

			AppendHeader(sb, report);
			AppendExecutiveSummary(sb, report);
			AppendSessionInfo(sb, report);
			AppendResultsTable(sb, report);
			AppendMetricCards(sb, report);
			AppendThresholdsUsed(sb, report);
			AppendSuggestions(sb, report);
			AppendFooter(sb);

			sb.AppendLine("</div>");
			sb.AppendLine("</body>");
			sb.AppendLine("</html>");

			return sb.ToString();
		}

		// --- Header / summary -------------------------------------------------

		private static void AppendHeader(StringBuilder sb, SessionAnalysisReport report)
		{
			sb.AppendLine("<header class=\"report-header\">");
			sb.AppendLine("<h1>Profiler Diagnostics Report</h1>");
			sb.AppendLine($"<p class=\"session-name\">{Escape(report.SourceSession.Metadata.SessionName)}</p>");
			sb.AppendLine("</header>");
		}

		private static void AppendExecutiveSummary(StringBuilder sb, SessionAnalysisReport report)
		{
			var counts = CountByStatus(report);

			sb.AppendLine("<section>");
			sb.AppendLine("<h2>Overview</h2>");
			sb.AppendLine(BuildSummaryBarSvg(counts));

			sb.AppendLine("<p class=\"summary-legend\">");
			sb.AppendLine($"<span class=\"legend-item\"><span class=\"dot\" style=\"background:{MetricPresentation.StatusColorHex(SemaphoreStatus.Critical)}\"></span>{counts.critical} Critical</span>");
			sb.AppendLine($"<span class=\"legend-item\"><span class=\"dot\" style=\"background:{MetricPresentation.StatusColorHex(SemaphoreStatus.Warning)}\"></span>{counts.warning} Warning</span>");
			sb.AppendLine($"<span class=\"legend-item\"><span class=\"dot\" style=\"background:{MetricPresentation.StatusColorHex(SemaphoreStatus.Ok)}\"></span>{counts.ok} Ok</span>");
			if (counts.notAnalyzable > 0)
			{
				sb.AppendLine($"<span class=\"legend-item\"><span class=\"dot\" style=\"background:#B0B0B0\"></span>{counts.notAnalyzable} N/A</span>");
			}
			sb.AppendLine("</p>");
			sb.AppendLine("</section>");
		}

		private static (int critical, int warning, int ok, int notAnalyzable) CountByStatus(SessionAnalysisReport report)
		{
			int critical = 0, warning = 0, ok = 0, notAnalyzable = 0;

			foreach (var result in report.Results)
			{
				if (!result.IsAnalyzable)
				{
					notAnalyzable++;
					continue;
				}

				switch (result.Status.Value)
				{
					case SemaphoreStatus.Critical:
						critical++;
						break;
					case SemaphoreStatus.Warning:
						warning++;
						break;
					case SemaphoreStatus.Ok:
						ok++;
						break;
				}
			}

			return (critical, warning, ok, notAnalyzable);
		}

		private static string BuildSummaryBarSvg((int critical, int warning, int ok, int notAnalyzable) counts)
		{
			const int width = 640;
			const int height = 24;

			int total = counts.critical + counts.warning + counts.ok + counts.notAnalyzable;
			if (total == 0)
			{
				return string.Empty;
			}

			var segments = new (int count, string color)[]
			{
				(counts.critical, MetricPresentation.StatusColorHex(SemaphoreStatus.Critical)),
				(counts.warning, MetricPresentation.StatusColorHex(SemaphoreStatus.Warning)),
				(counts.ok, MetricPresentation.StatusColorHex(SemaphoreStatus.Ok)),
				(counts.notAnalyzable, "#B0B0B0"),
			};

			var sb = new StringBuilder();
			sb.Append($"<svg viewBox=\"0 0 {width} {height}\" class=\"summary-bar\" preserveAspectRatio=\"none\">");

			float x = 0f;
			foreach (var segment in segments)
			{
				if (segment.count == 0)
				{
					continue;
				}

				float segmentWidth = width * segment.count / (float)total;
				sb.Append($"<rect x=\"{F(x)}\" y=\"0\" width=\"{F(segmentWidth)}\" height=\"{height}\" fill=\"{segment.color}\" />");
				x += segmentWidth;
			}

			sb.Append("</svg>");
			return sb.ToString();
		}

		// --- Session info -------------------------------------------------

		private static void AppendSessionInfo(StringBuilder sb, SessionAnalysisReport report)
		{
			var metadata = report.SourceSession.Metadata;

			sb.AppendLine("<section>");
			sb.AppendLine("<h2>Session</h2>");
			sb.AppendLine("<table class=\"info-table\">");
			AppendInfoRow(sb, "Platform", metadata.Platform);
			AppendInfoRow(sb, "Unity version", metadata.UnityVersion);
			AppendInfoRow(sb, "Frames read", report.SourceSession.Frames.Count.ToString(MetricPresentation.DisplayCulture));
			AppendInfoRow(sb, "Source file", metadata.SourceFilePath);
			sb.AppendLine("</table>");
			sb.AppendLine("</section>");
		}

		private static void AppendInfoRow(StringBuilder sb, string label, string value)
		{
			sb.AppendLine($"<tr><td class=\"label\">{Escape(label)}</td><td>{Escape(value)}</td></tr>");
		}

		// --- Results table (quick overview) --------------------------------

		private static void AppendResultsTable(StringBuilder sb, SessionAnalysisReport report)
		{
			sb.AppendLine("<section>");
			sb.AppendLine("<h2>Results</h2>");
			sb.AppendLine("<table class=\"results-table\">");
			sb.AppendLine("<tr><th>Metric</th><th>Status</th><th>Driver</th><th>Mean</th><th>Max</th><th>% over critical</th><th>Invalid frames</th></tr>");

			foreach (var result in report.Results)
			{
				AppendResultRow(sb, result);
			}

			sb.AppendLine("</table>");
			sb.AppendLine("</section>");
		}

		private static void AppendResultRow(StringBuilder sb, MetricAnalysisResult result)
		{
			string metricName = Escape(MetricPresentation.MetricDisplayName(result.MetricName));

			if (!result.IsAnalyzable)
			{
				sb.AppendLine(
					$"<tr><td>{metricName}</td><td>N/A</td><td>-</td><td>-</td><td>-</td><td>-</td>" +
					$"<td>{result.ExcludedInvalidFrames.ToString(MetricPresentation.DisplayCulture)}</td></tr>");
				return;
			}

			string statusColor = MetricPresentation.StatusColorHex(result.Status.Value);
			string statusText = MetricPresentation.StatusText(result.Status.Value);

			sb.AppendLine("<tr>");
			sb.AppendLine($"<td>{metricName}</td>");
			sb.AppendLine($"<td><span class=\"status-badge\" style=\"background:{statusColor}\">{statusText}</span></td>");
			sb.AppendLine($"<td>{MetricPresentation.FormatNumber(result.DriverValue.Value)}</td>");
			sb.AppendLine($"<td>{MetricPresentation.FormatNumber(result.SupportingStats["mean"])}</td>");
			sb.AppendLine($"<td>{MetricPresentation.FormatNumber(result.SupportingStats["max"])}</td>");
			sb.AppendLine($"<td>{MetricPresentation.FormatNumber(result.SupportingStats["percentOverCritical"])}%</td>");
			sb.AppendLine($"<td>{result.ExcludedInvalidFrames.ToString(MetricPresentation.DisplayCulture)}</td>");
			sb.AppendLine("</tr>");
		}

		// --- Metric detail cards with per-frame chart -----------------------

		private static void AppendMetricCards(StringBuilder sb, SessionAnalysisReport report)
		{
			sb.AppendLine("<section>");
			sb.AppendLine("<h2>Metric Trends</h2>");
			sb.AppendLine("<div class=\"card-grid\">");

			foreach (var result in report.Results)
			{
				AppendMetricCard(sb, report, result);
			}

			sb.AppendLine("</div>");
			sb.AppendLine("</section>");
		}

		private static void AppendMetricCard(StringBuilder sb, SessionAnalysisReport report, MetricAnalysisResult result)
		{
			string borderColor = result.IsAnalyzable ? MetricPresentation.StatusColorHex(result.Status.Value) : "#B0B0B0";

			sb.AppendLine($"<div class=\"metric-card\" style=\"border-left-color:{borderColor}\">");
			sb.AppendLine("<div class=\"metric-card-header\">");
			sb.AppendLine($"<h3>{Escape(MetricPresentation.MetricDisplayName(result.MetricName))}</h3>");

			string badgeText = result.IsAnalyzable ? MetricPresentation.StatusText(result.Status.Value) : "N/A";
			sb.AppendLine($"<span class=\"status-badge\" style=\"background:{borderColor}\">{badgeText}</span>");
			sb.AppendLine("</div>");

			if (!result.IsAnalyzable)
			{
				sb.AppendLine($"<p class=\"muted\">{Escape(result.Suggestion)}</p>");
				sb.AppendLine("</div>");
				return;
			}

			sb.AppendLine("<div class=\"metric-stats\">");
			sb.AppendLine($"<span><strong>Driver</strong> {MetricPresentation.FormatNumber(result.DriverValue.Value)}</span>");
			sb.AppendLine($"<span><strong>Mean</strong> {MetricPresentation.FormatNumber(result.SupportingStats["mean"])}</span>");
			sb.AppendLine($"<span><strong>Max</strong> {MetricPresentation.FormatNumber(result.SupportingStats["max"])}</span>");
			sb.AppendLine($"<span><strong>% over critical</strong> {MetricPresentation.FormatNumber(result.SupportingStats["percentOverCritical"])}%</span>");
			sb.AppendLine("</div>");

			MetricThreshold threshold = report.ThresholdsUsed.Get(result.MetricName);
			List<float> values = MetricValueExtractor.ExtractValues(report.SourceSession, result.MetricName);

			sb.AppendLine(BuildLineChartSvg(values, threshold));
			sb.AppendLine("<p class=\"chart-caption\">Yellow line = Warning · Red line = Critical</p>");

			if (!string.IsNullOrEmpty(result.Suggestion))
			{
				sb.AppendLine($"<p class=\"card-suggestion\">{Escape(result.Suggestion)}</p>");
			}

			sb.AppendLine("</div>");
		}

		private static string BuildLineChartSvg(List<float> values, MetricThreshold threshold)
		{
			if (values.Count < 2)
			{
				return "<p class=\"muted\">Not enough data to draw a chart.</p>";
			}

			const int width = 560;
			const int height = 130;
			const int padding = 8;

			float min = Math.Min(0f, values.Min());
			float max = Math.Max(values.Max(), threshold.CriticalValue) * 1.05f;
			if (Math.Abs(max - min) < 0.0001f)
			{
				max = min + 1f;
			}

			float warningY = ValueToY(threshold.WarningValue, min, max, height, padding);
			float criticalY = ValueToY(threshold.CriticalValue, min, max, height, padding);
			string points = BuildPolylinePoints(values, min, max, width, height, padding);

			var sb = new StringBuilder();
			sb.Append($"<svg viewBox=\"0 0 {width} {height}\" class=\"metric-chart\" preserveAspectRatio=\"none\">");
			sb.Append($"<line x1=\"0\" y1=\"{F(warningY)}\" x2=\"{width}\" y2=\"{F(warningY)}\" class=\"threshold-line warning-line\" />");
			sb.Append($"<line x1=\"0\" y1=\"{F(criticalY)}\" x2=\"{width}\" y2=\"{F(criticalY)}\" class=\"threshold-line critical-line\" />");
			sb.Append($"<polyline points=\"{points}\" class=\"data-line\" />");
			sb.Append("</svg>");

			return sb.ToString();
		}

		private static string BuildPolylinePoints(List<float> values, float min, float max, int width, int height, int padding)
		{
			var sb = new StringBuilder();

			for (int i = 0; i < values.Count; i++)
			{
				float x = width * i / (float)(values.Count - 1);
				float y = ValueToY(values[i], min, max, height, padding);

				if (i > 0)
				{
					sb.Append(' ');
				}

				sb.Append(F(x)).Append(',').Append(F(y));
			}

			return sb.ToString();
		}

		private static float ValueToY(float value, float min, float max, int height, int padding)
		{
			float t = (value - min) / (max - min);
			t = Math.Max(0f, Math.Min(1f, t));
			return padding + (1f - t) * (height - 2 * padding);
		}

		// SVG coordinates always use '.' as decimal separator regardless of
		// the report's display culture (comma) - required by the SVG spec.
		private static string F(float value)
		{
			return value.ToString("F1", CultureInfo.InvariantCulture);
		}

		// --- Thresholds / suggestions / footer -------------------------------

		private static void AppendThresholdsUsed(StringBuilder sb, SessionAnalysisReport report)
		{
			sb.AppendLine("<section>");
			sb.AppendLine("<h2>Thresholds Used</h2>");
			sb.AppendLine("<table class=\"info-table\">");
			sb.AppendLine("<tr><th>Metric</th><th>Warning</th><th>Critical</th></tr>");

			foreach (MetricName metric in Enum.GetValues(typeof(MetricName)))
			{
				MetricThreshold threshold = report.ThresholdsUsed.Get(metric);
				sb.AppendLine(
					$"<tr><td>{Escape(MetricPresentation.MetricDisplayName(metric))}</td>" +
					$"<td>{MetricPresentation.FormatNumber(threshold.WarningValue)}</td>" +
					$"<td>{MetricPresentation.FormatNumber(threshold.CriticalValue)}</td></tr>");
			}

			sb.AppendLine("</table>");
			sb.AppendLine("</section>");
		}

		private static void AppendSuggestions(StringBuilder sb, SessionAnalysisReport report)
		{
			sb.AppendLine("<section>");
			sb.AppendLine("<h2>Action Items</h2>");

			bool any = false;
			foreach (var result in report.Results)
			{
				if (string.IsNullOrEmpty(result.Suggestion))
				{
					continue;
				}

				any = true;
				string cssClass = result.IsAnalyzable && result.Status == SemaphoreStatus.Critical ? "suggestion critical" : "suggestion warning";
				sb.AppendLine($"<div class=\"{cssClass}\">");
				sb.AppendLine($"<strong>{Escape(MetricPresentation.MetricDisplayName(result.MetricName))}</strong>");
				sb.AppendLine($"<p>{Escape(result.Suggestion)}</p>");
				sb.AppendLine("</div>");
			}

			if (!any)
			{
				sb.AppendLine("<p>No issues detected: all analyzable metrics are in the Ok state.</p>");
			}

			sb.AppendLine("</section>");
		}

		private static void AppendFooter(StringBuilder sb)
		{
			string generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm", MetricPresentation.DisplayCulture);
			sb.AppendLine("<footer>");
			sb.AppendLine($"<p>Generated on {generatedAt} — Profiler Diagnostics Tool</p>");
			sb.AppendLine("</footer>");
		}

		private static string BuildStyle()
		{
			return @"<style>
				:root {
					--ink: #222;
					--muted: #6b6b6b;
					--border: #e4e4e4;
					--card-bg: #fff;
					--page-bg: #f7f7f8;
				}
				* { box-sizing: border-box; }
				body { font-family: -apple-system, Segoe UI, Roboto, Arial, sans-serif; margin: 0; color: var(--ink); background: var(--page-bg); }
				.container { max-width: 960px; margin: 0 auto; padding: 40px 24px 60px; }
				.report-header { padding: 28px 32px; margin-bottom: 24px; border-radius: 12px; color: #fff; background: linear-gradient(135deg, #2b6cb0, #2c5282); }
				.report-header h1 { margin: 0 0 4px; font-size: 1.6em; }
				.session-name { margin: 0; opacity: 0.9; }
				section { margin-top: 32px; }
				h2 { font-size: 1.05em; text-transform: uppercase; letter-spacing: 0.04em; color: var(--muted); border-bottom: 1px solid var(--border); padding-bottom: 6px; }
				table { border-collapse: collapse; width: 100%; margin-top: 10px; background: var(--card-bg); border-radius: 8px; overflow: hidden; }
				th, td { text-align: left; padding: 8px 12px; border-bottom: 1px solid var(--border); font-size: 0.95em; }
				th { background: #f0f2f5; font-weight: 600; }
				tr:last-child td { border-bottom: none; }
				.info-table td.label { color: var(--muted); width: 160px; }
				.status-badge { color: #fff; padding: 3px 12px; border-radius: 12px; font-size: 0.85em; font-weight: 600; }
				.summary-bar { width: 100%; height: 24px; border-radius: 6px; overflow: hidden; display: block; }
				.summary-legend { margin-top: 10px; }
				.legend-item { display: inline-flex; align-items: center; margin-right: 18px; font-size: 0.9em; color: var(--muted); }
				.dot { display: inline-block; width: 10px; height: 10px; border-radius: 50%; margin-right: 6px; }
				.card-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(420px, 1fr)); gap: 16px; margin-top: 14px; }
				.metric-card { background: var(--card-bg); border: 1px solid var(--border); border-left: 5px solid #999; border-radius: 10px; padding: 16px 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.06); }
				.metric-card-header { display: flex; justify-content: space-between; align-items: center; }
				.metric-card-header h3 { margin: 0; font-size: 1.05em; }
				.metric-stats { display: flex; flex-wrap: wrap; gap: 14px; margin: 10px 0; font-size: 0.9em; color: var(--muted); }
				.metric-stats strong { color: var(--ink); display: block; font-size: 0.8em; text-transform: uppercase; letter-spacing: 0.03em; }
				.metric-chart { width: 100%; height: 130px; background: #fbfbfc; border-radius: 6px; margin-top: 6px; }
				.data-line { fill: none; stroke: #2b6cb0; stroke-width: 2; }
				.threshold-line { stroke-width: 1.5; stroke-dasharray: 4 3; }
				.warning-line { stroke: #d9a406; }
				.critical-line { stroke: #c53030; }
				.chart-caption, .muted { color: var(--muted); font-size: 0.82em; margin: 6px 0 0; }
				.card-suggestion { margin: 10px 0 0; font-size: 0.88em; background: #f7f7f8; border-radius: 6px; padding: 8px 10px; }
				.suggestion { border-left: 4px solid #ccc; padding: 8px 14px; margin: 10px 0; background: var(--card-bg); border-radius: 0 6px 6px 0; }
				.suggestion.critical { border-left-color: #c53030; }
				.suggestion.warning { border-left-color: #d9a406; }
				footer { margin-top: 48px; color: var(--muted); font-size: 0.85em; text-align: center; }
				@media print {
					body { background: #fff; }
					.container { max-width: none; padding: 10mm; }
					.metric-card { box-shadow: none; break-inside: avoid; }
					.report-header { background: #2c5282 !important; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
				}
				</style>";
		}

		private static string Escape(string value)
		{
			return WebUtility.HtmlEncode(value ?? string.Empty);
		}
	}
}