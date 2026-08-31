using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using DoAnOlympics.Api.Models;

namespace DoAnOlympics.Api.Services;

public interface IJwtService
{
    string TaoToken(Driver driver);
}

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string TaoToken(Driver driver)
    {
        string signingKey = _config["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Chưa cấu hình Jwt:SigningKey trong user-secrets");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, driver.Id.ToString()),
            new Claim("MaTaiXe", driver.MaTaiXe),
            new Claim(ClaimTypes.Name, driver.HoTen)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "DoAnOlympics",
            audience: "DoAnOlympics",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
