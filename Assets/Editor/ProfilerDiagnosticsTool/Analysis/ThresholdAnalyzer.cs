using System;
using System.Collections.Generic;
using System.Linq;
using ProfilerDiagnosticsTool.Data;

namespace ProfilerDiagnosticsTool.Analysis
{

	// Compares a ProfilerSession against a ThresholdConfiguration and
	// produces a per-metric verdict. Driver statistic is mean for
	// FrameTime/DrawCalls/Triangles, max for GcAlloc/Memory

	public sealed class ThresholdAnalyzer : IAnalyzer
	{
		public SessionAnalysisReport Analyze(ProfilerSession session, ThresholdConfiguration thresholds)
		{
			var results = new List<MetricAnalysisResult>
			{
				AnalyzeMetric(MetricName.FrameTime, session, thresholds, f => f.FrameTimeMs, useMax: false),
				AnalyzeMetric(MetricName.DrawCalls, session, thresholds, f => f.DrawCalls, useMax: false),
				AnalyzeMetric(MetricName.GcAlloc, session, thresholds, f => f.GcAllocKb, useMax: true),
				AnalyzeMetric(MetricName.Triangles, session, thresholds, f => f.Triangles, useMax: false),
				AnalyzeMetric(MetricName.Memory, session, thresholds, f => f.TotalMemoryMb, useMax: true),
			};

			return new SessionAnalysisReport(session, thresholds, results);
		}

		private static MetricAnalysisResult AnalyzeMetric(
			MetricName metric,
			ProfilerSession session,
			ThresholdConfiguration thresholds,
			Func<ProfilerFrameRecord, float?> selector,
			bool useMax)
		{
			int totalFrames = session.Frames.Count;
			var values = session.Frames
				.Select(selector)
				.Where(v => v.HasValue)
				.Select(v => v.Value)
				.ToList();

			int excludedInvalidFrames = totalFrames - values.Count;

			if (values.Count == 0)
			{
				return new MetricAnalysisResult(
					metric,
					isAnalyzable: false,
					driverValue: null,
					supportingStats: new Dictionary<string, float>(),
					excludedInvalidFrames: excludedInvalidFrames,
					status: null,
					suggestion: "No data available: every sample for this metric is invalid (-1).");
			}

			MetricThreshold threshold = thresholds.Get(metric);

			float mean = values.Average();
			float max = values.Max();
			float percentOverCritical = 100f * values.Count(v => v > threshold.CriticalValue) / values.Count;

			var supportingStats = new Dictionary<string, float>
			{
				{ "mean", mean },
				{ "max", max },
				{ "percentOverCritical", percentOverCritical },
			};

			float driverValue = useMax ? max : mean;
			SemaphoreStatus status = ComputeStatus(driverValue, threshold);
			string suggestion = status == SemaphoreStatus.Ok ? null : BuildSuggestion(metric, status);

			return new MetricAnalysisResult(
				metric,
				isAnalyzable: true,
				driverValue: driverValue,
				supportingStats: supportingStats,
				excludedInvalidFrames: excludedInvalidFrames,
				status: status,
				suggestion: suggestion);
		}

		private static SemaphoreStatus ComputeStatus(float driverValue, MetricThreshold threshold)
		{
			if (driverValue >= threshold.CriticalValue)
			{
				return SemaphoreStatus.Critical;
			}

			if (driverValue >= threshold.WarningValue)
			{
				return SemaphoreStatus.Warning;
			}

			return SemaphoreStatus.Ok;
		}

		private static string BuildSuggestion(MetricName metric, SemaphoreStatus status)
		{
			string severity = status == SemaphoreStatus.Critical ? "critical" : "approaching the critical threshold";

			switch (metric)
			{
				case MetricName.FrameTime:
					return $"Frame Time {severity}: check the other metrics (Draw Calls, GC, Triangles); Frame Time is a composite indicator of the other four.";
				case MetricName.DrawCalls:
					return $"Draw Calls {severity}: consider Material Property Batching (T2) or Draw Call Reduction via Static/Dynamic Batching and GPU Instancing (T5).";
				case MetricName.GcAlloc:
					return $"GC Allocations {severity}: consider GC Management techniques, in particular object pooling and reducing per-frame allocations (T4).";
				case MetricName.Triangles:
					return $"Triangle count {severity}: consider Geometry Culling techniques for objects that are not visible or out of view (T6).";
				case MetricName.Memory:
					return $"Total memory {severity}: consider optimizing Post-processing/Anti-Aliasing and texture quality (T1).";
				default:
					return null;
			}
		}
	}
}