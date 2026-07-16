using System.Collections.Generic;

namespace ProfilerDiagnosticsTool.Analysis
{

	// Set of thresholds for all 5 metrics. Defaults come from the
	// empirical Fase 2.6 analysis on the Infotainment project; fully
	// user-editable and not hardcoded into the analysis logic.

	public sealed class ThresholdConfiguration
	{
		private readonly Dictionary<MetricName, MetricThreshold> _thresholds;

		public ThresholdConfiguration(IReadOnlyDictionary<MetricName, MetricThreshold> thresholds)
		{
			_thresholds = new Dictionary<MetricName, MetricThreshold>(thresholds);
		}

		public MetricThreshold Get(MetricName metric)
		{
			return _thresholds[metric];
		}

		public static ThresholdConfiguration CreateDefault()
		{
			var defaults = new Dictionary<MetricName, MetricThreshold>
			{
				{ MetricName.FrameTime, new MetricThreshold(MetricName.FrameTime, 16.7f, 33.4f) },
				{ MetricName.DrawCalls, new MetricThreshold(MetricName.DrawCalls, 100f, 200f) },
				{ MetricName.GcAlloc, new MetricThreshold(MetricName.GcAlloc, 2.5f, 5.0f) },
				{ MetricName.Triangles, new MetricThreshold(MetricName.Triangles, 600000f, 1200000f) },
				{ MetricName.Memory, new MetricThreshold(MetricName.Memory, 450f, 900f) },
			};

			return new ThresholdConfiguration(defaults);
		}
	}
}