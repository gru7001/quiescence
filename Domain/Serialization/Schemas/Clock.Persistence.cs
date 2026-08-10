public static class ClockPersistence
{
	public const string SaveSchemaId = "clock.v1";

	public sealed record ClockSave(long Now);

	public static ClockSave Encode(Clock clock) => new(Now: clock.Now);

	public static void Apply(Clock clock, ClockSave save, LoadSession session) => clock.Set(save.Now);
}

