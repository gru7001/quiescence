using Godot;
using System;

/// <summary>Serializable clip: ordered keyframes of IkTargetSet.</summary>
[GlobalClass]
public partial class IkAnimation : Godot.Resource
{
	[Export]
	public Godot.Collections.Array<IkAnimKey> Keys { get; set; } = [];

	/// <summary>Clip length in seconds (independent of last key time).</summary>
	[Export]
	public float Duration { get; set; } = 1f;

	/// <summary>
	/// Loop playback; Catmull–Rom pose neighbors wrap; closing segment last→first
	/// spans from last key time to Duration.
	/// </summary>
	[Export]
	public bool Cyclic { get; set; }

	public void AddKey(float time, IkTargetSet targets)
	{
		Keys.Add(new IkAnimKey
		{
			Time = time,
			TargetSet = IkTargetSet.Clone(targets),
		});
	}

	public void RemoveAt(int index)
	{
		Keys.RemoveAt(index);
	}

	public void SortByTime()
	{
		var list = new System.Collections.Generic.List<IkAnimKey>();
		foreach (IkAnimKey k in Keys)
		{
			if (k != null)
				list.Add(k);
		}
		list.Sort((a, b) => a.Time.CompareTo(b.Time));
		Keys = new Godot.Collections.Array<IkAnimKey>();
		foreach (var k in list)
			Keys.Add(k);
	}

	public int IndexOf(IkAnimKey key)
	{
		for (int i = 0; i < Keys.Count; i++)
		{
			if (Keys[i] == key)
				return i;
		}
		return -1;
	}

	public float LastKeyTime()
	{
		float t = 0f;
		foreach (IkAnimKey k in Keys)
		{
			if (k != null && k.Time > t)
				t = k.Time;
		}
		return t;
	}

	/// <summary>Duration used for playback; falls back to last key time if unset.</summary>
	public float EffectiveDuration()
	{
		if (Duration > 1e-8f)
			return Duration;
		float last = LastKeyTime();
		return last > 1e-8f ? last : 1f;
	}

	public static void Save(IkAnimation data, string path)
	{
		var toSave = Clone(data);
		toSave.TakeOverPath(path);
		Error err = ResourceSaver.Save(toSave, path);
		if (err != Error.Ok)
			throw new InvalidOperationException($"IkAnimation.Save failed ({err}): {path}");
	}

	public static IkAnimation Load(string path)
	{
		var loaded = ResourceLoader.Load<IkAnimation>(
			path, cacheMode: ResourceLoader.CacheMode.Ignore);
		if (loaded == null)
			throw new InvalidOperationException($"IkAnimation.Load failed: {path}");
		return Clone(loaded);
	}

	public static IkAnimation Clone(IkAnimation src)
	{
		var copy = new IkAnimation();
		if (src == null) return copy;

		copy.Duration = src.Duration > 1e-8f ? src.Duration : 1f;
		copy.Cyclic = src.Cyclic;

		if (src.Keys == null) return copy;
		foreach (Variant v in src.Keys)
		{
			if (v.AsGodotObject() is IkAnimKey k)
			{
				copy.Keys.Add(new IkAnimKey
				{
					Time = k.Time,
					TargetSet = k.TargetSet != null ? IkTargetSet.Clone(k.TargetSet) : new IkTargetSet(),
				});
			}
			else if (v.AsGodotObject() is Godot.Resource r)
			{
				float time = r.Get("Time").AsSingle();
				var targets = r.Get("TargetSet").AsGodotObject() as IkTargetSet
					?? r.Get("Solver").AsGodotObject() as IkTargetSet;
				copy.Keys.Add(new IkAnimKey
				{
					Time = time,
					TargetSet = targets != null ? IkTargetSet.Clone(targets) : new IkTargetSet(),
				});
			}
		}

		// Old assets / keys past length: grow duration to cover last key.
		float last = copy.LastKeyTime();
		if (last > copy.Duration)
			copy.Duration = last;

		return copy;
	}
}
