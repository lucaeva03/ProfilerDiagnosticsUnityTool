using System.Collections.Generic;

namespace ProfilerDiagnosticsTool.Data
{
	// In-memory representation of a full profiling session.

	public sealed class ProfilerSession
	{
		public ProfilerSessionMetadata Metadata { get; }
		public IReadOnlyList<ProfilerFrameRecord> Frames { get; }

		public ProfilerSession(ProfilerSessionMetadata metadata, IReadOnlyList<ProfilerFrameRecord> frames)
		{
			Metadata = metadata;
			Frames = frames;
		}
	}
}