using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public static class DecisionSystem
{
	private static HashSet<DecisionObligation> ReadObligations(Scheduler s, DecisionObligations o) =>
		s.Ctx.Read(o.U) ?? new HashSet<DecisionObligation>();

	public static void Step(Scheduler s, DecisionObligations o)
	{
		while (true)
		{
			var u = ReadObligations(s, o);
			if (u.Count > 0)
			{
				Ping(s, o, u, ct: default);
				return;
			}
			if (!s.HasPendingEvents)
				return;
			s.Advance();
		}
	}

	public static async Task StepRealTime(Scheduler s, DecisionObligations o, CancellationToken ct = default)
	{
		while (true)
		{
			ct.ThrowIfCancellationRequested();

			var u = ReadObligations(s, o);
			if (u.Count > 0)
			{
				Ping(s, o, u, ct);
				return;
			}
			if (!s.HasPendingEvents)
				return;

			await s.AdvanceRealTime(ct);
		}
	}

	private static void Ping(Scheduler s, DecisionObligations o, HashSet<DecisionObligation> u, CancellationToken ct)
	{
		foreach (var ob in u)
			Ping(s, o, ob, ct);
	}

	private static void Ping(Scheduler s, DecisionObligations o, DecisionObligation ob, CancellationToken ct)
	{
		var obl = ob;
		obl.Driver.OnDecisionNeeded(obl.Vehicle, (command, assignment) =>
		{
			var issued = false;
			s.RunScoped(() => issued = command.TryIssue(s, obl.Vehicle, assignment));
			if (!issued)
				return false;

			var u = ReadObligations(s, o);
			if (u.Contains(ob))
			{
				Ping(s, o, ob, ct);
				return true;
			}
			if (u.Count == 0)
				_ = StepRealTime(s, o, ct);
			return true;
		});
	}
}
