using System.IdentityModel.Tokens.Jwt;
using System.Text;
using ApiServer.Data;
using ApiServer.Filters;
using ApiServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// --- POSTGRES ----------------------------------------------------------------------------------------------------
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

// --- REDIS ----------------------------------------------------------------------------------------------------
    builder.Services.AddSingleton<IConnectionMultiplexer>(
        ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

// --- SIGNALR WITH REDIS BACKPLANE-------------------------------------------------------------------------------
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!);

// --- CORS ------------------------------------------------------------------------------------------------------
    var spaOrigin = builder.Configuration["Authentication:Spa:Origin"]
                    ?? throw new InvalidOperationException("Missing Authentication:Spa:Origin");

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("spa", p =>
            p.WithOrigins(spaOrigin)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
    });

    const string ExternalScheme = "External";

// --- AUTH ----------------------------------------------------------------------------------------------------
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = ExternalScheme;
        })
        .AddCookie(ExternalScheme, o =>
        {
            o.Cookie.Name = "gs_ext";
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Lax;
        })
        .AddGoogle(options =>
        {
            options.SignInScheme = ExternalScheme;
            options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? throw new InvalidOperationException("Missing Authentication:Google:ClientId");
            options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? throw new InvalidOperationException("Missing Authentication:Google:ClientSecret");
        })
        .AddJwtBearer(options =>
        {
            var jwtSecret = builder.Configuration["Authentication:Jwt:Secret"] ?? throw new InvalidOperationException("Missing Authentication:Jwt:Secret");
            var jwtIssuer = builder.Configuration["Authentication:Jwt:Issuer"] ?? throw new InvalidOperationException("Missing Authentication:Jwt:Issuer");
            var jwtAudience = builder.Configuration["Authentication:Jwt:Audience"] ?? throw new InvalidOperationException("Missing Authentication:Jwt:Audience");

            var keyBytes = Encoding.UTF8.GetBytes(jwtSecret);
            if (keyBytes.Length < 32)
                throw new InvalidOperationException("Authentication:Jwt:Secret must be at least 32 bytes.");

            options.MapInboundClaims = false;
            options.TokenValidationParameters = new()
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = JwtRegisteredClaimNames.Sub,
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.Request.Cookies.TryGetValue("gs_auth", out var token))
                    {
                        context.Token = token;
                        return Task.CompletedTask;
                    }
                    
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken)
                        && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                },
            };
        });

// --- SERVICES ----------------------------------------------------------------------------------------------------
    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddOpenApi();

// --- FILTERS -----------------------------------------------------------------------------------------------------

    builder.Services.AddScoped<RequireCustomHeaderFilter>();

    builder.Services.AddControllers(options =>
    {
        options.Filters.AddService<RequireCustomHeaderFilter>();
    });

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // Trust only local reverse proxy (Caddy/nginx on same host)
        options.KnownProxies.Add(System.Net.IPAddress.Loopback);
        options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
        options.ForwardLimit = 1;
    });


// --- APP ----------------------------------------------------------------------------------------------------
    var app = builder.Build();
    app.UseForwardedHeaders(); 

// Dev-only migrations before serving traffic
    if (app.Environment.IsDevelopment())
    {
    //    using var scope = app.Services.CreateScope();
     //   var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      //  db.Database.Migrate();
        app.MapOpenApi();

    }

    app.UseRouting();
    app.UseCors("spa");

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
