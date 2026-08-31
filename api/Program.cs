using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Repositories;
using DoAnOlympics.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// DbContext - connection string lấy từ user-secrets (ConnectionStrings:DefaultConnection)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<IContainerRecordRepository, ContainerRecordRepository>();

// Services
builder.Services.AddHttpClient<IGeminiOcrClient, GeminiOcrClient>();
builder.Services.AddScoped<IGoogleSheetsService, GoogleSheetsService>();
builder.Services.AddScoped<IJwtService, JwtService>();

// JWT Authentication - key lấy từ user-secrets (Jwt:SigningKey)
string jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? "khoa-tam-de-build-khong-loi---HAY-THAY-BANG-USER-SECRETS-THUC-TE";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "DoAnOlympics",
            ValidateAudience = true,
            ValidAudience = "DoAnOlympics",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Dán vào theo dạng: Bearer {token}"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
