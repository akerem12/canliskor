namespace CanliSkor.Core.Domain;

/// <param name="Code">Our league identifier, e.g. "tur.1". Also used as the SignalR group key.</param>
public sealed record League(string Code, string Name);
