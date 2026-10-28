using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Helpers;
using APIHandler.Services.GuestSessions;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

// GET /reservations/{reservationId}/guest-session-id
public class GetOwnerGuestSessionIdHandler(IGuestSessionService guestSessionService) : IApiRequestHandler
{
    public Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("reservationId", out var reservationId))
            return Task.FromResult(ResponseBuilder.CreateErrorResponse(400, "Missing 'reservationId' in path parameters.", context.Logger));

        return OwnerGroupHandlerBase.ExecuteAsync(request, context, async userId =>
        {
            var result = await guestSessionService.GetOwnerGuestSessionIdAsync(userId, reservationId);
            return ResponseBuilder.CreateJsonResponse(200, result);
        });
    }
}
