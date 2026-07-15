namespace ProfilerDiagnosticsTool.Data
{
	// A single sampled frame. Null fields mean the recorder was invalid
	// for that frame (-1 marker in the source CSV), not zero.

	public readonly struct ProfilerFrameRecord
	{
		public int FrameIndex { get; }
		public float? FrameTimeMs { get; }
		public int? DrawCalls { get; }
		public float? GcAllocKb { get; }
		public int? Triangles { get; }
		public float? TotalMemoryMb { get; }

		public ProfilerFrameRecord(
			int frameIndex,
			float? frameTimeMs,
			int? drawCalls,
			float? gcAllocKb,
			int? triangles,
			float? totalMemoryMb)
		{
			FrameIndex = frameIndex;
			FrameTimeMs = frameTimeMs;
			DrawCalls = drawCalls;
			GcAllocKb = gcAllocKb;
			Triangles = triangles;
			TotalMemoryMb = totalMemoryMb;
		}
	}
}