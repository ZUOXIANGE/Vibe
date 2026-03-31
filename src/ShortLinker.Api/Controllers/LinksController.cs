using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShortLinker.Api.Infrastructure;
using ShortLinker.Api.Models;
using ShortLinker.Api.Services;

namespace ShortLinker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class LinksController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IShortcodeGenerator _generator;

    public LinksController(ApplicationDbContext db, IShortcodeGenerator generator)
    {
        _db = db;
        _generator = generator;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ShortLink>>> Create([FromBody] string originalUrl)
    {
        var shortCode = _generator.Generate();
        var link = new ShortLink { OriginalUrl = originalUrl, ShortCode = shortCode };
        _db.ShortLinks.Add(link);
        await _db.SaveChangesAsync();
        return ApiResponse<ShortLink>.Success(link);
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ShortLink>>>> GetList()
    {
        var links = await _db.ShortLinks.ToListAsync();
        return ApiResponse<List<ShortLink>>.Success(links);
    }
}
