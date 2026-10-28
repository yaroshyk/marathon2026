using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GuestSessions;
using Moq;

namespace APIHandler.Tests.Services;

/// <summary>Owner group pre-order: homework levels 1-3.</summary>
public class GroupPreOrderOwnerTests
{
    private const string OwnerId = "owner-user";
    private const string GroupId = "group-1";
    private const string ReservationId = "reservation-1";

    private readonly Mock<IGuestSessionRepository> _sessions = new();
    private readonly Mock<IGroupReservationRepository> _groups = new();
    private readonly Mock<IDishRepository> _dishes = new();
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITableRepository> _tables = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly GuestSessionService _service;

    private readonly GroupReservation _group = new()
    {
        GroupReservationId = GroupId,
        ReservationId = ReservationId,
        ReservationToken = "token",
        Status = GroupReservationStatus.Open,
    };

    private readonly Reservation _reservation = new()
    {
        Id = ReservationId,
        TableId = "table-1",
        WaiterId = "waiter-1",
        CustomerId = OwnerId,
        GuestCount = 4,
    };

    public GroupPreOrderOwnerTests()
    {
        _service = new GuestSessionService(_groups.Object, _sessions.Object, _dishes.Object,
            _reservations.Object, _users.Object, _tables.Object, _orders.Object);

        _groups.Setup(r => r.GetGroupReservationByIdAsync(GroupId)).ReturnsAsync(_group);
        _groups.Setup(r => r.GetByReservationIdAsync(ReservationId)).ReturnsAsync(_group);
        _reservations.Setup(r => r.GetReservationByIdAsync(ReservationId)).ReturnsAsync(_reservation);
        _dishes.Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([Dish("dish1", "Caesar Salad", 180), Dish("dish2", "Tom Yum", 220)]);
    }

    private static Dish Dish(string id, string name, decimal price) => new() { Id = id, Name = name, Price = price, LocationId = "loc" };

    private static GuestSession Session(string id, string name, bool submitted, params (string DishId, int Qty)[] dishes) => new()
    {
        Id = id,
        GroupReservationId = GroupId,
        GuestName = name,
        IsSubmitted = submitted,
        Dishes = dishes.Select(d => new OrderItem { DishId = d.DishId, Quantity = d.Qty }).ToList(),
    };

    private void GivenSessions(params GuestSession[] sessions)
        => _sessions.Setup(r => r.GetAllByGroupReservationIdAsync(GroupId)).ReturnsAsync(sessions.ToList());

    // ---------- Level 1 ----------

    [Fact]
    public async Task GetGroupSessions_ReturnsParticipantsWithTotals()
    {
        GivenSessions(
            Session("session1", "Andrii", true, ("dish1", 2)),
            Session("session2", "Olena", false, ("dish2", 1)));

        var result = await _service.GetGroupSessionsAsync(OwnerId, GroupId);

        var participants = result.Participants.ToList();
        Assert.Equal(2, participants.Count);
        Assert.Equal(360, participants[0].Total);
        Assert.True(participants[0].IsSubmitted);
        Assert.Equal(ParticipantType.Guest, participants[0].Type);
        Assert.Equal(220, participants[1].Total);
        Assert.Equal(580, result.Total);
    }

    [Fact]
    public async Task GetGroupSessions_OwnerSessionIsMarkedAsOwnerAndListedFirst()
    {
        var owner = Session("owner-session", "Owner", true, ("dish1", 1));
        owner.IsOwnerSession = true;
        GivenSessions(Session("session1", "Andrii", true, ("dish2", 1)), owner);

        var result = await _service.GetGroupSessionsAsync(OwnerId, GroupId);

        var first = result.Participants.First();
        Assert.Equal("owner-session", first.Id);
        Assert.Equal(ParticipantType.Owner, first.Type);
    }

    [Fact]
    public async Task GetGroupSessions_NotOwner_ThrowsForbidden()
        => await Assert.ThrowsAsync<ActionForbiddenException>(() => _service.GetGroupSessionsAsync("someone-else", GroupId));

    [Fact]
    public async Task GetGroupSessions_GroupNotFound_ThrowsNotFound()
        => await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.GetGroupSessionsAsync(OwnerId, "missing"));

    // ---------- Level 2: owner session id ----------

    [Fact]
    public async Task GetOwnerGuestSessionId_ExistingOwnerSession_ReturnsItsId()
    {
        var owner = Session("owner-session", "Owner", false);
        owner.IsOwnerSession = true;
        GivenSessions(Session("session1", "Andrii", false), owner);

        var result = await _service.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId);

        Assert.Equal("owner-session", result.GuestSessionId);
        _sessions.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerGuestSessionId_NoOwnerSession_CreatesOne()
    {
        GivenSessions();
        GuestSession? saved = null;
        _sessions.Setup(r => r.SaveAsync(It.IsAny<GuestSession>())).Callback<GuestSession>(s => saved = s);

        var result = await _service.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId);

        Assert.NotNull(saved);
        Assert.True(saved!.IsOwnerSession);
        Assert.Equal(GroupId, saved.GroupReservationId);
        Assert.Equal(saved.Id, result.GuestSessionId);
    }

    [Fact]
    public async Task GetOwnerGuestSessionId_ReservationNotFound_ThrowsNotFound()
        => await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.GetOwnerGuestSessionIdAsync(OwnerId, "missing"));

    [Fact]
    public async Task GetOwnerGuestSessionId_NotOwner_ThrowsForbidden()
        => await Assert.ThrowsAsync<ActionForbiddenException>(() => _service.GetOwnerGuestSessionIdAsync("someone-else", ReservationId));

    // ---------- Level 2: submit consolidated pre-order ----------

    [Fact]
    public async Task SubmitGroupPreOrder_MergesSubmittedSessionsByDishId_AndUpdatesStatuses()
    {
        GivenSessions(
            Session("s1", "Andrii", true, ("dish1", 2), ("dish2", 1)),
            Session("s2", "Olena", true, ("dish1", 1), ("dish2", 1)),
            Session("s3", "Ivan", false, ("dish1", 10))); // not submitted: ignored

        var order = new Order
        {
            OrderId = "order-1", CustomerId = OwnerId, ReservationId = ReservationId,
            ReservationAddress = "addr", ReservationTime = "time", Dishes = [], Status = OrderStatus.Created,
        };
        _orders.Setup(r => r.GetOrderByReservationIdAsync(ReservationId)).ReturnsAsync(order);

        var result = await _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId);

        Assert.Equal(3, result.Single(d => d.DishId == "dish1").Quantity);
        Assert.Equal(2, result.Single(d => d.DishId == "dish2").Quantity);
        Assert.Equal(2, order.Dishes.Count);
        Assert.All(order.Dishes, d => Assert.Equal(OrderItemState.Pending, d.State));
        Assert.Equal(OrderStatus.SubmittedByCustomer, order.Status);
        Assert.Equal(GroupReservationStatus.Submitted, _group.Status);
        _orders.Verify(r => r.SaveOrderAsync(order), Times.Once);
        _groups.Verify(r => r.SaveGroupReservationAsync(_group), Times.Once);
    }

    [Fact]
    public async Task SubmitGroupPreOrder_NoSubmittedSessions_Throws()
    {
        GivenSessions(Session("s1", "Andrii", false, ("dish1", 1)));
        await Assert.ThrowsAsync<NoSubmittedGuestSessionsException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId));
    }

    [Fact]
    public async Task SubmitGroupPreOrder_SubmittedSessionsWithoutDishes_Throws()
    {
        GivenSessions(Session("s1", "Andrii", true));
        await Assert.ThrowsAsync<CartIsEmptyException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId));
    }

    [Fact]
    public async Task SubmitGroupPreOrder_OrderNotFound_ThrowsValidation()
    {
        GivenSessions(Session("s1", "Andrii", true, ("dish1", 1)));
        _orders.Setup(r => r.GetOrderByReservationIdAsync(ReservationId)).ReturnsAsync((Order?)null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId));
        Assert.Contains("Order", ex.Message);
        Assert.Equal(GroupReservationStatus.Open, _group.Status);
    }

    [Fact]
    public async Task SubmitGroupPreOrder_GroupNotFound_ThrowsNotFound()
        => await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, "missing", ReservationId));

    [Fact]
    public async Task SubmitGroupPreOrder_NotOwner_ThrowsForbidden()
        => await Assert.ThrowsAsync<ActionForbiddenException>(() => _service.SubmitGroupPreOrderAsync("someone-else", GroupId, ReservationId));

    [Fact]
    public async Task SubmitGroupPreOrder_AlreadySubmitted_Throws()
    {
        _group.Status = GroupReservationStatus.Submitted;
        await Assert.ThrowsAsync<GroupReservationSubmittedException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId));
    }

    // ---------- Level 3 ----------

    [Fact]
    public async Task DeleteGuestSession_ReturnsUpdatedSnapshot()
    {
        var andrii = Session("session1", "Andrii", true, ("dish1", 2));
        var olena = Session("session2", "Olena", false, ("dish2", 1));
        _sessions.Setup(r => r.GetByGuestSessionId("session1")).ReturnsAsync(andrii);
        GivenSessions(olena); // state after the delete

        var result = await _service.DeleteGuestSessionAsync(OwnerId, GroupId, "session1");

        _sessions.Verify(r => r.DeleteAsync(andrii), Times.Once);
        Assert.Equal("session2", Assert.Single(result.Participants).Id);
        Assert.Equal(220, result.Total);
    }

    [Fact]
    public async Task DeleteGuestSession_SessionFromAnotherGroup_ThrowsValidation()
    {
        var foreign = Session("session9", "Stranger", false);
        foreign.GroupReservationId = "other-group";
        _sessions.Setup(r => r.GetByGuestSessionId("session9")).ReturnsAsync(foreign);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.DeleteGuestSessionAsync(OwnerId, GroupId, "session9"));
        Assert.IsType<ValidationException>(ex);
        _sessions.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGuestSession_SessionNotFound_ThrowsNotFound()
        => await Assert.ThrowsAsync<GuestSessionNotFoundException>(() => _service.DeleteGuestSessionAsync(OwnerId, GroupId, "missing"));

    [Fact]
    public async Task DeleteGuestSession_NotOwner_ThrowsForbidden()
        => await Assert.ThrowsAsync<ActionForbiddenException>(() => _service.DeleteGuestSessionAsync("someone-else", GroupId, "session1"));
}
