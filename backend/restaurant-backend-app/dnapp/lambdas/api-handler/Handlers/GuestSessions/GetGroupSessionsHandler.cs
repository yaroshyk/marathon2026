using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Helpers;
using APIHandler.Services.GuestSessions;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GuestSessions;

// GET /guest/groups/{groupReservationId}/sessions
public class GetGroupSessionsHandler(IGuestSessionService guestSessionService) : IApiRequestHandler
{
    public Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("groupReservationId", out var groupReservationId))
            return Task.FromResult(ResponseBuilder.CreateErrorResponse(400, "Missing 'groupReservationId' in path parameters.", context.Logger));

        return OwnerGroupHandlerBase.ExecuteAsync(request, context, async userId =>
        {
            var snapshot = await guestSessionService.GetGroupSessionsAsync(userId, groupReservationId);
            return ResponseBuilder.CreateJsonResponse(200, snapshot);
        });
    }
}
