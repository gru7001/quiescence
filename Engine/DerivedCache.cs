using System;
using System.Collections.Generic;

public sealed class DerivedCache<TOut>
{
	private readonly ExecutionContext _ctx;
	private readonly Func<TOut> _compute;
	private readonly HashSet<object> _readSet = new();
	private readonly Dictionary<object, int> _versionSnapshot = new();
	private TOut _cached;
	private bool _hasValue;

	public DerivedCache(ExecutionContext ctx, Func<TOut> compute)
	{
		_ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
		_compute = compute ?? throw new ArgumentNullException(nameof(compute));
	}

	public TOut Get()
	{
		if (_hasValue && InDate())
			return _cached;
		Recompute();
		return _cached;
	}

	private bool InDate()
	{
		var ok = true;
		_ctx.Record(() =>
		{
			foreach (var k in _readSet)
			{
				if (!_versionSnapshot.TryGetValue(k, out var lam) || _ctx.ReadVersion(k) != lam)
					ok = false;
			}
		});
		return ok;
	}

	private void Recompute()
	{
		var (reads, _) = _ctx.Record(() => { _cached = _compute(); });

		_readSet.Clear();
		foreach (var k in reads)
			_readSet.Add(k);

		_versionSnapshot.Clear();
		_ctx.Record(() =>
		{
			foreach (var k in _readSet)
				_versionSnapshot[k] = _ctx.ReadVersion(k);
		});

		_hasValue = true;
	}
}
