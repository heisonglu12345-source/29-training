using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using OrderHub.Core.Ai;
using OrderHub.Core.Services;

namespace OrderHub.Web.Controllers.Api;

[ApiController]
[Route("api/orders")]
public class OrdersApiController(
    IOrderSearchService searchService,
    IOrderService orderService) : ControllerBase
{
    [HttpPost("search")]
    public async Task<IActionResult> Search(
        [FromBody] SearchOrdersRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await searchService.SearchAsync(request.Text, cancellationToken);
            if (!result.Success)
                return UnprocessableEntity(new { error = result.ErrorMessage });

            var response = result.Value!.Select(order => new
            {
                order.Id,
                CustomerName = order.Customer?.Name,
                Tier = order.Customer?.Tier.ToString(),
                Status = order.Status.ToString(),
                Total = orderService.CalculateTotal(order),
                order.CreatedAt
            });

            return Ok(response);
        }
        catch (AiServiceUnavailableException ex)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = ex.Message });
        }
    }
}

public class SearchOrdersRequest
{
    [Required(ErrorMessage = "text 為必填")]
    public string Text { get; set; } = string.Empty;
}
