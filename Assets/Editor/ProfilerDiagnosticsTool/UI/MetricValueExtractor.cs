using System.Collections.Generic;
using ProfilerDiagnosticsTool.Analysis;
using ProfilerDiagnosticsTool.Data;

namespace ProfilerDiagnosticsTool.UI
{
	// Single source of truth for reading a metric's raw per-frame series
	// out of a ProfilerSession. Used by both the EditorWindow chart and
	// the HTML export chart, so they always plot the same data. Invalid
	// (-1 -> null) samples are excluded here, same rule as the Data Layer;
	// anomalous real values are never filtered.

	public static class MetricValueExtractor
	{
		public static List<float> ExtractValues(ProfilerSession session, MetricName metric)
		{
			var values = new List<float>();

			foreach (var frame in session.Frames)
			{
				float? value = SelectRawValue(frame, metric);
				if (value.HasValue)
				{
					values.Add(value.Value);
				}
			}

			return values;
		}

		private static float? SelectRawValue(ProfilerFrameRecord frame, MetricName metric)
		{
			switch (metric)
			{
				case MetricName.FrameTime:
					return frame.FrameTimeMs;
				case MetricName.DrawCalls:
					return frame.DrawCalls;
				case MetricName.GcAlloc:
					return frame.GcAllocKb;
				case MetricName.Triangles:
					return frame.Triangles;
				case MetricName.Memory:
					return frame.TotalMemoryMb;
				default:
					return null;
			}
		}
	}
}