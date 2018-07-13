using System;
using System.Linq;
using System.Reflection;
using IdentityModel;
using IdentityServer4;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NOB.IDS.Data;
using NOB.IDS.Models;

namespace NOB.IDS
{
    public class Startup
    {
        private const string CertificateName = "nob-ids-cert";

        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.Configure<IISOptions>(iis =>
            {
                //iis.AuthenticationDisplayName = "Windows";
                iis.AutomaticAuthentication = false;
                iis.ForwardClientCertificate = false;
            });

            var authConnectionString = Configuration.GetConnectionString("AUTH.Connection");
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(authConnectionString));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();


            var builder = services.AddIdentityServer(options =>
                {
                    options.Events.RaiseErrorEvents = true;
                    options.Events.RaiseInformationEvents = true;
                    options.Events.RaiseFailureEvents = true;
                    options.Events.RaiseSuccessEvents = true;
                })
                //.AddInMemoryIdentityResources(Config.GetIdentityResources())
                //.AddInMemoryApiResources(Config.GetApiResources())
                //.AddInMemoryClients(Config.GetClients())
                // this adds the config data from DB (clients, resources)
                .AddConfigurationStore(options =>
                {
                    options.ConfigureDbContext = x =>
                        x.UseSqlServer(authConnectionString,
                            sql => sql.MigrationsAssembly(migrationsAssembly));
                })
                // this adds the operational data from DB (codes, tokens, consents)
                .AddOperationalStore(options =>
                {
                    options.ConfigureDbContext = x =>
                        x.UseSqlServer(authConnectionString,
                            sql => sql.MigrationsAssembly(migrationsAssembly));

                    // this enables automatic token cleanup. this is optional.
                    options.EnableTokenCleanup = true;
                    options.TokenCleanupInterval = 30;
                })
                .AddAspNetIdentity<ApplicationUser>();

            if (Environment.IsDevelopment())
                builder.AddDeveloperSigningCredential();
            else
                ConfigureSigningCerts(services, CertificateName);

            services.AddAuthentication()
                .AddGoogle("Google", options =>
                {
                    options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;

                    options.ClientId = "434483408261-55tc8n0cs4ff1fe21ea8df2o443v2iuc.apps.googleusercontent.com";
                    options.ClientSecret = "3gcoTrEDPPJ0ukn_aYYT6PWo";
                })
                .AddOpenIdConnect("oidc", "OpenID Connect", options =>
                {
                    options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                    options.SignOutScheme = IdentityServerConstants.SignoutScheme;

                    options.Authority = "https://demo.identityserver.io/";
                    options.ClientId = "implicit";

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = "name",
                        RoleClaimType = "role"
                    };
                });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();
            app.UseIdentityServer();
            app.UseMvcWithDefaultRoute();
        }

        private static void ConfigureSigningCerts(IServiceCollection services, string certificateName)
        {
            //The one that expires last at the top
            var certs = X509.LocalMachine.My.SubjectDistinguishedName.Find("CN=" + certificateName, false)
                .Where(o => DateTime.UtcNow >= o.NotBefore)
                .OrderByDescending(o => o.NotAfter);

            // ReSharper disable once PossibleMultipleEnumeration
            if (!certs.Any()) throw new Exception("No valid certificates could be found.");

            //Get first (in desc order of expiry)
            // ReSharper disable once PossibleMultipleEnumeration
            var signingCert = certs.FirstOrDefault();

            if (signingCert == null)
                throw new InvalidOperationException("No valid signing certificate could be found.");

            var signingCredential = new SigningCredentials(new X509SecurityKey(signingCert), "RS256");
            services.AddSingleton<ISigningCredentialStore>(new DefaultSigningCredentialsStore(signingCredential));

            // ReSharper disable once PossibleMultipleEnumeration
            var keys = certs.Select(cert => new SigningCredentials(new X509SecurityKey(cert), "RS256"))
                .Select(validationCredential => validationCredential.Key).ToList();

            services.AddSingleton<IValidationKeysStore>(new DefaultValidationKeysStore(keys));
        }
    }
}