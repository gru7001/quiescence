using System.Collections.Generic;
using Godot;

namespace DelaunyFabric.Core;

/// <summary>Convert authored <see cref="GarmentPattern"/> into positioned markers for <see cref="TopologyBuilder"/>.</summary>
public static class GarmentPatternBuild
{
	public static List<PositionedPatternMarker> ToPositioned(GarmentPattern pattern, bool sew = true)
	{
		var markers = new List<PositionedPatternMarker>(pattern.Nodes.Count);
		for (int i = 0; i < pattern.Nodes.Count; i++)
		{
			markers.Add(new PositionedPatternMarker
			{
				Uv = pattern.Nodes[i].Uv,
				Xyz = pattern.NodeWorld(i),
			});
		}

		foreach (Variant v in pattern.Edges)
		{
			if (v.AsGodotObject() is not GarmentEdge e)
				continue;
			if (e.A < 0 || e.B < 0 || e.A >= markers.Count || e.B >= markers.Count)
				continue;
			Link(markers[e.A].Connected, markers[e.B]);
			Link(markers[e.B].Connected, markers[e.A]);
		}

		if (sew)
		{
			foreach (Variant v in pattern.Sews)
			{
				if (v.AsGodotObject() is not GarmentSew s)
					continue;
				if (s.A < 0 || s.B < 0 || s.A >= markers.Count || s.B >= markers.Count)
					continue;
				Link(markers[s.A].WeldedTo, markers[s.B]);
				Link(markers[s.B].WeldedTo, markers[s.A]);
			}
		}

		return markers;
	}

	public static Topology BuildTopology(GarmentPattern pattern, bool sew = true) =>
		TopologyBuilder.Build(ToPositioned(pattern, sew));

	static void Link(List<PositionedPatternMarker> list, PositionedPatternMarker other)
	{
		if (!list.Contains(other))
			list.Add(other);
	}
}
