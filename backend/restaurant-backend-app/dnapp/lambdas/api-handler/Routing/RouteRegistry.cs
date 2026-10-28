using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using Amazon.SQS;
using APIHandler.Constants;
using APIHandler.Handlers;
using APIHandler.Handlers.Dishes;
using APIHandler.Handlers.Feedbacks;
using APIHandler.Handlers.GroupReservations;
using APIHandler.Handlers.GuestReservations;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Handlers.Locations;
using APIHandler.Handlers.Notifications;
using APIHandler.Handlers.Order;
using APIHandler.Handlers.Reports;
using APIHandler.Handlers.Reservations;
using APIHandler.Handlers.Tables;
using APIHandler.Handlers.Tables.Management;
using APIHandler.Handlers.TimeSlots;
using APIHandler.Handlers.Users;
using APIHandler.Handlers.Waiters;
using APIHandler.Records;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.Feedbacks;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Locations;
using APIHandler.Repositories.Notifications;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reports;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Repositories.Waiters;
using APIHandler.Services;
using APIHandler.Services.Auth;
using APIHandler.Services.Dishes;
using APIHandler.Services.Feedbacks;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.GuestSessions;
using APIHandler.Services.Locations;
using APIHandler.Services.Notifications;
using APIHandler.Services.Orders;
using APIHandler.Services.Reports;
using APIHandler.Services.Reports.Interfaces;
using APIHandler.Services.Reservations;
using APIHandler.Services.Tables;
using APIHandler.Services.Waiters;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace APIHandler.Routing;

public static class RouteRegistry
{
    private static readonly List<RouteHandler> CachedRoutes = BuildRoutes();

    public static List<RouteHandler> GetAllRoutes() => CachedRoutes;

    private static List<RouteHandler> BuildRoutes()
    {
        var services = new ServiceCollection();
        var region = RegionEndpoint.GetBySystemName(ResourceNames.Region);

        // 1. AWS Clients
        var dynamoDbClient = new AmazonDynamoDBClient(region);
        services.AddSingleton(dynamoDbClient);
        services.AddSingleton<IAmazonDynamoDB>(dynamoDbClient);
        services.AddSingleton<IDynamoDBContext>(sp => new DynamoDBContextBuilder()
            .WithDynamoDBClient(() => sp.GetRequiredService<IAmazonDynamoDB>())
            .Build());

        var cognitoClient = new AmazonCognitoIdentityProviderClient(region);
        services.AddSingleton(cognitoClient);
        services.AddSingleton<IAmazonCognitoIdentityProvider>(cognitoClient);

        var sqsClient = new AmazonSQSClient(region);
        services.AddSingleton(sqsClient);
        services.AddSingleton<IAmazonSQS>(sqsClient);

        var s3Client = new AmazonS3Client(region);
        services.AddSingleton(s3Client);
        services.AddSingleton<IAmazonS3>(s3Client);

        // 2. Repositories
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IDishRepository, DishRepository>();
        services.AddSingleton<ILocationRepository, LocationRepository>();
        services.AddSingleton<IWaiterRepository, WaiterRepository>();
        services.AddSingleton<INotificationRepository, NotificationRepository>();
        services.AddSingleton<IOrderRepository, OrderRepository>();
        services.AddSingleton<IReservationRepository, ReservationRepository>();
        services.AddSingleton<IFeedbackRepository, FeedbackRepository>();
        services.AddSingleton<IReportRepository, ReportRepository>();
        services.AddSingleton<IGuestSessionRepository, GuestSessionRepository>();
        services.AddSingleton<IGroupReservationRepository, GroupReservationRepository>();
        services.AddSingleton<ITableRepository, TableRepository>();

        // 3. Services
        services.AddSingleton<S3ImageService>();
        services.AddSingleton<IReportFileService, ReportFileService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IDishService, DishService>();
        services.AddSingleton<IWaiterService, WaiterService>();
        services.AddSingleton<ILocationService, LocationService>();
        services.AddSingleton<ITableService, TableService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IOrderService, OrderService>();
        services.AddSingleton<IGroupReservationService, GroupReservationService>();
        services.AddSingleton<IReservationService, ReservationService>();
        services.AddSingleton<IFeedbackService, FeedbackService>();
        services.AddSingleton<IGuestSessionService, GuestSessionService>();
        services.AddSingleton<IGuestAuthorizationService, GuestAuthorizationService>();
        services.AddSingleton<IReportService, ReportService>();

        var provider = services.BuildServiceProvider();

        List<RouteHandler> GetAuthAndUserRoutes() => [
            new RouteHandler(new Route(HttpMethods.POST, "/register"), ActivatorUtilities.CreateInstance<RegistrationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/signin"), ActivatorUtilities.CreateInstance<SignInHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/me"), ActivatorUtilities.CreateInstance<GetAuthorizedUserHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PUT, "/me"), ActivatorUtilities.CreateInstance<EditUserHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PUT, "/me/change-password"), ActivatorUtilities.CreateInstance<ChangeOwnPasswordHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/users"), ActivatorUtilities.CreateInstance<GetUsersHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/users/{id}/profile-image"), ActivatorUtilities.CreateInstance<GetUserProfileImageHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PUT, "/me/profile-image"), ActivatorUtilities.CreateInstance<UploadUserProfileImageHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/users/{id}/name"), ActivatorUtilities.CreateInstance<GetUserNameByIdHandler>(provider))
        ];

        List<RouteHandler> GetLocationTableAndWaiterRoutes() => [
            // Locations
            new RouteHandler(new Route(HttpMethods.POST, "/locations"), ActivatorUtilities.CreateInstance<CreateLocationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/locations"), ActivatorUtilities.CreateInstance<GetLocationsHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/locations/{id}/dishes"), ActivatorUtilities.CreateInstance<GetDishesByLocationIdHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/locations/{id}"), ActivatorUtilities.CreateInstance<GetLocationByIdHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PUT, "/locations/{id}"), ActivatorUtilities.CreateInstance<EditLocationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.DELETE, "/locations/{id}"), ActivatorUtilities.CreateInstance<DeleteLocationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.DELETE, "/locations/{locationId}/dishes/{dishId}"), ActivatorUtilities.CreateInstance<RemoveDishFromLocationHandler>(provider)),
            
            // Tables
            new RouteHandler(new Route(HttpMethods.POST, "/tables"), ActivatorUtilities.CreateInstance<CreateTableHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/available-tables"), ActivatorUtilities.CreateInstance<GetAvailableTablesHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/tables/{id}/available-time-slots"), ActivatorUtilities.CreateInstance<GetAvailableTimeSlotsHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/locations/{locationId}/tables/all"), ActivatorUtilities.CreateInstance<GetLocationTablesHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PUT, "/tables/{id}"), ActivatorUtilities.CreateInstance<EditTableHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.DELETE, "/tables/{id}"), ActivatorUtilities.CreateInstance<DeleteTableHandler>(provider)),
            
            // Waiters
            new RouteHandler(new Route(HttpMethods.POST, "/waiters"), ActivatorUtilities.CreateInstance<AddWaiterHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/waiters/{userId}/location"), ActivatorUtilities.CreateInstance<GetWaiterLocationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.DELETE, "/waiters"), ActivatorUtilities.CreateInstance<DeleteWaiterHandler>(provider))
        ];

        List<RouteHandler> GetDishRoutes() => [
            new RouteHandler(new Route(HttpMethods.POST, "/dishes"), ActivatorUtilities.CreateInstance<CreateDishHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/dishes"), ActivatorUtilities.CreateInstance<GetAllDishesHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/dishes/popular"), ActivatorUtilities.CreateInstance<GetPopularDishesHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/dishes/{id}"), ActivatorUtilities.CreateInstance<GetDishByIdHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PUT, "/dishes/{id}"), ActivatorUtilities.CreateInstance<EditDishHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.DELETE, "/dishes/{id}"), ActivatorUtilities.CreateInstance<DeleteDishHandler>(provider))
        ];

        List<RouteHandler> GetReservationAndOrderRoutes() => [
            // Reservations
            new RouteHandler(new Route(HttpMethods.POST, "/reservations"), ActivatorUtilities.CreateInstance<CreateReservationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/reservations"), ActivatorUtilities.CreateInstance<GetReservationsForUserHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/reservations/{reservationId}"), ActivatorUtilities.CreateInstance<GetReservationByIdHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PATCH, "/reservations/{reservationId}/status"), ActivatorUtilities.CreateInstance<UpdateReservationStatusHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/reservations/{reservationId}/feedback"), ActivatorUtilities.CreateInstance<GetFeedbackForReservationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.DELETE, "/reservations"), ActivatorUtilities.CreateInstance<CancelReservationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PUT, "/reservations"), ActivatorUtilities.CreateInstance<EditReservationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PATCH, "/reservations/{reservationId}/table"), ActivatorUtilities.CreateInstance<UpdateReservationTableHandler>(provider)),            
            // Orders
            new RouteHandler(new Route(HttpMethods.GET, "/orders"), ActivatorUtilities.CreateInstance<GetOrdersByCustomerHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/orders/{reservationId}"), ActivatorUtilities.CreateInstance<GetOrderByReservationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/orders"), ActivatorUtilities.CreateInstance<CreateOrderHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PUT, "/orders/{reservationId}"), ActivatorUtilities.CreateInstance<EditPreOrderHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.DELETE, "/orders/{reservationId}"), ActivatorUtilities.CreateInstance<CancelPreOrderHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/orders/{reservationId}/submit"), ActivatorUtilities.CreateInstance<SubmitOrderHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/orders/{reservationId}/unsubmit"), ActivatorUtilities.CreateInstance<UnsubmitOrderHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PATCH, "/reservations/{reservationId}/order/confirmations/{dishId}"), ActivatorUtilities.CreateInstance<ConfirmPreOrderDishHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PATCH, "/reservations/{reservationId}/order/cancellations/{dishId}"), ActivatorUtilities.CreateInstance<CancelPreOrderDishHandler>(provider))
        ];

        List<RouteHandler> GetGuestAndGroupRoutes() => [
            new RouteHandler(new Route(HttpMethods.POST, "/guest/sessions"), ActivatorUtilities.CreateInstance<CreateGuestSessionHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/guest/sessions/{guestSessionId}"), ActivatorUtilities.CreateInstance<GetGuestSessionHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/guest/sessions/{guestSessionId}/dishes"), ActivatorUtilities.CreateInstance<AddDishToGuestSessionHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.DELETE, "/guest/sessions/{guestSessionId}/dishes"), ActivatorUtilities.CreateInstance<RemoveDishFromGuestSessionHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/guest/sessions/{guestSessionId}/submit"), ActivatorUtilities.CreateInstance<SubmitGuestCartHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/guest/sessions/{guestSessionId}/menu"), ActivatorUtilities.CreateInstance<GetGuestSessionMenuHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/guest/groups/{groupReservationId}/status"), ActivatorUtilities.CreateInstance<GetGroupReservationStatusHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/guest/invites/{token}"), ActivatorUtilities.CreateInstance<GetGroupReservationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/reservations/{reservationId}/invite-token"), ActivatorUtilities.CreateInstance<GetInviteTokenHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.DELETE, "/guest/groups/{groupReservationId}/sessions/{guestSessionId}"), ActivatorUtilities.CreateInstance<DeleteGuestSessionHandler>(provider)),

            // Owner: group pre-order management
            new RouteHandler(new Route(HttpMethods.GET, "/guest/groups/{groupReservationId}/sessions"), ActivatorUtilities.CreateInstance<GetGroupSessionsHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/reservations/{reservationId}/guest-session-id"), ActivatorUtilities.CreateInstance<GetOwnerGuestSessionIdHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/group-reservations/{groupReservationId}/submit"), ActivatorUtilities.CreateInstance<SubmitGroupPreOrderHandler>(provider))
        ];
        List<RouteHandler> GetAuxiliaryRoutes() => [
            new RouteHandler(new Route(HttpMethods.GET, "/feedback"), ActivatorUtilities.CreateInstance<GetFeedbackForLocationHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.POST, "/feedback"), ActivatorUtilities.CreateInstance<CreateFeedbackHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PUT, "/feedback"), ActivatorUtilities.CreateInstance<UpdateFeedbackHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/reports/download"), ActivatorUtilities.CreateInstance<DownloadReportHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/reports/generate"), ActivatorUtilities.CreateInstance<GenerateReportHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/reports"), ActivatorUtilities.CreateInstance<GetUserReportsHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.GET, "/notifications"), ActivatorUtilities.CreateInstance<GetWaiterNotificationsHandler>(provider)),
            new RouteHandler(new Route(HttpMethods.PATCH, "/notifications/{id}/read"), ActivatorUtilities.CreateInstance<MarkNotificationReadHandler>(provider))
        ];

        var allRoutes = new List<RouteHandler>();
        allRoutes.AddRange(GetAuthAndUserRoutes());
        allRoutes.AddRange(GetLocationTableAndWaiterRoutes());
        allRoutes.AddRange(GetDishRoutes());
        allRoutes.AddRange(GetReservationAndOrderRoutes());
        allRoutes.AddRange(GetGuestAndGroupRoutes());
        allRoutes.AddRange(GetAuxiliaryRoutes());

        return allRoutes;
    }
}



