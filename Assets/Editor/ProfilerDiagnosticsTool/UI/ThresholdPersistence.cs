using UnityEditor;
using UnityEngine;

namespace ProfilerDiagnosticsTool.UI
{
	// Persists ThresholdEditableSet across Editor sessions via EditorPrefs.
	public static class ThresholdPersistence
	{
		private const string PrefsKey = "ProfilerDiagnosticsTool.Thresholds";

		public static ThresholdEditableSet Load()
		{
			if (!EditorPrefs.HasKey(PrefsKey))
			{
				return ThresholdEditableSet.CreateDefault();
			}

			string json = EditorPrefs.GetString(PrefsKey);

			try
			{
				var loaded = JsonUtility.FromJson<ThresholdEditableSet>(json);
				return loaded ?? ThresholdEditableSet.CreateDefault();
			}
			catch
			{
				return ThresholdEditableSet.CreateDefault();
			}
		}

		public static void Save(ThresholdEditableSet set)
		{
			string json = JsonUtility.ToJson(set);
			EditorPrefs.SetString(PrefsKey, json);
		}
	}
}