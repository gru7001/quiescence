using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

public static class BestTryActionStateCodec
{
	public sealed record ActionStateSave(string TypeId, object Data);

	private sealed class RefEq : IEqualityComparer<object>
	{
		public static readonly RefEq Instance = new();
		public new bool Equals(object x, object y) => ReferenceEquals(x, y);
		public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
	}

	public static ActionStateSave Encode(object state, SaveSession session)
	{
		if (session == null) throw new ArgumentNullException(nameof(session));

		var seen = new HashSet<object>(RefEq.Instance);
		var data = EncodeAny(state, session, seen);
		var typeId = state == null ? "null" : state.GetType().AssemblyQualifiedName ?? state.GetType().FullName ?? state.GetType().Name;
		return new ActionStateSave(TypeId: typeId, Data: data);
	}

	public static object Decode(ActionStateSave save, LoadSession session)
	{
		if (save == null) throw new ArgumentNullException(nameof(save));
		if (session == null) throw new ArgumentNullException(nameof(session));

		if (save.TypeId == "null")
			return null;

		var t = Type.GetType(save.TypeId, throwOnError: false);
		var data = DecodeAny(save.Data, session);
		if (t == null)
			return data;

		// If the data already matches the target type, use it.
		if (data != null && t.IsInstanceOfType(data))
			return data;

		// If the data is a dictionary, best-effort fill an instance.
		if (data is Dictionary<string, object> m)
			return FillObjectFromMap(t, m, session);

		return data;
	}

	private static object DecodeAny(object x, LoadSession session)
	{
		if (x == null)
			return null;

		// System.Text.Json deserializes `object`-typed fields as JsonElement.
		// Convert JsonElement into the primitive/dict/list shapes the codec expects.
		if (x is JsonElement je)
			x = JsonElementToObject(je);

		// Leaves are already in final form.
		switch (x)
		{
			case string:
			case bool:
			case int:
			case long:
			case float:
			case double:
			case decimal:
				return x;
		}

		if (x is RefSave r)
			return session.ResolveRef(r);

		// Pos no longer exists; tile/edge references are saved as NodeRef.

		if (x is NodeRef nr)
			return session.Ref(nr);

		if (x is List<object> list)
		{
			var outList = new List<object>(list.Count);
			for (var i = 0; i < list.Count; i++)
				outList.Add(DecodeAny(list[i], session));
			return outList;
		}

		if (x is Dictionary<string, object> dict)
		{
			// Special-case shapes coming back from System.Text.Json where `object`-typed
			// values were originally `NodeRef` / `RefSave` records.
			if (dict.Count == 1 &&
			    dict.TryGetValue("id", out var onlyId) &&
			    onlyId is string nodeId)
			{
				return session.Ref(new NodeRef(nodeId));
			}
			if ((dict.Count == 2 || dict.Count == 3) &&
			    dict.TryGetValue("kind", out var kindObj) &&
			    dict.TryGetValue("id", out var idObj) &&
			    kindObj is string kind &&
			    idObj is string id)
			{
				return session.ResolveRef(new RefSave(kind, id));
			}

			var outDict = new Dictionary<string, object>(dict.Count, StringComparer.Ordinal);
			foreach (var (k, v) in dict)
				outDict[k] = DecodeAny(v, session);
			return outDict;
		}

		return x;
	}

	private static object JsonElementToObject(JsonElement je)
	{
		switch (je.ValueKind)
		{
			case JsonValueKind.Null:
			case JsonValueKind.Undefined:
				return null;
			case JsonValueKind.String:
				return je.GetString();
			case JsonValueKind.True:
			case JsonValueKind.False:
				return je.GetBoolean();
			case JsonValueKind.Number:
				if (je.TryGetInt64(out var l))
					return l;
				if (je.TryGetDouble(out var d))
					return d;
				return je.GetRawText();
			case JsonValueKind.Array:
			{
				var list = new List<object>();
				foreach (var it in je.EnumerateArray())
					list.Add(JsonElementToObject(it));
				return list;
			}
			case JsonValueKind.Object:
			{
				var dict = new Dictionary<string, object>(StringComparer.Ordinal);
				foreach (var p in je.EnumerateObject())
					dict[p.Name] = JsonElementToObject(p.Value);
				return dict;
			}
			default:
				return je.GetRawText();
		}
	}

	private static object FillObjectFromMap(Type t, Dictionary<string, object> m, LoadSession session)
	{
		// Cycle marker.
		if (m.ContainsKey("$cycle"))
			return null;

		var obj = Activator.CreateInstance(t, nonPublic: true);
		if (obj == null)
			return null;

		foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
		{
			if (f.IsInitOnly || typeof(Delegate).IsAssignableFrom(f.FieldType))
				continue;
			if (!m.TryGetValue(f.Name, out var raw))
				continue;
			var v = DecodeAny(raw, session);
			v = CoerceValue(f.FieldType, v);
			if (v == null || f.FieldType.IsInstanceOfType(v))
				f.SetValue(obj, v);
		}

		foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
		{
			if (!p.CanWrite || p.GetIndexParameters().Length != 0)
				continue;
			if (typeof(Delegate).IsAssignableFrom(p.PropertyType))
				continue;
			if (!m.TryGetValue(p.Name, out var raw))
				continue;
			var v = DecodeAny(raw, session);
			v = CoerceValue(p.PropertyType, v);
			if (v == null || p.PropertyType.IsInstanceOfType(v))
			{
				try { p.SetValue(obj, v); } catch { }
			}
		}

		return obj;
	}

	private static object CoerceValue(Type targetType, object v)
	{
		if (v == null)
			return null;

		// Best-effort: coerce decoded List<object> into arrays / generic collection interfaces
		// like IReadOnlyCollection<T> so in-flight action state fields round-trip.
		if (v is List<object> list)
		{
			if (targetType.IsArray)
			{
				var elemType = targetType.GetElementType();
				if (elemType == null)
					return v;
				var arr = Array.CreateInstance(elemType, list.Count);
				for (var i = 0; i < list.Count; i++)
				{
					var it = list[i];
					if (it != null && !elemType.IsInstanceOfType(it))
						return v;
					arr.SetValue(it, i);
				}
				return arr;
			}

			if (targetType.IsGenericType)
			{
				var def = targetType.GetGenericTypeDefinition();
				if (def == typeof(IReadOnlyCollection<>) || def == typeof(IReadOnlyList<>) || def == typeof(IEnumerable<>))
				{
					var elemType = targetType.GetGenericArguments()[0];
					var outListType = typeof(List<>).MakeGenericType(elemType);
					var outList = (IList)Activator.CreateInstance(outListType);
					for (var i = 0; i < list.Count; i++)
					{
						var it = list[i];
						if (it != null && !elemType.IsInstanceOfType(it))
							return v;
						outList.Add(it);
					}
					return outList;
				}
			}
		}

		return v;
	}

	private static object EncodeAny(object x, SaveSession session, HashSet<object> seen)
	{
		if (x == null)
			return null;

		// Primitive-ish leaves.
		switch (x)
		{
			case string:
			case bool:
			case int:
			case long:
			case float:
			case double:
			case decimal:
				return x;
		}

		// Registry-backed statics.
		if (x is Item item)
			return new RefSave(Kind: "Item", Id: session.Context.Items.GetId(item));
		if (x is Perk perk)
			return new RefSave(Kind: "Perk", Id: session.Context.Perks.GetId(perk));
		if (x is Stat stat)
			return new RefSave(Kind: "Stat", Id: session.Context.Stats.GetId(stat));
		if (x is Resource res)
			return new RefSave(Kind: "Resource", Id: session.Context.Resources.GetId(res));

		// World references (enqueue for save).
		if (x is Body body)
			return session.Ref(body);
		if (x is World world)
			return session.Ref(world);

		// Tile / Edge graph leaves.
		if (x is Tile tile)
			return session.Ref(tile);
		if (x is Edge edge)
			return session.Ref(edge);

		// Enums -> stable string.
		var t = x.GetType();
		if (t.IsEnum)
			return x.ToString();

		// Avoid cycles on reference types.
		if (!t.IsValueType)
		{
			if (!seen.Add(x))
				return new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["$cycle"] = t.Name
				};
		}

		// IDictionary<string, ?>
		if (x is IDictionary dict)
		{
			var outDict = new Dictionary<string, object>(StringComparer.Ordinal);
			foreach (DictionaryEntry e in dict)
			{
				if (e.Key is not string k)
					continue;
				outDict[k] = EncodeAny(e.Value, session, seen);
			}
			return outDict;
		}

		// IEnumerable
		if (x is IEnumerable en && x is not string)
		{
			var list = new List<object>();
			foreach (var it in en)
				list.Add(EncodeAny(it, session, seen));
			return list;
		}

		// Best-effort reflection object -> map of fields/props.
		var m = new Dictionary<string, object>(StringComparer.Ordinal);
		foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public))
		{
			if (typeof(Delegate).IsAssignableFrom(f.FieldType))
				continue;
			m[f.Name] = EncodeAny(f.GetValue(x), session, seen);
		}
		foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
		{
			if (!p.CanRead || p.GetIndexParameters().Length != 0)
				continue;
			if (typeof(Delegate).IsAssignableFrom(p.PropertyType))
				continue;
			try
			{
				m[p.Name] = EncodeAny(p.GetValue(x), session, seen);
			}
			catch
			{
				// Best-try: skip unreadable property.
			}
		}
		return m;
	}
}

