using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;

namespace DoAnOlympics.Api.Controllers;

[ApiController]
[Route("api/loaihinhchuyen")]
[Authorize]
public class LoaiHinhChuyenController : ControllerBase
{
    private readonly AppDbContext _db;

    public LoaiHinhChuyenController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> DanhSach()
    {
        var ds = await _db.LoaiHinhChuyens
            .AsNoTracking()
            .OrderBy(l => l.Id)
            .Select(l => new { l.Id, l.Ten })
            .ToListAsync();

        return Ok(ds);
    }
}