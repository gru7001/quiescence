using System;
using System.Collections.Generic;

public static class ResourcesPersistence
{
	public sealed record ResourcesSave(IReadOnlyDictionary<string, ResourceValue> ValuesByResourceId);

	public static ResourcesSave Encode(Resources resources, SaveSession session)
	{
		var d = resources.ReadAll();
		var byId = new Dictionary<string, ResourceValue>(StringComparer.Ordinal);
		foreach (var (res, v) in d)
			byId[session.Context.Resources.GetId(res)] = v;
		return new ResourcesSave(ValuesByResourceId: byId);
	}

	public static void Apply(Resources resources, ResourcesSave save, LoadSession session)
	{
		foreach (var (resId, value) in save.ValuesByResourceId)
		{
			var r = session.Context.Resources.Get(resId);
			resources.WriteMax(r, value.Max);
			resources.WriteCur(r, value.Cur);
		}
	}
}
