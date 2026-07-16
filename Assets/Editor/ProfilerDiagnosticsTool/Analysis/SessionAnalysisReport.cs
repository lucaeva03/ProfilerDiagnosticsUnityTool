using System.Collections.Generic;
using ProfilerDiagnosticsTool.Data;

namespace ProfilerDiagnosticsTool.Analysis
{
	public sealed class SessionAnalysisReport
	{
		public ProfilerSession SourceSession { get; }
		public ThresholdConfiguration ThresholdsUsed { get; }
		public IReadOnlyList<MetricAnalysisResult> Results { get; }

		public SessionAnalysisReport(
			ProfilerSession sourceSession,
			ThresholdConfiguration thresholdsUsed,
			IReadOnlyList<MetricAnalysisResult> results)
		{
			SourceSession = sourceSession;
			ThresholdsUsed = thresholdsUsed;
			Results = results;
		}
	}
}