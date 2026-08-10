using System;

/// <summary>
/// Mutable simulation clock (ticks). Lives outside the scheduler and is attached at construction.
/// </summary>
public sealed class Clock : ISaveable<ClockPersistence.ClockSave>
{
	private readonly ExecutionContext _ctx;
	public readonly Key<long> NowKey = new();

	public Clock(ExecutionContext ctx)
	{
		_ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
	}

	public long Now => _ctx.Read(NowKey);

	public void Set(long now) => _ctx.Write(NowKey, now);

	public SaveNode<ClockPersistence.ClockSave> SaveTo(SaveSession session) =>
		new(ClockPersistence.SaveSchemaId, ClockPersistence.Encode(this));

	SaveNode ISaveable.SaveTo(SaveSession session) => SaveTo(session).Untyped();
}

