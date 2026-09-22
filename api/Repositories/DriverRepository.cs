using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Models;

namespace DoAnOlympics.Api.Repositories;

public interface IDriverRepository
{
    Task<Driver?> TimTheoUsernameAsync(string username);
    Task<Driver?> TimTheoIdAsync(int id);
    Task<Driver> ThemMoiAsync(Driver driver);
    Task<bool> UsernameDaTonTaiAsync(string username);
    Task<bool> MaTaiXeDaTonTaiAsync(string maTaiXe);
    Task CapNhatAsync(Driver driver);
}

public class DriverRepository : IDriverRepository
{
    private readonly AppDbContext _db;

    public DriverRepository(AppDbContext db) => _db = db;

    public Task<Driver?> TimTheoUsernameAsync(string username) =>
        _db.Drivers.FirstOrDefaultAsync(d => d.Username == username);

    public Task<Driver?> TimTheoIdAsync(int id) =>
        _db.Drivers.FirstOrDefaultAsync(d => d.Id == id);

    public async Task<Driver> ThemMoiAsync(Driver driver)
    {
        _db.Drivers.Add(driver);
        await _db.SaveChangesAsync();
        return driver;
    }

    public Task<bool> UsernameDaTonTaiAsync(string username) =>
        _db.Drivers.AnyAsync(d => d.Username == username);

    public Task<bool> MaTaiXeDaTonTaiAsync(string maTaiXe) =>
        _db.Drivers.AnyAsync(d => d.MaTaiXe == maTaiXe);

    public Task CapNhatAsync(Driver driver)
    {
        _db.Drivers.Update(driver);
        return _db.SaveChangesAsync();
    }
}