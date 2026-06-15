using System.Data;
using System.Text;
using Medpointe.Data;
using Medpointe.Models.Api;
using Medpointe.Repositories;
using Medpointe.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

string connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is missing.");

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PatientsService>();
builder.Services.AddScoped<LanguagesService>();
builder.Services.AddScoped<BillingClaimsService>();
builder.Services.AddScoped<ScheduleService>();

builder.Services.AddScoped<PatientsRepository>();
builder.Services.AddScoped<AuthRepository>();
builder.Services.AddScoped<LanguagesRepository>();
builder.Services.AddScoped<BillingClaimsRepository>();
builder.Services.AddScoped<ScheduleRepository>();

builder.Services.AddScoped<DatabaseClient>();
builder.Services.AddScoped<IDbConnection>(_ => new NpgsqlConnection(connectionString));

builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyHeader();
            policy.AllowAnyMethod();
            policy.AllowAnyOrigin();
        });
    }
);

IConfigurationSection jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is missing.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                // Skip the default logic
                context.HandleResponse();

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                if (context.AuthenticateFailure is SecurityTokenExpiredException)
                {
                    await context.Response.WriteAsJsonAsync(new ApiError
                    {
                        Title = "Invalid Token",
                        Message = "The token has expired",
                        Code = "EXPIRED_TOKEN"
                    });
                }
                else
                {
                    await context.Response.WriteAsJsonAsync(new ApiError
                    {
                        Title = "Invalid Token",
                        Message = "The token sended is not valid",
                        Code = "INVALID_TOKEN"
                    });
                }
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();