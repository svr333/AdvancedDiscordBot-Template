using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Toolbelt.Blazor.Extensions.DependencyInjection;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using System.IO;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace AdvancedBot.Dashboard
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddHeadElementHelper();
            services.AddSingleton<HttpClient>();
            services.AddResponseCompression();
            services.AddResponseCompression(options =>
            {
                IEnumerable<string> MimeTypes = new[] { "text/plain", "text/html", "text/css", "font/woff2", "font/woff", "font/ttf", "application/javascript", "image/x-icon", "image/png" };
                
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();

                options.Providers.Add<CustomCompressionProvider>();
                options.MimeTypes = MimeTypes;
                options.EnableForHttps = true;

                services.Configure<BrotliCompressionProviderOptions>(options => 
                {
                    options.Level = CompressionLevel.Optimal;
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseResponseCompression();
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
    public class CustomCompressionProvider : ICompressionProvider
    {
        public string EncodingName => "br";
        public bool SupportsFlush => true; 
        public Stream CreateStream(Stream outputStream) => new BrotliStream(outputStream, CompressionMode.Compress);
    }
}