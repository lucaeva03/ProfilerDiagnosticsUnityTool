using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ProfilerDiagnosticsTool.Data
{

	// Parses ProfilerDataRecorder CSV output into a typed ProfilerSession.
	// -1 in a field means the recorder was invalid that frame (-> null).
	// Any other value, even if anomalous, is kept as-is.

	public sealed class CsvSessionParser : IDataProvider
	{
		private static readonly string[] ExpectedColumns =
		{
			"Frame", "FrameTime_ms", "DrawCalls", "GCAlloc_KB", "Triangles", "TotalMemory_MB"
		};

		private const int ExpectedColumnCount = 6;
		private const float InvalidSentinel = -1f;

		public CsvParseResult Parse(string filePath)
		{
			var issues = new List<CsvParseIssue>();

			if (string.IsNullOrWhiteSpace(filePath))
			{
				return CsvParseResult.Fail("File path not specified.");
			}

			if (!File.Exists(filePath))
			{
				return CsvParseResult.Fail($"File not found: {filePath}");
			}

			string[] lines;
			try
			{
				lines = File.ReadAllLines(filePath);
			}
			catch (IOException ex)
			{
				return CsvParseResult.Fail($"Error reading file: {ex.Message}");
			}

			if (lines.Length == 0)
			{
				return CsvParseResult.Fail("File is empty.");
			}

			var metadata = new ProfilerSessionMetadata { SourceFilePath = filePath };
			int headerLineIndex = -1;

			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i];

				if (string.IsNullOrWhiteSpace(line))
				{
					continue;
				}

				if (line.TrimStart().StartsWith("#"))
				{
					ParseMetadataLine(line, metadata);
					continue;
				}

				headerLineIndex = i;
				break;
			}

			if (headerLineIndex == -1)
			{
				return CsvParseResult.Fail("No header row found.", issues);
			}

			string[] headerFields = lines[headerLineIndex].Split(';');
			if (!HeaderMatchesExpected(headerFields, out string headerMismatchDetail))
			{
				return CsvParseResult.Fail($"Header does not match expected format. {headerMismatchDetail}", issues);
			}

			var frames = new List<ProfilerFrameRecord>();

			for (int i = headerLineIndex + 1; i < lines.Length; i++)
			{
				string line = lines[i];
				int humanLineNumber = i + 1;

				if (string.IsNullOrWhiteSpace(line))
				{
					continue;
				}

				string[] fields = line.Split(';');

				if (fields.Length != ExpectedColumnCount)
				{
					issues.Add(new CsvParseIssue(
						CsvParseIssueSeverity.Warning,
						$"Expected {ExpectedColumnCount} columns, found {fields.Length}. Row skipped.",
						humanLineNumber));
					continue;
				}

				if (!int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int frameIndex))
				{
					issues.Add(new CsvParseIssue(
						CsvParseIssueSeverity.Warning,
						$"Non-numeric frame index ('{fields[0]}'). Row skipped.",
						humanLineNumber));
					continue;
				}

				float? frameTimeMs = ParseNullableFloat(fields[1], "FrameTime_ms", humanLineNumber, issues);
				int? drawCalls = ParseNullableInt(fields[2], "DrawCalls", humanLineNumber, issues);
				float? gcAllocKb = ParseNullableFloat(fields[3], "GCAlloc_KB", humanLineNumber, issues);
				int? triangles = ParseNullableInt(fields[4], "Triangles", humanLineNumber, issues);
				float? totalMemoryMb = ParseNullableFloat(fields[5], "TotalMemory_MB", humanLineNumber, issues);

				frames.Add(new ProfilerFrameRecord(
					frameIndex, frameTimeMs, drawCalls, gcAllocKb, triangles, totalMemoryMb));
			}

			if (frames.Count == 0)
			{
				return CsvParseResult.Fail("No valid data rows found after the header.", issues);
			}

			var session = new ProfilerSession(metadata, frames);
			return CsvParseResult.Ok(session, issues);
		}

		private static bool HeaderMatchesExpected(string[] headerFields, out string detail)
		{
			if (headerFields.Length != ExpectedColumnCount)
			{
				detail = $"Expected {ExpectedColumnCount} columns, found {headerFields.Length}.";
				return false;
			}

			for (int i = 0; i < ExpectedColumnCount; i++)
			{
				if (!string.Equals(headerFields[i].Trim(), ExpectedColumns[i], StringComparison.OrdinalIgnoreCase))
				{
					detail = $"Column {i + 1} expected '{ExpectedColumns[i]}', found '{headerFields[i].Trim()}'.";
					return false;
				}
			}

			detail = null;
			return true;
		}

		private static void ParseMetadataLine(string line, ProfilerSessionMetadata metadata)
		{
			string content = line.TrimStart('#', ' ');

			if (content.StartsWith("ProfilerDataRecorder", StringComparison.OrdinalIgnoreCase))
			{
				int sessionIndex = content.IndexOf("Session:", StringComparison.OrdinalIgnoreCase);
				if (sessionIndex >= 0)
				{
					metadata.SessionName = content.Substring(sessionIndex + "Session:".Length).Trim();
				}
				return;
			}

			if (content.StartsWith("Start timestamp:", StringComparison.OrdinalIgnoreCase))
			{
				string raw = content.Substring("Start timestamp:".Length).Trim();
				if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
				{
					metadata.StartTimestamp = parsed;
				}
				return;
			}

			if (content.StartsWith("Unity version:", StringComparison.OrdinalIgnoreCase))
			{
				metadata.UnityVersion = content.Substring("Unity version:".Length).Trim();
				return;
			}

			if (content.StartsWith("Platform:", StringComparison.OrdinalIgnoreCase))
			{
				metadata.Platform = content.Substring("Platform:".Length).Trim();
			}
		}

		private static float? ParseNullableFloat(string raw, string columnName, int lineNumber, List<CsvParseIssue> issues)
		{
			if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
			{
				issues.Add(new CsvParseIssue(
					CsvParseIssueSeverity.Warning,
					$"Non-numeric value in '{columnName}' ('{raw}'). Treated as unavailable.",
					lineNumber));
				return null;
			}

			if (ApproximatelyEqual(value, InvalidSentinel))
			{
				return null;
			}

			if (value < 0f)
			{
				issues.Add(new CsvParseIssue(
					CsvParseIssueSeverity.Info,
					$"Unexpected negative value in '{columnName}' ('{raw}'). Kept in dataset.",
					lineNumber));
			}

			return value;
		}

		private static int? ParseNullableInt(string raw, string columnName, int lineNumber, List<CsvParseIssue> issues)
		{
			if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
			{
				issues.Add(new CsvParseIssue(
					CsvParseIssueSeverity.Warning,
					$"Non-numeric value in '{columnName}' ('{raw}'). Treated as unavailable.",
					lineNumber));
				return null;
			}

			if (value == (int)InvalidSentinel)
			{
				return null;
			}

			if (value < 0)
			{
				issues.Add(new CsvParseIssue(
					CsvParseIssueSeverity.Info,
					$"Unexpected negative value in '{columnName}' ('{raw}'). Kept in dataset.",
					lineNumber));
			}

			return value;
		}

		private static bool ApproximatelyEqual(float a, float b)
		{
			return Math.Abs(a - b) < 0.0001f;
		}
	}
}