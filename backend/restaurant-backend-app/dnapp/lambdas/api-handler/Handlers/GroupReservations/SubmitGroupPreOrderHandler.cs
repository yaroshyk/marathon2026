using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Helpers;
using APIHandler.Models.GroupReservation;
using APIHandler.Services.GuestSessions;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

// POST /group-reservations/{groupReservationId}/submit
public class SubmitGroupPreOrderHandler(IGuestSessionService guestSessionService) : IApiRequestHandler
{
    public Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("groupReservationId", out var groupReservationId))
            return Task.FromResult(ResponseBuilder.CreateErrorResponse(400, "Missing 'groupReservationId' in path parameters.", context.Logger));

        if (!RequestBodyHelper.TryParseAndValidate<SubmitGroupPreOrderRequest>(request, context, out var body, out var errorResponse))
            return Task.FromResult(errorResponse);

        return OwnerGroupHandlerBase.ExecuteAsync(request, context, async userId =>
        {
            var dishes = await guestSessionService.SubmitGroupPreOrderAsync(userId, groupReservationId, body.ReservationId);
            return ResponseBuilder.CreateJsonResponse(200, dishes);
        });
    }
}
