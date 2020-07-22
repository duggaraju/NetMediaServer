using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WebServer
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMemoryCache(cacheOptions => { });
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder => builder.AllowAnyOrigin());
            });
            services.Configure<IISServerOptions>(options => options.AutomaticAuthentication = false);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseCors();
            app.UseEndpoints(endpoints =>
            {
                var cache = endpoints.ServiceProvider.GetService<IMemoryCache>();
                var streaming = new DashStreamingHandler(cache, loggerFactory.CreateLogger<DashStreamingHandler>());
                endpoints.MapGet("/{**rest}", async context =>
                {
                    await streaming.InvokeAsync(context);
                }).RequireCors(builder => builder.AllowAnyOrigin());

                var ingest = new DashIngestHandler(cache, loggerFactory.CreateLogger<DashIngestHandler>());
                endpoints.MapPost("/{**rest}", async context =>
                {
                    await ingest.InvokeAsync(context);
                });

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync($"Hello Streaming!");
                });
            });
        }
    }
}
