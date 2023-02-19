using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SmallSafe.Web.Data;

namespace SmallSafe.Web;

public class Startup
{
    public Startup(IWebHostEnvironment env)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(env.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
            .AddEnvironmentVariables();
        Configuration = builder.Build();
        Environment = env;
    }

    public IConfigurationRoot Configuration { get; }
    public IWebHostEnvironment Environment { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IConfiguration>(Configuration);

        services
            .AddAuthentication(o => o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.LoginPath = "/signin";
                o.LogoutPath = "/signout";
                o.Cookie.HttpOnly = true;
                o.Cookie.MaxAge = TimeSpan.FromDays(1);
                o.ExpireTimeSpan = TimeSpan.FromDays(1);
                o.SlidingExpiration = true;
            })
            .AddOpenIdConnect(options =>
            {
                var openIdOptions = Configuration.GetSection("SmallSafeOpenId");
                options.ClientId = openIdOptions.GetValue("ClientId", "");
                options.ClientSecret = openIdOptions.GetValue("ClientSecret", "");

                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.AuthenticationMethod = OpenIdConnectRedirectBehavior.RedirectGet;
                options.Authority = "https://smallauth.nosuchblogger.com/";
                options.Scope.Add("roles");

                options.SecurityTokenValidator = new JwtSecurityTokenHandler
                {
                    InboundClaimTypeMap = new Dictionary<string, string>()
                };

                options.TokenValidationParameters.NameClaimType = "name";
                options.TokenValidationParameters.RoleClaimType = "role";

                options.AccessDeniedPath = "/";
            });

        services
            .AddDataProtection()
            .SetApplicationName(typeof(Startup).Namespace ?? "")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Environment.ContentRootPath, ".keys")));

        services.AddLogging(logging =>
        {
            logging.AddSimpleConsole(opt =>
            {
                opt.UseUtcTimestamp = true;
                opt.TimestampFormat = "[HH:mm:ss.fff] ";
                opt.SingleLine = true;
            });
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Trace);
        });

        services.Configure<CookiePolicyOptions>(o =>
        {
            o.CheckConsentNeeded = context => false;
            o.MinimumSameSitePolicy = SameSiteMode.None;
        });

        services.AddMvc().AddSessionStateTempDataProvider();
        var builder = services.AddRazorPages();
#if DEBUG
        if (Environment.IsDevelopment())
            builder.AddRazorRuntimeCompilation();
#endif
        services.AddCors();
        services.AddDistributedMemoryCache();
        services.AddSession(options => options.IdleTimeout = TimeSpan.FromMinutes(5));

        services
            .AddDbContext<SqliteDataContext>((serviceProvider, options) =>
            {
                var sqliteConnectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("SmallSafe");
                serviceProvider.GetRequiredService<ILogger<SqliteDataContext>>().LogInformation($"Using connection string: {sqliteConnectionString}");
                options.UseSqlite(sqliteConnectionString);
            })
            .AddScoped(sp => (ISqliteDataContext)sp.GetRequiredService<SqliteDataContext>());
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
    {
        if (env.IsDevelopment())
            app.UseDeveloperExceptionPage();
        else
            app.UseExceptionHandler("/Home/Error");

        app.UseStaticFiles();
        app.UseCookiePolicy();
        app.UseSession();
        app.UseAuthentication();
        app.UseRouting();
        app.UseAuthorization();
        app.UseEndpoints(options => options.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}"));

        using var scope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
        scope.ServiceProvider.GetRequiredService<ISqliteDataContext>().Migrate();
    }
}
