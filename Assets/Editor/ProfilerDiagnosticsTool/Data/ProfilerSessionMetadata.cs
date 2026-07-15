using System;

namespace ProfilerDiagnosticsTool.Data
{

	// Metadata parsed from the '#' header block of the CSV file.

	public sealed class ProfilerSessionMetadata
	{
		public const string UnknownValue = "N/D";

		public string SessionName { get; set; } = UnknownValue;
		public DateTime? StartTimestamp { get; set; } = null;
		public string UnityVersion { get; set; } = UnknownValue;
		public string Platform { get; set; } = UnknownValue;
		public string SourceFilePath { get; set; } = UnknownValue;
	}
}