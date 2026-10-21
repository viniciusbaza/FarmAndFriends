using FarmAndFriends.Api.Domain.Entities;
using FarmAndFriends.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FarmAndFriends.Api.Domain.Services;

/// <summary>One definition of a fertilizer application belonging to a plot's current harvest cycle.</summary>
internal static class FertilizerApplicationQuery
{
    public static IQueryable<FertilizerApplication> CurrentForFarm(AppDbContext db, Guid farmId) =>
        db.FertilizerApplications.AsNoTracking()
            .Join(db.Plots.AsNoTracking(), application => application.PlotId, plot => plot.Id,
                (application, plot) => new { application, plot })
            .Where(x => x.application.FarmId == farmId
                && x.application.HarvestCycle == x.plot.CurrentHarvestCycle
                && x.application.CycleStartedAt == x.plot.CurrentHarvestCycleStartedAt)
            .Select(x => x.application);
}
