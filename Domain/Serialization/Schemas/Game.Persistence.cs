using System.Collections.Generic;

public static class GamePersistence
{
	public const string SaveSchemaId = "game.v1";

	public sealed record GameSave(
		NodeRef Clock,
		NodeRef Occupancy,
		NodeRef World,
		NodeRef Obligations,
		IReadOnlyList<NodeRef> Bodies);

	public static GameSave Encode(Game game, SaveSession session)
	{
		var clock = session.Ref(game.Clock);
		var occ = session.Ref(game.Occupancy);
		var world = session.Ref(game.World);
		var obl = session.Ref(game.Obligations);

		var bodies = new List<NodeRef>(game.Bodies.Count);
		foreach (var b in game.Bodies)
			bodies.Add(session.Ref(b));

		return new GameSave(
			Clock: clock,
			Occupancy: occ,
			World: world,
			Obligations: obl,
			Bodies: bodies);
	}

	public static void Apply(Game game, GameSave save, LoadSession session)
	{
		_ = (Clock)session.Ref(save.Clock);
		_ = (Occupancy)session.Ref(save.Occupancy);
		_ = (World)session.Ref(save.World);
		var obl = (DecisionObligations)session.Ref(save.Obligations);

		var bodies = new List<Body>(save.Bodies.Count);
		foreach (var r in save.Bodies)
			bodies.Add((Body)session.Ref(r));

		game.SetObligations(obl);
		game.ReplaceRoots(bodies);
	}
}

