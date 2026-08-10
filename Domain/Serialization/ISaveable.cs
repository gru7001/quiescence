public interface ISaveable
{
	SaveNode SaveTo(SaveSession session);
}

public interface ISaveable<TRecord> : ISaveable
{
	new SaveNode<TRecord> SaveTo(SaveSession session);
}

