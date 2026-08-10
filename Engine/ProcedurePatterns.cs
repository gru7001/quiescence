using System;

public static class ProcedurePatterns
{
	/// <summary>
	/// Change-triggered procedure: if <paramref name="xKey"/> equals memo, no-op; else invoke <paramref name="onChange"/> and store memo.
	/// This overload keeps the scheduler context internal; call sites only supply the observed key and handler.
	/// </summary>
	public static ProcedureHandle ChangeTriggered<T>(
		Scheduler scheduler,
		Key<T> xKey,
		Action<Scheduler, T, T> onChange)
	{
		var memoKey = new Key<T>();
		return scheduler.AddProcedure(() =>
		{
			var x = scheduler.Ctx.Read(xKey);
			var memo = scheduler.Ctx.Read(memoKey);
			if (Equals(x, memo))
				return;
			onChange(scheduler, x, memo);
			scheduler.Ctx.Write(memoKey, x);
		});
	}
}

