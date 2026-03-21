using ApiServer.Data;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace ApiServer.Controllers;


[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConnectionMultiplexer _redis;

    public HealthController(AppDbContext db, IConnectionMultiplexer redis)
    {
        _db = db;
        _redis = redis;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var postgresOk = await CheckPostgres();
        var redisOk = CheckRedis();

        var result = new
        {
            status = postgresOk && redisOk ? "healthy" : "degraded",
            postgres = postgresOk ? "connected" : "unreachable",
            redis = redisOk ? "connected" : "unreachable"
        };

        return postgresOk && redisOk ? Ok(result) : StatusCode(503, result);
    }

    private async Task<bool> CheckPostgres()
    {
        try
        {
            return await _db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private bool CheckRedis()
    {
        try
        {
            var db = _redis.GetDatabase();
            db.Ping();
            return true;
        }
        catch
        {
            return false;
        }
    }
}