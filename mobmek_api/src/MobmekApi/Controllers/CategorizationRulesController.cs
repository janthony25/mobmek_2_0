using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/categorization-rules")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class CategorizationRulesController(ICategorizationRuleService ruleService) : ControllerBase
{
    /// <summary>All rules in evaluation order (priority, then name).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategorizationRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategorizationRuleDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await ruleService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CategorizationRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategorizationRuleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var rule = await ruleService.GetByIdAsync(id, cancellationToken);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategorizationRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CategorizationRuleDto>> Create(CreateCategorizationRuleRequest request, CancellationToken cancellationToken)
    {
        var (rule, error) = await ruleService.CreateAsync(request, cancellationToken);
        if (error != CategorizationRuleWriteError.None)
        {
            return MapError(error);
        }

        return CreatedAtAction(nameof(GetById), new { id = rule!.Id }, rule);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CategorizationRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategorizationRuleDto>> Update(Guid id, UpdateCategorizationRuleRequest request, CancellationToken cancellationToken)
    {
        var (rule, error) = await ruleService.UpdateAsync(id, request, cancellationToken);
        return error == CategorizationRuleWriteError.None ? Ok(rule) : MapError(error);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return await ruleService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>The winning rule's pre-fill for what's been typed so far, or 204 when nothing matches.</summary>
    [HttpPost("suggest")]
    [ProducesResponseType(typeof(RuleSuggestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<RuleSuggestionDto>> Suggest(RuleSuggestionRequest request, CancellationToken cancellationToken)
    {
        var suggestion = await ruleService.SuggestAsync(request, cancellationToken);
        return suggestion is null ? NoContent() : Ok(suggestion);
    }

    /// <summary>
    /// Applies the rule to existing unmanaged history. commit=false (default) previews the
    /// match/update counts without changing anything.
    /// </summary>
    [HttpPost("{id:guid}/apply-to-existing")]
    [ProducesResponseType(typeof(ApplyRuleResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplyRuleResultDto>> ApplyToExisting(
        Guid id, [FromQuery] bool commit = false, CancellationToken cancellationToken = default)
    {
        var (result, error) = await ruleService.ApplyToExistingAsync(id, commit, cancellationToken);
        return error == CategorizationRuleWriteError.None ? Ok(result) : MapError(error);
    }

    private ActionResult MapError(CategorizationRuleWriteError error) => error switch
    {
        CategorizationRuleWriteError.NotFound => NotFound(),
        CategorizationRuleWriteError.CategoryNotFound => BadRequest("The category does not exist."),
        CategorizationRuleWriteError.PayeeNotFound => BadRequest("The payee does not exist."),
        CategorizationRuleWriteError.InvalidMatchField => BadRequest("Match field must be \"Description\", \"Counterparty\" or \"Either\"."),
        CategorizationRuleWriteError.InvalidMatchType => BadRequest("Match type must be \"Contains\", \"StartsWith\" or \"Equals\"."),
        CategorizationRuleWriteError.InvalidDirection => BadRequest("Direction must be \"In\" or \"Out\"."),
        CategorizationRuleWriteError.InvalidGstTreatment => BadRequest("GST treatment must be \"Taxable\", \"Exempt\" or \"ZeroRated\"."),
        CategorizationRuleWriteError.InvalidAmountBand => BadRequest("Amount min can't be greater than amount max."),
        _ => BadRequest(),
    };
}
