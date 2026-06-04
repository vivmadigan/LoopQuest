using MediatR;

namespace LoopQuest.Application.Loops.Queries.GetLoops;

/// <summary>
/// Query: return the active loop library for the picker. Takes no parameters in v1.
///
/// This is the reference vertical slice. Copy this folder's shape — Query + Dto + Handler (and a
/// Validator when there are inputs) — for every new use case.
/// </summary>
public sealed record GetLoopsQuery : IRequest<IReadOnlyList<LoopDto>>;
