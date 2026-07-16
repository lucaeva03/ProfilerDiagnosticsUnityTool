using System;
using System.Collections.Generic;
using System.Linq;
using ProfilerDiagnosticsTool.Data;

namespace ProfilerDiagnosticsTool.Analysis
{

	// Compares a ProfilerSession against a ThresholdConfiguration and
	// produces a per-metric verdict. Driver statistic is mean for
	// FrameTime/DrawCalls/Triangles, max for GcAlloc/Memory (see
	// Fase 3.1 design doc for the rationale).

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
					suggestion: "Nessun dato disponibile: tutti i campioni per questa metrica sono invalidi (-1).");
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
			string severity = status == SemaphoreStatus.Critical ? "critico" : "in avvicinamento alla soglia critica";

			switch (metric)
			{
				case MetricName.FrameTime:
					return $"Frame Time {severity}: verificare le altre metriche (Draw Calls, GC, Triangoli), il Frame Time è un indicatore composito delle altre quattro.";
				case MetricName.DrawCalls:
					return $"Draw Calls {severity}: valutare Material Property Batching (T2) o Draw Call Reduction tramite Static/Dynamic Batching e GPU Instancing (T5).";
				case MetricName.GcAlloc:
					return $"GC Allocations {severity}: valutare tecniche di GC Management, in particolare object pooling e riduzione delle allocazioni per frame (T4).";
				case MetricName.Triangles:
					return $"Numero di triangoli {severity}: valutare tecniche di Geometry Culling per gli oggetti non visibili o fuori scena (T6).";
				case MetricName.Memory:
					return $"Memoria totale {severity}: valutare ottimizzazione di Post-processing/Anti-Aliasing e qualità texture (T1).";
				default:
					return null;
			}
		}
	}
}