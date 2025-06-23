using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Raven.Client.Documents;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RavenUploader
{
    public class User
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string City { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false);
                })
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;
                    services.AddSingleton<IDocumentStore>(provider =>
                    {
                        return new DocumentStore
                        {
                            Urls = new[] { configuration["Raven:Url"] },
                            Database = configuration["Raven:Database"]
                        }.Initialize();
                    });
                })
                .Build();

            var store = host.Services.GetRequiredService<IDocumentStore>();

            using (var session = store.OpenAsyncSession())
            {
                var users = new List<User>
                {
                    new User { Name = "Robert Smythe", Email = "robert@example.com", City = "Raleigh" },
                    new User { Name = "Rob Smyth", Email = "rob@example.com", City = "Raleigh" },
                    new User { Name = "Alice Johnson", Email = "alice@example.com", City = "Durham" },
                    new User { Name = "Alicia Jonson", Email = "alicia@example.com", City = "Durham" },
                    new User { Name = "Bob Smith", Email = "bob@example.com", City = "Raleigh" },
                    new User { Name = "Bobby Smythe", Email = "bobby@example.com", City = "Raleigh" },
                    new User { Name = "Robert Smith", Email = "roberts@example.com", City = "Raleigh" }
                };

                foreach (var user in users)
                {
                    await session.StoreAsync(user);
                }

                await session.SaveChangesAsync();
                Console.WriteLine("Sample users uploaded to RavenDB.");
            }
        }
    }
}
