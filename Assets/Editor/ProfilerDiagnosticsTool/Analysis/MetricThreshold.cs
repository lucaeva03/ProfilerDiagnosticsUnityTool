namespace ProfilerDiagnosticsTool.Analysis
{
	public readonly struct MetricThreshold
	{
		public MetricName MetricName { get; }
		public float WarningValue { get; }
		public float CriticalValue { get; }

		public MetricThreshold(MetricName metricName, float warningValue, float criticalValue)
		{
			MetricName = metricName;
			WarningValue = warningValue;
			CriticalValue = criticalValue;
		}
	}
}