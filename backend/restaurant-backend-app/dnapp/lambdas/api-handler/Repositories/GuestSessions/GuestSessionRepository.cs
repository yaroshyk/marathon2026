using Amazon.DynamoDBv2.DataModel;
using APIHandler.Constants;
using APIHandler.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Repositories.GuestSessions;

public class GuestSessionRepository(IDynamoDBContext context) : IGuestSessionRepository
{
    public async Task<GuestSession?> GetByGuestSessionId(string id) => await context.LoadAsync<GuestSession?>(id, new LoadConfig()
    {
        OverrideTableName = ResourceNames.GuestSessionTableName
    });

    public async Task<GuestSession?> GetByGroupReservationId(string groupReservationId)
    {
        List<GuestSession> result = await context.QueryAsync<GuestSession>(groupReservationId, new QueryConfig
        {
            OverrideTableName = ResourceNames.GuestSessionTableName,
            IndexName = GlobalSecondaryIndexes.GuestSession.GroupReservationId
        }).GetRemainingAsync();

        return result.FirstOrDefault();
    }

    public async Task<GuestSession?> GetByTokenHash(string tokenHash)
    {
        List<GuestSession> result = await context.QueryAsync<GuestSession>(tokenHash, new QueryConfig
        {
            OverrideTableName = ResourceNames.GuestSessionTableName,
            IndexName = GlobalSecondaryIndexes.GuestSession.SessionTokenHash
        }).GetRemainingAsync();

        return result.FirstOrDefault();
    }

    public async Task<List<GuestSession>> GetAllByGroupReservationIdAsync(string groupReservationId)
    {
        return await context.QueryAsync<GuestSession>(groupReservationId, new QueryConfig
        {
            OverrideTableName = ResourceNames.GuestSessionTableName,
            IndexName = GlobalSecondaryIndexes.GuestSession.GroupReservationId
        }).GetRemainingAsync();
    }

    public async Task SaveAsync(GuestSession guestSession) => await context.SaveAsync(guestSession, new SaveConfig
    {
        OverrideTableName = ResourceNames.GuestSessionTableName
    });

    public async Task<int> CountByGroupReservationId(string groupReservationId)
    {
        List<GuestSession> result = await context.QueryAsync<GuestSession>(groupReservationId, new QueryConfig
        {
            OverrideTableName = ResourceNames.GuestSessionTableName,
            IndexName = GlobalSecondaryIndexes.GuestSession.GroupReservationId
        }).GetRemainingAsync();

        // Counts joined guests only. The owner is always added as "+1" by the callers,
        // so the owner session must not be counted here a second time.
        return result.Count(s => !s.IsOwnerSession);
    }

    public async Task DeleteAsync(GuestSession session)
    {
        await context.DeleteAsync(session, DynamoConfig.DeleteConfig(ResourceNames.GuestSessionTableName));
    }
}
