namespace ProfilerDiagnosticsTool.Data
{
	public interface IDataProvider
	{
		CsvParseResult Parse(string filePath);
	}
}