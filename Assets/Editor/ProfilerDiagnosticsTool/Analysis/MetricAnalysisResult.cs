using System.Collections.Generic;

namespace ProfilerDiagnosticsTool.Analysis
{

	// Analysis outcome for one metric. IsAnalyzable is false only when
	// every frame had an invalid (-1) sample for this metric: in that
	// case DriverValue/Status are null and Suggestion explains why.

	public sealed class MetricAnalysisResult
	{
		public MetricName MetricName { get; }
		public bool IsAnalyzable { get; }
		public float? DriverValue { get; }
		public IReadOnlyDictionary<string, float> SupportingStats { get; }
		public int ExcludedInvalidFrames { get; }
		public SemaphoreStatus? Status { get; }
		public string Suggestion { get; }

		public MetricAnalysisResult(
			MetricName metricName,
			bool isAnalyzable,
			float? driverValue,
			IReadOnlyDictionary<string, float> supportingStats,
			int excludedInvalidFrames,
			SemaphoreStatus? status,
			string suggestion)
		{
			MetricName = metricName;
			IsAnalyzable = isAnalyzable;
			DriverValue = driverValue;
			SupportingStats = supportingStats;
			ExcludedInvalidFrames = excludedInvalidFrames;
			Status = status;
			Suggestion = suggestion;
		}
	}
}