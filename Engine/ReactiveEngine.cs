using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public sealed class Key<T> { }

public readonly record struct ProcedureHandle(int Index, int Generation);

/// <summary>
/// Key/value/version storage with weak keys: if nothing outside this table strongly references a key object,
/// the entry (value + version) can be collected — the table does not pin the key.
/// </summary>
public sealed class State
{
	private sealed class Entry
	{
		public object Value = null!;
		public int Version;
	}

	private readonly ConditionalWeakTable<object, Entry> _entries = new();

	public T ReadValue<T>(object key)
	{
		if (_entries.TryGetValue(key, out var e) && e.Value is T t)
			return t;
		return default!;
	}

	/// <summary>Returns current version, or 0 if no entry. Does not record a reactive read — use <see cref="ExecutionContext.ReadVersion"/> inside a trace when a procedure depends on version.</summary>
	public int GetVersion(object key) =>
		_entries.TryGetValue(key, out var e) ? e.Version : 0;

	/// <summary>Writes if <paramref name="value"/> differs from stored value; bumps version when written.</summary>
	public bool WriteValueIfChanged<T>(object key, T value)
	{
		if (!_entries.TryGetValue(key, out var e))
		{
			e = new Entry();
			_entries.Add(key, e);
		}

		var old = e.Value is T t ? t : default!;
		if (EqualityComparer<T>.Default.Equals(old, value))
			return false;
		e.Value = value!;
		e.Version++;
		return true;
	}
}

public sealed class ExecutionContext
{
	private sealed class TraceFrame
	{
		public readonly HashSet<object> Reads = new();
		public readonly HashSet<object> Writes = new();
	}

	public readonly State State;

	private readonly List<TraceFrame> _traceStack = new();

	public ExecutionContext(State state) => State = state;

	/// <summary>
	/// Runs <paramref name="body"/> with a fresh read/write trace. Nested calls merge their trace into the parent on exit.
	/// Returns this frame’s sets (also merged upward when applicable).
	/// </summary>
	public (HashSet<object> Reads, HashSet<object> Writes) Record(Action body)
	{
		var frame = new TraceFrame();
		_traceStack.Add(frame);
		try
		{
			body();
		}
		finally
		{
			_traceStack.RemoveAt(_traceStack.Count - 1);
			if (_traceStack.Count > 0)
			{
				var parent = _traceStack[^1];
				parent.Reads.UnionWith(frame.Reads);
				parent.Writes.UnionWith(frame.Writes);
			}
		}

		return (frame.Reads, frame.Writes);
	}

	public T Read<T>(Key<T> key)
	{
		if (_traceStack.Count > 0)
			_traceStack[^1].Reads.Add(key);
		return State.ReadValue<T>(key);
	}

	/// <summary>Records a read dependency on <paramref name="key"/> (same as <see cref="Read{T}"/>) and returns its current version.</summary>
	public int ReadVersion(object key)
	{
		if (_traceStack.Count > 0)
			_traceStack[^1].Reads.Add(key);
		return State.GetVersion(key);
	}

	/// <inheritdoc cref="ReadVersion(object)"/>
	public int ReadVersion<T>(Key<T> key) => ReadVersion(key);

	public void Write<T>(Key<T> key, T value)
	{
		if (!State.WriteValueIfChanged(key, value))
			return;
		if (_traceStack.Count == 0)
			throw new InvalidOperationException("Write outside ExecutionContext.Record — use Scheduler.RunScoped or another Record scope.");
		_traceStack[^1].Writes.Add(key);
	}
}

public sealed class ReactiveEngine
{
	private sealed class Proc
	{
		public Action Run = null!;
		public readonly HashSet<object> LastReadSet = new();
		public bool Awake;
		public int Generation;
		public bool Removed;
	}

	private readonly List<Proc> _procs = new();
	private readonly ExecutionContext _ctx;

	public ReactiveEngine(ExecutionContext ctx) => _ctx = ctx;

	public ProcedureHandle AddProcedure(Action run, bool awake = true)
	{
		var p = new Proc { Run = run ?? throw new ArgumentNullException(nameof(run)), Awake = awake };
		_procs.Add(p);
		return new ProcedureHandle(_procs.Count - 1, p.Generation);
	}

	public bool Wake(ProcedureHandle h)
	{
		if ((uint)h.Index >= (uint)_procs.Count)
			return false;
		var p = _procs[h.Index];
		if (p.Removed || p.Generation != h.Generation)
			return false;
		p.Awake = true;
		return true;
	}

	public bool Sleep(ProcedureHandle h)
	{
		if ((uint)h.Index >= (uint)_procs.Count)
			return false;
		var p = _procs[h.Index];
		if (p.Removed || p.Generation != h.Generation)
			return false;
		p.Awake = false;
		return true;
	}

	public bool Remove(ProcedureHandle h)
	{
		if ((uint)h.Index >= (uint)_procs.Count)
			return false;
		var p = _procs[h.Index];
		if (p.Removed || p.Generation != h.Generation)
			return false;
		p.Removed = true;
		p.Awake = false;
		p.Generation++;
		p.LastReadSet.Clear();
		p.Run = null!;
		return true;
	}

	public void PropagateWriteKeys(IEnumerable<object> writeKeys)
	{
		foreach (var k in writeKeys)
			foreach (var q in _procs)
				if (!q.Removed && q.LastReadSet.Contains(k))
					q.Awake = true;
	}

	public void RunTillQuiescence()
	{
		while (true)
		{
			// Index loop: Run() may AddProcedure; foreach would invalidate enumerator.
			for (var i = 0; i < _procs.Count; i++)
			{
				var p = _procs[i];
				if (p.Removed || !p.Awake)
					continue;
				var (reads, writes) = _ctx.Record(p.Run);

				p.LastReadSet.Clear();
				foreach (var x in reads)
					p.LastReadSet.Add(x);

				p.Awake = false;
				PropagateWriteKeys(writes);
			}

			var anyAwake = false;
			foreach (var q in _procs)
			{
				if (!q.Removed && q.Awake)
				{
					anyAwake = true;
					break;
				}
			}
			if (!anyAwake)
				break;
		}
	}
}
