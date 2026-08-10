#nullable enable
using System;
using Godot;

public static class GameFile
{
	public static SaveFile Save(Game game)
	{
		var ss = new SaveSession();
		var root = ss.Ref(game);
		return ss.Finish(root.Id);
	}

	/// <summary>
	/// In-memory deep copy of <paramref name="game"/> into a fresh <see cref="ExecutionContext"/> (same pipeline as <see cref="Load"/>).
	/// Use for AI rollouts / planning: the returned <see cref="Game"/> and <see cref="Body"/> instances are new; the live session is untouched.
	/// Persisted <see cref="DecisionObligations"/> couplings are recreated; Godot-backed drivers use <paramref name="seatRoot"/>
	/// (a detached <see cref="Node"/> is fine for headless simulation—free it when the fork is discarded).
	/// </summary>
	public static (Game Game, Scheduler Scheduler) Fork(Game game, Node seatRoot)
	{
		var (g, sched, _) = ForkCore(game, mirrorFor: null, seatRoot);
		return (g, sched);
	}

	/// <summary>
	/// Like <see cref="Fork(Game, Node)"/>, but also resolves <paramref name="mirrorFor"/> to the corresponding <see cref="Body"/>
	/// in the fork (same save-node identity). <paramref name="mirrorFor"/> must be reachable from <paramref name="game"/> (e.g. in <see cref="Game.Bodies"/>).
	/// </summary>
	public static (Game Game, Scheduler Scheduler, Body MirrorSelf) Fork(Game game, Body mirrorFor, Node seatRoot)
	{
		var (g, sched, mirror) = ForkCore(game, mirrorFor, seatRoot);
		return (g, sched, mirror!);
	}

	static (Game Game, Scheduler Scheduler, Body? MirrorSelf) ForkCore(Game game, Body? mirrorFor, Node seatRoot)
	{
		var ss = new SaveSession();
		var root = ss.Ref(game);
		var file = ss.Finish(root.Id);

		NodeRef mirrorRef = NodeRefs.Null;
		if (mirrorFor != null)
		{
			mirrorRef = ss.LookupRefOrNull(mirrorFor);
			if (NodeRefs.IsNull(mirrorRef))
				throw new InvalidOperationException(
					"The given body was not part of the saved game graph; use a Body from this Game (e.g. Game.Bodies).");
		}

		var ls = new LoadSession(SaveContext.Default, seatRoot);
		ls.Index(file);
		var g = (Game)ls.Ref(new NodeRef(file.GameRootId));
		var sched = new Scheduler(ls.Ctx, g.Clock);
		sched.RunScoped(ls.Drain);
		Game.SetupRuntime(g, sched);
		g.Obligations.Setup(sched);

		Body? mirror = null;
		if (!NodeRefs.IsNull(mirrorRef))
			mirror = (Body)ls.Ref(mirrorRef);

		return (g, sched, mirror);
	}

	/// <summary>Restores a game from disk and registers obligation/sim-observation procedures on the returned scheduler (not serialized).</summary>
	public static (Game Game, Scheduler Scheduler) Load(SaveFile file, Node seatRoot)
	{
		var ls = new LoadSession(SaveContext.Default, seatRoot);
		ls.Index(file);
		var g = (Game)ls.Ref(new NodeRef(file.GameRootId));
		var sched = new Scheduler(ls.Ctx, g.Clock);
		sched.RunScoped(ls.Drain);
		Game.SetupRuntime(g, sched);
		g.Obligations.Setup(sched);
		return (g, sched);
	}
}

