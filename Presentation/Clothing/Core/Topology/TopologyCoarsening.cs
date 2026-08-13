using System;
using System.Collections.Generic;
using Godot;

namespace DelaunyFabric.Core;

public sealed class CollapseFaceSets
{
	public List<IReadOnlyList<Corner>> Before { get; } = [];
	public List<IReadOnlyList<Corner>> After { get; } = [];
}

public static class TopologyCoarsening
{
	public static Topology Coarsen(Topology source, float maxIntegralError, int maxCollapses = int.MaxValue)
	{
		var errors = new Dictionary<Corner, float>();
		for (int i = 0; i < maxCollapses; i++)
		{
			var pending = new HashSet<Corner>(source.Corners);
			var bestStrip = null as IReadOnlyList<Corner>;
			var bestFaceSets = null as CollapseFaceSets;
			var bestError = float.MaxValue;
			
			while (pending.Count > 0)
			{
				var strip = TopologyWalk.WalkStrip(TopologyWalk.TakeAny(pending));
				foreach (var corner in strip)
					pending.Remove(corner);

				if (!IsStripFromSubdivision(strip))
				{
					continue;
				}

				var faceSets = BuildCollapseFaceSets(strip);
				var newError = CalculateIntegralError(faceSets);
				var error = errors.TryGetValue(strip[0], out var existingError) ? existingError + newError : newError;
				//var area = CalculateFacesArea(faceSets.After);
				if (error <= maxIntegralError && error < bestError)
				{
					//smallestArea = area;
					bestStrip = strip;
					bestFaceSets = faceSets;
					bestError = error;
				}
			}

			if (bestStrip == null)
				break;

			PropagateErrorToAdjacentStrips(errors, bestStrip, bestError);
			CollapseStrip(source, bestStrip, bestFaceSets);
		}
		return source;
	}

	static void PropagateErrorToAdjacentStrips(
		Dictionary<Corner, float> errors,
		IReadOnlyList<Corner> strip,
		float error)
	{
		var start = strip[0];
		var nextSide = start.Next.Across;
		if (nextSide != null)
			CacheStripError(errors, nextSide, error);
		if (start.Prev.Across != null)
			CacheStripError(errors, start.Prev.Across?.Next.Next, error);
	}

	static void CacheStripError(Dictionary<Corner, float> errors, Corner origin, float error)
	{
		if (!origin.Vertex.FromSubdivision)
			return;
		foreach (var corner in TopologyWalk.WalkStrip(origin))
			errors[corner] = error;
	}

	public static float ComputeIntegralError(IReadOnlyList<Corner> strip, CollapseFaceSets faceSets) =>
		CalculateIntegralError(faceSets);

	public static CollapseFaceSets BuildCollapseFaceSets(IReadOnlyList<Corner> strip)
	{
		var faceSets = new CollapseFaceSets();

		foreach (var corner in strip)
			AddStripEntryFaces(faceSets, corner);

		return faceSets;
	}

	static void CollapseStrip(
		Topology topology,
		IReadOnlyList<Corner> strip,
		CollapseFaceSets faceSets)
	{
		RewireAfterFaces(strip, faceSets);
		RemoveEdgeLoop(topology, strip);
	}

	static void RemoveEdgeLoop(Topology topology, IReadOnlyList<Corner> strip)
	{
		var vertices = new HashSet<Vertex>();
		foreach (var corner in strip)
		{
			vertices.Add(corner.Vertex);
			vertices.Add(corner.Prev.Vertex);
		}

		var cornersToRemove = new HashSet<Corner>();
		foreach (var vertex in vertices)
		{
			foreach (var corner in vertex.Corners)
				cornersToRemove.Add(corner);
		}

		foreach (var corner in cornersToRemove)
			topology.Corners.Remove(corner);

		foreach (var vertex in vertices)
			topology.Vertices.Remove(vertex);
	}

	static void AddStripEntryFaces(CollapseFaceSets faceSets, Corner corner)
	{
		var across = corner.Prev.Across;
		if (across == null)
			return;

		faceSets.Before.Add(TopologyWalk.WalkFace(corner));
		faceSets.Before.Add(TopologyWalk.WalkFace(across));

		faceSets.After.Add(
		[
			corner.Next,
			corner.Next.Next,
			across.Next,
			across.Next.Next,
		]);
	}

	static void RewireAfterFaces(IReadOnlyList<Corner> strip, CollapseFaceSets faceSets)
	{
		int faceIndex = 0;
		foreach (var corner in strip)
		{
			if (corner.Prev.Across == null)
				continue;

			var face = faceSets.After[faceIndex++];
			TopologyWalk.WireNext(face[1], face[2]);
			TopologyWalk.WireNext(face[3], face[0]);
		}
	}


	static bool IsStripFromSubdivision(IReadOnlyList<Corner> strip)
	{
		foreach (var corner in strip)
		{
			if (!corner.Vertex.FromSubdivision || !corner.Prev.Vertex.FromSubdivision)
				return false;
		}

		return true;
	}

	static float CalculateIntegralError(CollapseFaceSets faceSets)
	{
		var totalError = 0f;
		for (int i = 0; i < faceSets.After.Count; i++)
		{
			var before = faceSets.Before[i * 2];
			var after = faceSets.After[i];
			var volume = CalculateVolume(before, after);
			var error = volume;
			totalError += error;
		}
		return totalError;
	}

	static float CalculateVolume(IReadOnlyList<Corner> before, IReadOnlyList<Corner> after)
	{	
		(Vector3, Vector3)[] rails = [
				(after[0].Vertex.Xyz, after[1].Vertex.Xyz),
				(before[0].Vertex.Xyz, before[3].Vertex.Xyz),
				(after[2].Vertex.Xyz, after[3].Vertex.Xyz),
		];
		float length = 0.0f;
		for (int i = 0; i < rails.Length; i++)
		{
			var (a, b) = rails[i];
			length += (b - a).Length();
		}
		length /= rails.Length;
		float totalVolume = 0.0f;
		float step = 0.1f;
		for (float alpha = 0.0f; alpha < 1.0f; alpha += step)
		{
			var lerpedTriangle = new Vector3[rails.Length];
			for (int i = 0; i < rails.Length; i++)
			{
				var (a, b) = rails[i];
				var c = Lerp(a, b, alpha);
				lerpedTriangle[i] = c;
			}
			float area = CalculateTriangleArea(lerpedTriangle);
			totalVolume += area * length * step;
		}
		return totalVolume;
	}

	static Vector3 Lerp(Vector3 a, Vector3 b, float t) =>
		a + (b - a) * t;

	static float CalculateTriangleArea(IReadOnlyList<Vector3> triangle)
	{
		if (triangle.Count < 3)
			return 0f;

		var ab = triangle[1] - triangle[0];
		var ac = triangle[2] - triangle[0];
		return ab.Cross(ac).Length() * 0.5f;
	}
}
