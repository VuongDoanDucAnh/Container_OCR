using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Models;

namespace DoAnOlympics.Api.Repositories;

public interface IContainerRecordRepository
{
    Task<ContainerRecord> ThemMoiAsync(ContainerRecord record);
    Task<List<ContainerRecord>> LayTheoTaiXeAsync(int driverId);
    Task<ContainerRecord?> TimBanGhiTrungAsync(string maContainer);
}

public class ContainerRecordRepository : IContainerRecordRepository
{
    private readonly AppDbContext _db;

    public ContainerRecordRepository(AppDbContext db) => _db = db;

    public async Task<ContainerRecord> ThemMoiAsync(ContainerRecord record)
    {
        _db.ContainerRecords.Add(record);
        await _db.SaveChangesAsync();
        return record;
    }

    public Task<List<ContainerRecord>> LayTheoTaiXeAsync(int driverId) =>
        _db.ContainerRecords.Where(c => c.DriverId == driverId)
            .OrderByDescending(c => c.ThoiGianQuet)
            .ToListAsync();

    public Task<ContainerRecord?> TimBanGhiTrungAsync(string maContainer) =>
        _db.ContainerRecords
            .Where(c => c.MaContainer == maContainer && c.ChecksumHopLe)
            .OrderByDescending(c => c.ThoiGianQuet)
            .FirstOrDefaultAsync();
}