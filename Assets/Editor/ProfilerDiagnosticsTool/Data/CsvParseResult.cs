using System.Collections.Generic;

namespace ProfilerDiagnosticsTool.Data
{

	// Result pattern: parsing never throws for expected format issues.

	public sealed class CsvParseResult
	{
		public bool Success { get; }
		public ProfilerSession Session { get; }
		public string ErrorMessage { get; }
		public IReadOnlyList<CsvParseIssue> Issues { get; }

		private CsvParseResult(bool success, ProfilerSession session, string errorMessage, IReadOnlyList<CsvParseIssue> issues)
		{
			Success = success;
			Session = session;
			ErrorMessage = errorMessage;
			Issues = issues;
		}

		public static CsvParseResult Ok(ProfilerSession session, IReadOnlyList<CsvParseIssue> issues)
		{
			return new CsvParseResult(true, session, null, issues);
		}

		public static CsvParseResult Fail(string errorMessage, IReadOnlyList<CsvParseIssue> issues = null)
		{
			return new CsvParseResult(false, null, errorMessage, issues ?? new List<CsvParseIssue>());
		}
	}
}