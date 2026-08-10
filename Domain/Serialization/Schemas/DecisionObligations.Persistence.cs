using System;
using System.Collections.Generic;

public static class DecisionObligationsPersistence
{
	public const string SaveSchemaId = "decisionObligations.v1";

	public sealed record CouplingSave(NodeRef Driver, NodeRef Vehicle);

	public sealed record ObligationsSave(IReadOnlyList<CouplingSave> Couplings);

	public static ObligationsSave Encode(DecisionObligations o, SaveSession session)
	{
		var set = o.Ctx.Read(o.RegisteredCouplings);
		if (set == null || set.Count == 0)
			return new ObligationsSave(Couplings: Array.Empty<CouplingSave>());

		var list = new List<CouplingSave>(set.Count);
		foreach (var p in set)
			list.Add(new CouplingSave(Driver: session.Ref(p.Driver), Vehicle: session.Ref(p.Vehicle)));

		return new ObligationsSave(Couplings: list);
	}

	public static void Apply(DecisionObligations o, ObligationsSave save, LoadSession load)
	{
		var built = new HashSet<DecisionObligation>();
		foreach (var c in save.Couplings)
		{
			var driver = (IDriver)load.Ref(c.Driver);
			var body = (Body)load.Ref(c.Vehicle);
			built.Add(new DecisionObligation(driver, body));
		}

		load.Ctx.Write(o.RegisteredCouplings, built);
	}
}
