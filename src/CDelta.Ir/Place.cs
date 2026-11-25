namespace CDelta.Ir;

public readonly record struct Place(PlaceBase Base, int Id, IdRange Projections);
