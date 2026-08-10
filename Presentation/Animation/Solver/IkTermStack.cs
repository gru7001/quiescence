using System.Collections;
using System.Collections.Generic;

/// <summary>Fixed list of weighted IK terms for a character (not per-key targets).</summary>
public class IkTermStack : IEnumerable<(IIkTerm Term, float Weight)>
{
	readonly List<(IIkTerm Term, float Weight)> terms = [];

	public IkTermStack Add(IIkTerm term, float weight = 1f)
	{
		terms.Add((term, weight));
		return this;
	}

	public IEnumerator<(IIkTerm Term, float Weight)> GetEnumerator() => terms.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
