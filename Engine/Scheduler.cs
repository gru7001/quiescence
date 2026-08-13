using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class Scheduler
{
	private readonly ReactiveEngine _reactive;
	public readonly ExecutionContext Ctx;
	public readonly Clock Clock;
	private readonly PriorityQueue<(Action Run, int Tie), (long Tau, int Tie)> _queue = new();
	private int _nextTie;

	public Scheduler(ExecutionContext ctx, Clock clock)
	{
		Ctx = ctx;
		Clock = clock ?? throw new ArgumentNullException(nameof(clock));
		_reactive = new ReactiveEngine(Ctx);
	}

	public long CurrentTime => Clock.Now;

	public bool HasPendingEvents => _queue.Count > 0;

	/// <summary>
	/// Real-time pacing parameter for <see cref="AdvanceRealTime(System.Threading.CancellationToken)"/>:
	/// simulation ticks per wall second.
	/// </summary>
	public double RealTimeSpeedTicksPerSecond { get; set; } = 1000.0;

	/// <summary>Wall-clock frame length used when crawling the sim clock in <see cref="AdvanceRealTime(double, System.Threading.CancellationToken)"/>.</summary>
	public double RealTimeFrameSeconds { get; set; } = 1.0 / 60.0;

	public ProcedureHandle AddProcedure(Action run, bool awake = true) =>
		_reactive.AddProcedure(run, awake);

	public bool Wake(ProcedureHandle h) => _reactive.Wake(h);

	public bool Sleep(ProcedureHandle h) => _reactive.Sleep(h);

	public bool RemoveProcedure(ProcedureHandle h) => _reactive.Remove(h);

	public void Schedule(Action ev, long tau)
	{
		var tie = _nextTie++;
		_queue.Enqueue((ev, tie), (tau, tie));
	}

	public void RunTillQuiescence() => _reactive.RunTillQuiescence();

	/// <summary>
	/// Run <paramref name="body"/> inside <see cref="ExecutionContext.Record"/>, propagate recorded writes, then run to quiescence.
	/// Empty write sets are a no-op through propagation and quiescence.
	/// </summary>
	public void RunScoped(Action body)
	{
		var (_, writes) = Ctx.Record(body);
		_reactive.PropagateWriteKeys(writes);
		_reactive.RunTillQuiescence();
	}

	public void Advance()
	{
		_reactive.RunTillQuiescence();

		if (!_queue.TryPeek(out _, out var next))
			return;
		var targetTau = next.Tau;
		{
			var (_, writes) = Ctx.Record(() => Clock.Set(targetTau));
			_reactive.PropagateWriteKeys(writes);
		}

		while (_queue.Count > 0)
		{
			_queue.TryPeek(out _, out var p);
			if (p.Tau != targetTau)
				break;
			var (run, _) = _queue.Dequeue();
			var (_, writes) = Ctx.Record(run);
			_reactive.PropagateWriteKeys(writes);
		}

		_reactive.RunTillQuiescence();
	}

	/// <summary>
	/// Like <see cref="Advance"/>, but crawls the sim clock in frame-sized steps (wall wait + <c>Now += dt</c>)
	/// until the next event time, then snaps via <see cref="Advance"/>.
	/// </summary>
	public async Task AdvanceRealTime(double speedTicksPerSecond, CancellationToken ct = default)
	{
		if (speedTicksPerSecond <= 0)
			throw new ArgumentOutOfRangeException(nameof(speedTicksPerSecond));

		_reactive.RunTillQuiescence();

		if (!_queue.TryPeek(out _, out var next))
			return;
		var targetTau = next.Tau;
		var frameSeconds = RealTimeFrameSeconds > 0 ? RealTimeFrameSeconds : 1.0 / 60.0;

		while (Clock.Now < targetTau)
		{
			ct.ThrowIfCancellationRequested();

			var ticksLeft = targetTau - Clock.Now;
			var wallLeft = ticksLeft / speedTicksPerSecond;
			var wait = Math.Min(frameSeconds, wallLeft);
			if (wait <= 0)
				break;

			var delayMs = (int)Math.Clamp(Math.Ceiling(wait * 1000.0), 1.0, int.MaxValue);
			await Task.Delay(delayMs, ct);

			var dtTicks = Math.Max(1L, (long)Math.Round(wait * speedTicksPerSecond));
			var stepped = Clock.Now + dtTicks;
			if (stepped >= targetTau)
				break;

			RunScoped(() => Clock.Set(stepped));
		}

		Advance();
	}

	public Task AdvanceRealTime(CancellationToken ct = default) =>
		AdvanceRealTime(RealTimeSpeedTicksPerSecond, ct);
}
