using System;
using System.Collections.Generic;

public static class StatsPersistence
{
	public sealed record StatsSave(IReadOnlyDictionary<string, float> ValuesByStatId);

	public static StatsSave Encode(Stats stats, SaveSession session)
	{
		var d = stats.ReadAll();
		var byId = new Dictionary<string, float>(StringComparer.Ordinal);
		foreach (var (stat, v) in d)
			byId[session.Context.Stats.GetId(stat)] = v;
		return new StatsSave(ValuesByStatId: byId);
	}

	public static void Apply(Stats stats, StatsSave save, LoadSession session)
	{
		foreach (var (statId, v) in save.ValuesByStatId)
			stats.Write(session.Context.Stats.Get(statId), v);
	}
}

