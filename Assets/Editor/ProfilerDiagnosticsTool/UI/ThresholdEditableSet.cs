using System.Collections.Generic;
using ProfilerDiagnosticsTool.Analysis;

namespace ProfilerDiagnosticsTool.UI
{
	// Mutable, JSON-serializable copy of the 5 threshold pairs, used by the
	// GUI. ThresholdConfiguration (Analysis layer) stays immutable.

	[System.Serializable]
	public class ThresholdEditableSet
	{
		public float frameTimeWarning, frameTimeCritical;
		public float drawCallsWarning, drawCallsCritical;
		public float gcAllocWarning, gcAllocCritical;
		public float trianglesWarning, trianglesCritical;
		public float memoryWarning, memoryCritical;

		public static ThresholdEditableSet CreateDefault()
		{
			return FromConfiguration(ThresholdConfiguration.CreateDefault());
		}

		public static ThresholdEditableSet FromConfiguration(ThresholdConfiguration config)
		{
			var set = new ThresholdEditableSet();

			var frameTime = config.Get(MetricName.FrameTime);
			set.frameTimeWarning = frameTime.WarningValue;
			set.frameTimeCritical = frameTime.CriticalValue;

			var drawCalls = config.Get(MetricName.DrawCalls);
			set.drawCallsWarning = drawCalls.WarningValue;
			set.drawCallsCritical = drawCalls.CriticalValue;

			var gcAlloc = config.Get(MetricName.GcAlloc);
			set.gcAllocWarning = gcAlloc.WarningValue;
			set.gcAllocCritical = gcAlloc.CriticalValue;

			var triangles = config.Get(MetricName.Triangles);
			set.trianglesWarning = triangles.WarningValue;
			set.trianglesCritical = triangles.CriticalValue;

			var memory = config.Get(MetricName.Memory);
			set.memoryWarning = memory.WarningValue;
			set.memoryCritical = memory.CriticalValue;

			return set;
		}

		public ThresholdConfiguration ToConfiguration()
		{
			var thresholds = new Dictionary<MetricName, MetricThreshold>
			{
				{ MetricName.FrameTime, new MetricThreshold(MetricName.FrameTime, frameTimeWarning, frameTimeCritical) },
				{ MetricName.DrawCalls, new MetricThreshold(MetricName.DrawCalls, drawCallsWarning, drawCallsCritical) },
				{ MetricName.GcAlloc, new MetricThreshold(MetricName.GcAlloc, gcAllocWarning, gcAllocCritical) },
				{ MetricName.Triangles, new MetricThreshold(MetricName.Triangles, trianglesWarning, trianglesCritical) },
				{ MetricName.Memory, new MetricThreshold(MetricName.Memory, memoryWarning, memoryCritical) },
			};

			return new ThresholdConfiguration(thresholds);
		}
	}
}