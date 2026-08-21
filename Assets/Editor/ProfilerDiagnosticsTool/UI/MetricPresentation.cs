using System.Globalization;
using ProfilerDiagnosticsTool.Analysis;
using UnityEngine;

namespace ProfilerDiagnosticsTool.UI
{
	// Single source of truth for how metrics/status/numbers are displayed,
	// shared between the EditorWindow and the HTML exporter so the two
	// never show different labels or formats for the same data.

	public static class MetricPresentation
	{
		public static readonly CultureInfo DisplayCulture = new CultureInfo("it-IT");

		public static string MetricDisplayName(MetricName metric)
		{
			switch (metric)
			{
				case MetricName.FrameTime:
					return "Frame Time (ms)";
				case MetricName.DrawCalls:
					return "Draw Calls";
				case MetricName.GcAlloc:
					return "GC Alloc (KB)";
				case MetricName.Triangles:
					return "Triangles";
				case MetricName.Memory:
					return "Memory (MB)";
				default:
					return metric.ToString();
			}
		}

		public static string StatusText(SemaphoreStatus status)
		{
			switch (status)
			{
				case SemaphoreStatus.Ok:
					return "Ok";
				case SemaphoreStatus.Warning:
					return "Warning";
				case SemaphoreStatus.Critical:
					return "Critical";
				default:
					return "-";
			}
		}

		public static string FormatNumber(float value)
		{
			return value.ToString("F2", DisplayCulture);
		}

		public static Color StatusColor(SemaphoreStatus status)
		{
			switch (status)
			{
				case SemaphoreStatus.Ok:
					return new Color(0.35f, 0.8f, 0.35f);
				case SemaphoreStatus.Warning:
					return new Color(0.95f, 0.75f, 0.15f);
				case SemaphoreStatus.Critical:
					return new Color(0.9f, 0.3f, 0.3f);
				default:
					return Color.white;
			}
		}

		public static string StatusColorHex(SemaphoreStatus status)
		{
			switch (status)
			{
				case SemaphoreStatus.Ok:
					return "#59CC59";
				case SemaphoreStatus.Warning:
					return "#F2BF26";
				case SemaphoreStatus.Critical:
					return "#E64D4D";
				default:
					return "#999999";
			}
		}
	}
}