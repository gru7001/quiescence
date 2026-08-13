using System.Collections.Generic;
using Godot;

namespace DelaunyFabric.Core;

public static class TopologyClone
{
	public static Topology Clone(Topology source)
	{
		var clone = new Topology();
		var vertexMap = new Dictionary<Vertex, Vertex>(source.Vertices.Count);
		var cornerMap = new Dictionary<Corner, Corner>(source.Corners.Count);

		foreach (var vertex in source.Vertices)
		{
			var copy = new Vertex
			{
				Xyz = vertex.Xyz,
				ContactNormal = vertex.ContactNormal,
				FromSubdivision = vertex.FromSubdivision,
			};
			clone.Vertices.Add(copy);
			vertexMap[vertex] = copy;
		}

		foreach (var corner in source.Corners)
		{
			var copy = new Corner
			{
				Uv = corner.Uv,
				Vertex = vertexMap[corner.Vertex],
			};
			clone.Corners.Add(copy);
			copy.Vertex.Corners.Add(copy);
			cornerMap[corner] = copy;
		}

		foreach (var corner in source.Corners)
		{
			var copy = cornerMap[corner];
			copy.Next = cornerMap[corner.Next];
			copy.Prev = cornerMap[corner.Prev];
			if (corner.Across != null)
				copy.Across = cornerMap[corner.Across];
		}

		return clone;
	}
}
