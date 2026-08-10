using System.Collections.Generic;
using System.Text.Json;

public sealed record SaveFile(
	string GameRootId,
	IReadOnlyList<SaveRecord> Nodes);

public sealed record SaveRecord(
	string Tag,
	string Id,
	JsonElement Record);

public readonly record struct SaveNode(
	string Tag,
	object Record);

public readonly record struct SaveNode<TRecord>(
	string Tag,
	TRecord Record)
{
	public SaveNode Untyped() => new(Tag, Record!);
}

public readonly record struct NodeRef(string Id);

public static class NodeRefs
{
	/// <summary>
	/// Sentinel node id that represents a null reference in graph persistence.
	/// Real node ids are generated as "n0", "n1", ... so this will not collide.
	/// </summary>
	public const string NullId = "$null";
	public static readonly NodeRef Null = new(NullId);
	public static bool IsNull(NodeRef r) => r.Id == NullId;
}

public static class SaveJson
{
	public static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
	};
}

