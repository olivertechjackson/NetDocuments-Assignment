using GameOfLife.Api.Contracts;
using GameOfLife.Core.Abstractions;
using GameOfLife.Core.Configuration;
using GameOfLife.Core.Domain;
using GameOfLife.Core.Simulation;
using GameOfLife.Core.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GameOfLife.Api.Controllers;

// No class-level [Produces]: it would pin every response to application/json and
// suppress the application/problem+json content type on ProblemDetails errors.
[ApiController]
[Route("api/boards")]
public sealed class BoardsController : ControllerBase
{
    private readonly IBoardRepository _repository;
    private readonly ISimulationService _simulation;
    private readonly GameSettings _settings;

    public BoardsController(
        IBoardRepository repository,
        ISimulationService simulation,
        IOptions<GameSettings> settings)
    {
        _repository = repository;
        _simulation = simulation;
        _settings = settings.Value;
    }

    /// <summary>Uploads a new board; the stored state is an immutable generation-0 baseline.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BoardResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateBoardRequest request, CancellationToken cancellationToken)
    {
        BoardValidationResult validation = BoardStateValidator.Validate(request.State, _settings.MaxGridDimension);

        if (!validation.IsValid)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid board state.",
                detail: validation.Error);
        }

        var board = new Board(
            Guid.NewGuid(),
            validation.Rows,
            validation.Cols,
            validation.Cells!,
            DateTimeOffset.UtcNow);

        await _repository.AddAsync(board, cancellationToken);

        var response = new BoardResponse(
            board.Id,
            board.Rows,
            board.Cols,
            Generation: 0,
            GridMapper.ToJagged(board.Cells, board.Rows, board.Cols));

        return CreatedAtAction(nameof(GetById), new { id = board.Id }, response);
    }

    /// <summary>Returns the stored generation-0 baseline.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BoardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Board? board = await _repository.GetAsync(id, cancellationToken);

        if (board is null)
        {
            return BoardNotFound(id);
        }

        var response = new BoardResponse(
            board.Id,
            board.Rows,
            board.Cols,
            Generation: 0,
            GridMapper.ToJagged(board.Cells, board.Rows, board.Cols));

        return Ok(response);
    }

    /// <summary>Returns generation 1, computed from the stored baseline.</summary>
    [HttpGet("{id:guid}/next")]
    [ProducesResponseType(typeof(BoardStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNext(Guid id, CancellationToken cancellationToken)
    {
        Board? board = await _repository.GetAsync(id, cancellationToken);

        if (board is null)
        {
            return BoardNotFound(id);
        }

        bool[] state = _simulation.ComputeGeneration(board, steps: 1);

        return Ok(new BoardStateResponse(board.Id, Generation: 1, GridMapper.ToJagged(state, board.Rows, board.Cols)));
    }

    /// <summary>Returns the state exactly <paramref name="steps"/> generations ahead of the baseline.</summary>
    [HttpGet("{id:guid}/states/{steps:int}")]
    [ProducesResponseType(typeof(BoardStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStateAfterSteps(Guid id, int steps, CancellationToken cancellationToken)
    {
        if (steps < 1 || steps > _settings.MaxAllowedSteps)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid step count.",
                detail: $"steps must be between 1 and {_settings.MaxAllowedSteps}, but was {steps}.");
        }

        Board? board = await _repository.GetAsync(id, cancellationToken);

        if (board is null)
        {
            return BoardNotFound(id);
        }

        bool[] state = _simulation.ComputeGeneration(board, steps);

        return Ok(new BoardStateResponse(board.Id, steps, GridMapper.ToJagged(state, board.Rows, board.Cols)));
    }

    /// <summary>
    /// Simulates until the board is stable or oscillating, or returns 422 if neither
    /// happens within the configured iteration limit.
    /// </summary>
    [HttpGet("{id:guid}/final")]
    [ProducesResponseType(typeof(FinalStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetFinalState(Guid id, CancellationToken cancellationToken)
    {
        Board? board = await _repository.GetAsync(id, cancellationToken);

        if (board is null)
        {
            return BoardNotFound(id);
        }

        FinalStateResult result = _simulation.ComputeFinalState(board, _settings.MaxFinalIterations);

        if (result.Kind == FinalStateKind.NotReached)
        {
            return Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Board did not reach a final state.",
                detail: $"No stable or oscillating state was found within {_settings.MaxFinalIterations} iterations. " +
                        "Increase Game:MaxFinalIterations or inspect the board pattern.");
        }

        var response = new FinalStateResponse(
            board.Id,
            result.Kind.ToString(),
            result.Generation,
            result.CyclePeriod,
            GridMapper.ToJagged(result.State, board.Rows, board.Cols));

        return Ok(response);
    }

    private ObjectResult BoardNotFound(Guid id)
    {
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Board not found.",
            detail: $"No board exists with id '{id}'.");
    }
}
