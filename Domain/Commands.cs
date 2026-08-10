using System;
using System.Collections.Generic;

public static class Commands
{
	public static readonly CommandDefinition Wait = global::Wait.Command;

	public static readonly CommandDefinition Move = global::Move.Command;

	public static readonly CommandDefinition Consume = global::Consume.Command;

	private static readonly CommandDefinition[] Catalog =
	{
		Wait,
		Move,
		Punch.Command,
		Fireball.Command,
		Consume,
		global::Transfer.DepositCommand,
		global::Transfer.WithdrawCommand,
	};

	public static IReadOnlyList<CommandDefinition> All => Catalog;

	public static CommandDefinition[] AvailableCommands(Body body)
	{
		var list = new List<CommandDefinition>();
		foreach (var cmd in Catalog)
		{
			if (cmd.IsAvailable(body))
				list.Add(cmd);
		}
		return list.ToArray();
	}

	public static bool DecisionPoint(Body body)
	{
		foreach (var cmd in Catalog)
		{
			if (cmd.IsAvailable(body))
				return true;
		}
		return false;
	}

	public static CommandDefinition[] AffectedByPerk(Perk perk)
	{
		var list = new List<CommandDefinition>();
		foreach (var cmd in Catalog)
		{
			foreach (var atom in cmd.InspectAtoms())
			{
				if (atom is Perk p && ReferenceEquals(p, perk))
				{
					list.Add(cmd);
					break;
				}
			}
		}
		return list.ToArray();
	}
}

/// <summary>
/// Command schema: <see cref="IsAvailable"/>, <see cref="TryIssue"/>, and <see cref="Variables"/> bound into an
/// <see cref="Assignment"/> (UI may use <see cref="TryBindVariables(object, out Assignment)"/> like <see cref="FooDriver"/>).
/// </summary>
public abstract class CommandDefinition
{
	public virtual string Name => GetType().Name;

	/// <summary>Ordered command parameters; each entry must be a <see cref="Var"/>.</summary>
	public virtual IReadOnlyList<Var> Variables => Array.Empty<Var>();

	/// <summary>
	/// Constraint formula over <see cref="PredicateCall"/> atoms (see <see cref="IPredicate"/>); checked by
	/// <see cref="Formula.Accepts(Body, Assignment)"/> and <see cref="Formula.Extendable(Body, Assignment)"/>.
	/// </summary>
	public virtual Formula Constraint => Formula.True;

	private static readonly Assignment EmptyPartialAssignment = new();

	/// <summary>
	/// Optional override for progressive binding: a partial <see cref="Assignment"/> is extendable when some completion
	/// satisfies <see cref="Constraint"/> (default: <see cref="Formula.Extendable(Body, Assignment)"/>).
	/// </summary>
	public virtual bool IsExtendable(Body body, Assignment partialAssignment) =>
		Constraint.Extendable(body, partialAssignment);

	public virtual bool IsAvailable(Body body) =>
		Constraint.Extendable(body, EmptyPartialAssignment);

	/// <summary>
	/// Effectful issuance. Assumes the caller has already established admissibility
	/// (<see cref="Constraint"/> / <see cref="TryIssue"/>); does not soft-fail.
	/// </summary>
	public abstract void Issue(Scheduler scheduler, Body body, Assignment assignment);

	/// <summary>
	/// Safe issuance: checks admissibility from <see cref="Constraint"/> and then calls <see cref="Issue"/>.
	/// </summary>
	public bool TryIssue(Scheduler scheduler, Body body, Assignment assignment)
	{
		if (!Constraint.Accepts(body, assignment))
			return false;
		Issue(scheduler, body, assignment);
		return true;
	}

	/// <summary>
	/// Binds <see cref="Variables"/> from a single value or an <c>object[]</c> prefix in declaration order.
	/// Does not re-validate CLR types against the constraint; callers use <see cref="TryIssue"/> (or <see cref="Formula.Accepts(Body, Assignment)"/>) for that.
	/// </summary>
	public bool TryBindVariables(object argument, out Assignment assignment)
	{
		assignment = new Assignment();
		if (Variables.Count == 0)
			return true;

		if (Variables.Count == 1)
		{
			var v0 = Variables[0];
			if (argument == null)
				return false;
			var next = v0.BindOrCheck(assignment, argument);
			if (next == null)
				return false;
			assignment = next;
			return true;
		}

		if (argument is not object[] arr || arr.Length < Variables.Count)
			return false;

		for (var i = 0; i < Variables.Count; i++)
		{
			var v = Variables[i];
			var o = arr[i];
			if (o == null)
				return false;
			var next = v.BindOrCheck(assignment, o);
			if (next == null)
				return false;
			assignment = next;
		}

		return true;
	}

	public virtual IReadOnlyList<object> InspectAtoms()
	{
		var atoms = new List<object>();
		Constraint.CollectAtoms(atoms);
		return atoms;
	}
}

/// <summary>Command behavior from delegates; use for catalog entries you iterate with <see cref="Commands.All"/>.</summary>
public sealed class Command : CommandDefinition
{
	private readonly string _name;
	private readonly Var[] _variables;
	private readonly Formula _constraint;
	private readonly Action<Scheduler, Body, Assignment> _issue;
	private readonly Func<Body, Assignment, bool> _isExtendable;

	public Command(
		string name,
		Var[] variables,
		Action<Scheduler, Body, Assignment> issue,
		Formula constraint = null,
		Func<Body, Assignment, bool> isExtendable = null)
	{
		_name = name ?? throw new ArgumentNullException(nameof(name));
		_variables = variables ?? throw new ArgumentNullException(nameof(variables));
		_issue = issue ?? throw new ArgumentNullException(nameof(issue));
		_constraint = constraint ?? Formula.True;
		_isExtendable = isExtendable;
	}

	public override string Name => _name;

	public override IReadOnlyList<Var> Variables => _variables;

	public override Formula Constraint => _constraint;

	public override bool IsExtendable(Body body, Assignment partialAssignment) =>
		_isExtendable != null ? _isExtendable(body, partialAssignment) : base.IsExtendable(body, partialAssignment);

	public override void Issue(Scheduler scheduler, Body body, Assignment assignment) =>
		_issue(scheduler, body, assignment);
}
