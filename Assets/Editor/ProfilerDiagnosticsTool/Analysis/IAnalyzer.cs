using ProfilerDiagnosticsTool.Data;

namespace ProfilerDiagnosticsTool.Analysis
{
	public interface IAnalyzer
	{
		SessionAnalysisReport Analyze(ProfilerSession session, ThresholdConfiguration thresholds);
	}
}