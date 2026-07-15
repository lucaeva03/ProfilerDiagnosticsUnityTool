namespace ProfilerDiagnosticsTool.Data
{
	public enum CsvParseIssueSeverity
	{
		Info,
		Warning
	}

	// A non-blocking event found while parsing (skipped row, unexpected value).

	public readonly struct CsvParseIssue
	{
		public CsvParseIssueSeverity Severity { get; }
		public int? LineNumber { get; }
		public string Message { get; }

		public CsvParseIssue(CsvParseIssueSeverity severity, string message, int? lineNumber = null)
		{
			Severity = severity;
			Message = message;
			LineNumber = lineNumber;
		}

		public override string ToString()
		{
			return LineNumber.HasValue
				? $"[{Severity}] Line {LineNumber.Value}: {Message}"
				: $"[{Severity}] {Message}";
		}
	}
}